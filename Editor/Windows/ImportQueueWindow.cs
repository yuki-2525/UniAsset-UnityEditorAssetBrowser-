// Copyright (c) 2025 sakurayuki

#nullable enable

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEditorAssetBrowser.Services;
using UnityEditorAssetBrowser.Helper;

namespace UnityEditorAssetBrowser.Windows
{
    /// <summary>
    /// インポートリストを表示・操作するウィンドウ
    /// </summary>
    public class ImportQueueWindow : EditorWindow
    {
        private ReorderableList? _reorderableList;
        private Vector2 _scrollPosition;

        [MenuItem("Window/UniAsset Import List")]
        public static void ShowWindow()
        {
            var window = GetWindow<ImportQueueWindow>();
            window.titleContent = new GUIContent("Import List");
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(LocalizationService.Instance.GetString("import_list") ?? "Import List");
            InitializeReorderableList();
            
            ImportQueueService.Instance.OnQueueChanged += Repaint;
            ImportQueueService.Instance.OnImportProgress += OnImportProgress;
            ImportQueueService.Instance.OnImportCompleted += OnImportCompleted;
        }

        private void OnDisable()
        {
            ImportQueueService.Instance.OnQueueChanged -= Repaint;
            ImportQueueService.Instance.OnImportProgress -= OnImportProgress;
            ImportQueueService.Instance.OnImportCompleted -= OnImportCompleted;
        }

        private void OnImportProgress(int current, int total)
        {
            Repaint();
        }

        private void OnImportCompleted()
        {
            Repaint();
            ShowNotification(new GUIContent(LocalizationService.Instance.GetString("import_completed") ?? "Import Completed"));
        }

        private void InitializeReorderableList()
        {
            _reorderableList = new ReorderableList(
                (System.Collections.IList)ImportQueueService.Instance.Queue,
                typeof(Models.ImportQueueItem),
                true, true, false, true
            );

            _reorderableList.drawHeaderCallback = (rect) =>
            {
                EditorGUI.LabelField(rect, LocalizationService.Instance.GetString("import_list") ?? "Import List");
            };

            _reorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var item = ImportQueueService.Instance.Queue[index];
                float height = _reorderableList.elementHeight;
                
                // サムネイル表示（あれば）
                var iconRect = new Rect(rect.x, rect.y + 2, height - 4, height - 4);
                if (!string.IsNullOrEmpty(item.ThumbnailPath))
                {
                    var texture = ImageServices.Instance.LoadTexture(item.ThumbnailPath);
                    if (texture != null)
                    {
                        GUI.DrawTexture(iconRect, texture, ScaleMode.ScaleToFit);
                    }
                }

                // テキスト表示
                var labelRect = new Rect(rect.x + height, rect.y, rect.width - height, rect.height);
                EditorGUI.LabelField(labelRect, item.PackageName, EditorStyles.boldLabel);
                
                // パスをツールチップとして表示するための透明ボタン
                GUI.Label(rect, new GUIContent("", item.PackagePath));
            };

            _reorderableList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
            {
                ImportQueueService.Instance.Move(oldIndex, newIndex);
            };

            _reorderableList.onRemoveCallback = (list) =>
            {
                ImportQueueService.Instance.Remove(list.index);
            };
            
            // 要素の高さを少し広げる
            _reorderableList.elementHeight = 24;
        }

        private void OnGUI()
        {
            // ドラッグ＆ドロップ受信処理
            HandleDragAndDrop();

            if (ImportQueueService.Instance.IsImporting)
            {
                EditorGUILayout.HelpBox(LocalizationService.Instance.GetString("importing") ?? "Importing...", MessageType.Info);
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            if (_reorderableList != null)
            {
                // リストの参照を更新（ReorderableListは参照を保持するため、List自体が変わるわけではないが念のため）
                _reorderableList.list = (System.Collections.IList)ImportQueueService.Instance.Queue;
                _reorderableList.DoLayoutList();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(LocalizationService.Instance.GetString("import_list_help") ?? "You can add items to this list by right-clicking a unitypackage or by dragging and dropping it here.", MessageType.Info);

            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button(LocalizationService.Instance.GetString("clear_list") ?? "Clear List"))
            {
                ImportQueueService.Instance.Clear();
            }

            GUI.enabled = !ImportQueueService.Instance.IsImporting && ImportQueueService.Instance.Queue.Count > 0;
            if (GUILayout.Button(LocalizationService.Instance.GetString("start_import") ?? "Start Import", GUILayout.Height(30)))
            {
                ImportQueueService.Instance.StartImport();
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            Rect dropArea = new Rect(0, 0, position.width, position.height);

            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        var paths = DragAndDrop.paths; // ファイルからのD&D用
                        var genericData = DragAndDrop.GetGenericData("ImportQueueItem"); // 内部D&D用

                        if (genericData is Models.ImportQueueItem item)
                        {
                            ImportQueueService.Instance.Add(item.PackagePath, item.PackageName, item.ThumbnailPath, item.Category);
                        }
                        else if (paths != null && paths.Length > 0)
                        {
                            // 外部ファイル（エクスプローラー等）からのD&D対応も可能ならここで行う
                            // 今回は要件に「アセット閲覧ウィンドウから」とあるので、内部D&Dが主
                        }
                    }
                    break;
            }
        }
    }
}
