// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEditorAssetBrowser.Interfaces;
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
        private string _boothIdInput = "";
        
        // 複数選択用のフィールド
        private readonly List<int> _selectedIndices = new List<int>();
        private int _lastSelectedIndex = -1;

        [MenuItem("Window/UniAsset Import List")]
        public static void ShowWindow()
        {
            DebugLogger.Log("Opening ImportQueueWindow.");
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
            DebugLogger.Log($"Import progress: {current}/{total}");
            Repaint();
        }

        private void OnImportCompleted()
        {
            DebugLogger.Log("Import completed.");
            Repaint();
            ShowNotification(new GUIContent(LocalizationService.Instance.GetString("import_completed") ?? "Import Completed"));
        }

        private void InitializeReorderableList()
        {
            _reorderableList = new ReorderableList(
                (System.Collections.IList)ImportQueueService.Instance.Queue,
                typeof(Models.ImportQueueItem),
                true, false, false, true
            );

            _reorderableList.onSelectCallback = (list) =>
            {
                UpdateSelection(list.index);
            };

            _reorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                // 複数選択用の背景描画
                // デフォルトの青背景(isActive)と被らないように調整するか、
                // リスト項目が選択されていることを示す
                if (_selectedIndices.Contains(index))
                {
                    // ReorderableListはisActiveがtrueの場合にデフォルトの青背景を描画する
                    // ここではそれ以外の選択済み項目、またはisActiveでも自前の描画を行う
                    if (!isActive) 
                    {
                        var color = GUI.skin.settings.selectionColor;
                        color.a = 0.15f; // 少し薄くする
                        EditorGUI.DrawRect(rect, color);
                    }
                }

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
                
                // 並び替え後は選択状態をリセットし、移動した項目を選択する
                _selectedIndices.Clear();
                _selectedIndices.Add(newIndex);
                _lastSelectedIndex = newIndex;
            };

            _reorderableList.onRemoveCallback = (list) =>
            {
                RemoveSelectedItems();
            };
            
            // 要素の高さを少し広げる
            _reorderableList.elementHeight = 24;
        }

        private void UpdateSelection(int index)
        {
            Event evt = Event.current;
            
            if (evt.shift)
            {
                // Shift: 範囲選択
                if (_lastSelectedIndex == -1) _lastSelectedIndex = index;
                
                int start = Mathf.Min(_lastSelectedIndex, index);
                int end = Mathf.Max(_lastSelectedIndex, index);
                
                _selectedIndices.Clear();
                for (int i = start; i <= end; i++)
                {
                    _selectedIndices.Add(i);
                }
            }
            else if (evt.control || evt.command) // Ctrl/Cmd: 個別トグル
            {
                if (_selectedIndices.Contains(index))
                {
                    _selectedIndices.Remove(index);
                }
                else
                {
                    _selectedIndices.Add(index);
                }
                _lastSelectedIndex = index;
            }
            else
            {
                // 通常クリック: 単一選択
                _selectedIndices.Clear();
                _selectedIndices.Add(index);
                _lastSelectedIndex = index;
            }
        }

        private void RemoveSelectedItems()
        {
            if (_selectedIndices.Count == 0)
            {
                // ここには基本こないはずだが、念のため最後にクリックされた要素を削除
                if (_reorderableList != null && _reorderableList.index >= 0)
                {
                     ImportQueueService.Instance.Remove(_reorderableList.index);
                     _reorderableList.index = -1;
                }
                return;
            }

            // インデックスが大きい順に削除しないとずれる
            var sortedIndices = _selectedIndices.OrderByDescending(i => i).ToList();
            
            foreach (var index in sortedIndices)
            {
                ImportQueueService.Instance.Remove(index);
            }
            
            _selectedIndices.Clear();
            _lastSelectedIndex = -1;
            if (_reorderableList != null) _reorderableList.index = -1;
            
            // 削除後に再描画
            Repaint();
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
            EditorGUIUtility.labelWidth = 70;
            
            // フォーカス名を先に定義
            string controlName = "BoothIdInput";
            GUI.SetNextControlName(controlName);

            // TextFieldがイベントを消費する前にEnterキーを検知する
            bool enterPressed = Event.current.type == EventType.KeyDown && 
                                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && 
                                GUI.GetNameOfFocusedControl() == controlName;
            
            if (enterPressed)
            {
                Event.current.Use();
            }

            _boothIdInput = EditorGUILayout.TextField("ID or URL", _boothIdInput);
            EditorGUIUtility.labelWidth = 0;

            // 追加ボタンまたはEnterキー押下時の処理
            if (GUILayout.Button(LocalizationService.Instance.GetString("add") ?? "Add", GUILayout.Width(60)) || enterPressed)
            {
                // UIイベントのループ中に値を変更したりフォーカスを変更すると問題が起きることがあるため
                // 処理を遅延させるか、このまま実行するか検討が必要だが、
                // ここでは即時実行しても、TextFieldの描画が終わった後(次のフレーム)で反映されるため問題ないはず
                AddPackagesFromBoothId(_boothIdInput);
                _boothIdInput = "";
                GUI.FocusControl(controlName);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button(LocalizationService.Instance.GetString("clear_list") ?? "Clear List"))
            {
                DebugLogger.Log("Clear Import Queue List button clicked.");
                ImportQueueService.Instance.Clear();
            }

            GUI.enabled = !ImportQueueService.Instance.IsImporting && ImportQueueService.Instance.Queue.Count > 0;
            if (GUILayout.Button(LocalizationService.Instance.GetString("start_import") ?? "Start Import", GUILayout.Height(30)))
            {
                DebugLogger.Log("Start Import button clicked.");
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
                        var databaseItem = DragAndDrop.GetGenericData("ImportQueue_DatabaseItem") as IDatabaseItem; // MainViewからのD&D

                        if (genericData is Models.ImportQueueItem item)
                        {
                            DebugLogger.Log($"Dropped internal item to Import Queue: {item.PackageName}");
                            ImportQueueService.Instance.Add(item.PackagePath, item.PackageName, item.ThumbnailPath, item.Category);
                        }
                        else if (databaseItem != null)
                        {
                            DebugLogger.Log($"Dropped Database Item: {databaseItem.GetTitle()}");
                            AddPackagesFromItem(databaseItem);
                        }
                        else if (paths != null && paths.Length > 0)
                        {
                            DebugLogger.Log($"Dropped files to Import Queue: {string.Join(", ", paths)}");
                            HandleExternalFilesDrop(paths);
                        }
                    }
                    break;
            }
        }

        private void HandleExternalFilesDrop(string[] paths)
        {
            DebugLogger.Log($"HandleExternalFilesDrop: Processing {paths.Length} files.");
            int foundItemsCount = 0;
            int directPackagesCount = 0;

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                {
                    DebugLogger.Log($"Skipping invalid path: {path}");
                    continue;
                }

                // UnityPackageなら直接追加
                if (path.EndsWith(".unitypackage", System.StringComparison.OrdinalIgnoreCase))
                {
                    DebugLogger.Log($"File is UnityPackage: {path}");
                    ImportQueueService.Instance.Add(path, System.IO.Path.GetFileName(path), null, "External");
                    directPackagesCount++;
                    continue;
                }

                // 画像ファイルならデータベースからアイテムを逆引きして追加
                if (IsImageFile(path))
                {
                    DebugLogger.Log($"File is Image, searching for item: {path}");
                    var item = FindItemByImagePath(path);
                    if (item != null)
                    {
                        DebugLogger.Log($"Item found: {item.GetTitle()} ({item.GetItemPath()})");
                        AddPackagesFromItem(item);
                        foundItemsCount++;
                    }
                    else
                    {
                        DebugLogger.Log($"Item not found for image: {path}");
                    }
                }
                else
                {
                    DebugLogger.Log($"Unsupported file type: {path}");
                }
            }

            if (foundItemsCount == 0 && directPackagesCount == 0)
            {
                // 何も追加されなかった場合（画像だがDBに見つからない、または非対応形式）
                // 必要に応じてメッセージを出すが、D&Dはサイレント失敗も一般的
                DebugLogger.Log("No items added from dropped files.");
            }
        }

        private bool IsImageFile(string path)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif";
        }

        private IDatabaseItem? FindItemByImagePath(string path)
        {
            string normalizedPath = NormalizePath(path);
            DebugLogger.Log($"Searching item for normalized image path: {normalizedPath}");

            bool IsMatch(IDatabaseItem item)
            {
                var imgPath = item.GetImagePath();
                if (string.IsNullOrEmpty(imgPath)) return false;
                return NormalizePath(imgPath) == normalizedPath;
            }

            // AE Database
            var aeDb = DatabaseService.GetAEDatabase();
            if (aeDb != null)
            {
                DebugLogger.Log("Searching in AE Database...");
                var item = aeDb.Items.FirstOrDefault(IsMatch);
                if (item != null)
                {
                    DebugLogger.Log("Found in AE Database.");
                    return item;
                }
            }

            // KA Database
            var kaDb = DatabaseService.GetKADatabase();
            if (kaDb != null)
            {
                DebugLogger.Log("Searching in KA Database...");
                var item = kaDb.Items.FirstOrDefault(IsMatch);
                if (item != null)
                {
                    DebugLogger.Log("Found in KA Database.");
                    return item;
                }
            }

            // BOOTHLM Database
            var boothlmDb = DatabaseService.GetBOOTHLMDatabase();
            if (boothlmDb != null)
            {
                DebugLogger.Log("Searching in BOOTHLM Database...");
                var item = boothlmDb.Items.FirstOrDefault(IsMatch);
                if (item != null)
                {
                    DebugLogger.Log("Found in BOOTHLM Database.");
                    return item;
                }
            }

            DebugLogger.Log("Item not found in any database.");
            return null;
        }

        private string NormalizePath(string path)
        {
            try
            {
                return System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).ToLowerInvariant();
            }
            catch
            {
                return path.ToLowerInvariant();
            }
        }

        /// <summary>
        /// IDatabaseItemからパッケージを検索して追加する
        /// </summary>
        private void AddPackagesFromItem(IDatabaseItem item)
        {
            var itemPath = item.GetItemPath();
            if (string.IsNullOrEmpty(itemPath) || !System.IO.Directory.Exists(itemPath))
            {
                 DebugLogger.LogError($"Item path not found: {itemPath}");
                 ShowNotification(new GUIContent(LocalizationService.Instance.GetString("download_data_missing") ?? "Download data missing"));
                 return;
            }

            // アイテムのパスからUnityPackageを検索
            var packages = UnityPackageServices.FindUnityPackages(itemPath);
            if (packages == null || packages.Length == 0)
            {
                 DebugLogger.Log($"No unitypackages found for item: {item.GetTitle()}");
                 ShowNotification(new GUIContent(LocalizationService.Instance.GetString("error_no_packages") ?? "No packages found"));
                return;
            }

            // 見つかったパッケージをキューに追加
            int addedCount = 0;
            foreach (var pkg in packages)
            {
                ImportQueueService.Instance.Add(
                    pkg,
                    System.IO.Path.GetFileName(pkg),
                    item.GetImagePath(),
                    item.GetCategory()
                );
                addedCount++;
            }
            
            ShowNotification(new GUIContent($"Added {addedCount} packages."));
        }

        /// <summary>
        /// Booth IDまたはURLからパッケージを検索してリストに追加する
        /// </summary>
        /// <param name="input">Booth ID または 商品ページURL</param>
        private void AddPackagesFromBoothId(string input)
        {
            if (string.IsNullOrEmpty(input)) return;

            int boothId = 0;
            if (int.TryParse(input, out int id))
            {
                // ID直接入力の場合
                boothId = id;
            }
            else
            {
                // URLからID抽出を試みる
                // マッチパターン: items/123456 (URLの一部)
                var match = System.Text.RegularExpressions.Regex.Match(input, @"items/(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int urlId))
                {
                    boothId = urlId;
                }
            }

            if (boothId == 0)
            {
                DebugLogger.LogError($"Invalid Booth ID or URL: {input}");
                ShowNotification(new GUIContent(LocalizationService.Instance.GetString("error_invalid_booth_id") ?? "Invalid Booth ID or URL format."));
                return;
            }

            IDatabaseItem? foundItem = null;

            // 各データベースからアイテムを検索
            // AvatarExplorerデータベースを検索
            var aeDb = DatabaseService.GetAEDatabase();
            if (aeDb != null)
                foundItem = aeDb.Items.FirstOrDefault(x => x.GetBoothId() == boothId);

            // KonoAssetデータベースを検索
            if (foundItem == null)
            {
                var kaDb = DatabaseService.GetKADatabase();
                if (kaDb != null)
                    foundItem = kaDb.Items.FirstOrDefault(x => x.GetBoothId() == boothId);
            }

            // BOOTHLMデータベースを検索
            if (foundItem == null)
            {
                var boothlmDb = DatabaseService.GetBOOTHLMDatabase();
                if (boothlmDb != null)
                    foundItem = boothlmDb.Items.FirstOrDefault(x => x.GetBoothId() == boothId);
            }

            if (foundItem == null)
            {
                DebugLogger.Log($"Item with Booth ID {boothId} not found.");
                ShowNotification(new GUIContent(LocalizationService.Instance.GetString("item_not_registered") ?? "Unregistered item"));
                return;
            }

            AddPackagesFromItem(foundItem);
        }
    }
}
