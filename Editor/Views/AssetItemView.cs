// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorAssetBrowser.Helper;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Services;
using UnityEditorAssetBrowser.Windows;
using UnityEngine;

namespace UnityEditorAssetBrowser.Views
{
    /// <summary>
    /// アセットアイテムの表示を管理するビュー
    /// AvatarExplorerとKonoAssetのアイテムを統一的に表示する
    /// </summary>
    public class AssetItemView
    {
        /// <summary>メモのフォールドアウト状態</summary>
        private readonly Dictionary<string, bool> _memoFoldouts = new();

        /// <summary>UnityPackageのフォールドアウト状態</summary>
        private readonly Dictionary<string, bool> _unityPackageFoldouts = new();

        // 色を循環させる（赤、青、緑、黄、紫、水色）
        private static readonly Color[] LineColors = new Color[]
        {
            new Color(1f, 0f, 0f, 0.5f), // 赤
            new Color(0f, 0f, 1f, 0.5f), // 青
            new Color(0f, 1f, 0f, 0.5f), // 緑
            new Color(1f, 1f, 0f, 0.5f), // 黄
            new Color(1f, 0f, 1f, 0.5f), // 紫
            new Color(0f, 1f, 1f, 0.5f), // 水色
        };

        /// <summary>
        /// AEアバターアイテムの表示
        /// </summary>
        /// <param name="item">表示するアイテム</param>
        public void ShowAvatarItem(IDatabaseItem item)
        {
            GUILayout.BeginVertical(GUIStyleManager.BoxStyle);

            DrawItemHeader(
                item.GetTitle(),
                item.GetAuthor(),
                item.GetImagePath(),
                item.GetItemPath(),
                item.GetCreatedDate(),
                item.GetCategory(),
                item.GetSupportedAvatars(),
                item.GetTags(),
                item.GetMemo(),
                item.GetBoothId()
            );
            DrawUnityPackageSection(item.GetItemPath(), item.GetTitle(), item.GetImagePath(), item.GetCategory());

            GUILayout.EndVertical();
        }

        /// <summary>
        /// アイテムヘッダーの描画
        /// </summary>
        /// <param name="title">タイトル</param>
        /// <param name="author">作者名</param>
        /// <param name="imagePath">画像パス</param>
        /// <param name="itemPath">アイテムパス</param>
        /// <param name="createdDate">作成日（ソート用）</param>
        /// <param name="category">カテゴリ</param>
        /// <param name="supportedAvatars">対応アバター</param>
        /// <param name="tags">タグ</param>
        /// <param name="memo">メモ</param>
        /// <param name="boothItemId">BoothアイテムID</param>
        private void DrawItemHeader(
            string title,
            string author,
            string imagePath,
            string itemPath,
            DateTime createdDate,
            string category,
            string[] supportedAvatars,
            string[] tags,
            string memo,
            int boothItemId = 0
        )
        {
            GUILayout.BeginHorizontal();

            DrawItemImage(imagePath);

            GUILayout.BeginVertical();
            
            DrawItemBasicInfo(title, author);
            DrawItemMetadata(title, category, supportedAvatars, tags, memo);
            DrawItemActionButtons(itemPath, boothItemId, imagePath);

            GUILayout.EndVertical();
            
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// アイテムの基本情報（タイトル・作者）を描画
        /// </summary>
        private void DrawItemBasicInfo(string title, string author)
        {
            GUILayout.Label(title, GUIStyleManager.BoldLabel);
            GUILayout.Label($"{LocalizationService.Instance.GetString("author")}{author}", GUIStyleManager.Label);
        }

        /// <summary>
        /// アイテムのメタデータ（カテゴリ・対応アバター・タグ・メモ）を描画
        /// </summary>
        private void DrawItemMetadata(string title, string category, string[] supportedAvatars, string[] tags, string memo)
        {
            // カテゴリ
            if (!string.IsNullOrEmpty(category))
                DrawCategory(category);

            // 対応アバター
            if (supportedAvatars.Length > 0)
                DrawSupportedAvatars(supportedAvatars);

            // タグ
            if (tags.Length > 0)
                GUILayout.Label($"{LocalizationService.Instance.GetString("tags")}{string.Join(", ", tags)}", GUIStyleManager.WordWrappedLabel);

            // メモ
            if (!string.IsNullOrEmpty(memo))
                DrawMemo(title, memo);
        }

        /// <summary>
        /// アクションボタン（エクスプローラー・Booth）を描画
        /// </summary>
        private void DrawItemActionButtons(string itemPath, int boothItemId, string imagePath)
        {
            EditorGUILayout.Space(5);
            DrawSetFolderThumbnailButton(imagePath);
            DrawExplorerOpenButton(itemPath);

            if (boothItemId > 0)
            {
                DrawBoothOpenButton(boothItemId);
            }

            if (!Directory.Exists(itemPath))
            {
                EditorGUILayout.HelpBox(LocalizationService.Instance.GetString("download_data_missing"), MessageType.Info);
            }
        }

        /// <summary>
        /// Booth商品ページを開くボタンを描画
        /// </summary>
        private void DrawBoothOpenButton(int boothItemId)
        {
            if (GUILayout.Button(LocalizationService.Instance.GetString("open_product_page"), GUIStyleManager.Button, GUILayout.Width(150)))
            {
                Application.OpenURL($"https://booth.pm/ja/items/{boothItemId}");
            }
        }

        /// <summary>
        /// "フォルダにサムネイルを付与"ボタンを描画
        /// </summary>
        private void DrawSetFolderThumbnailButton(string imagePath)
        {
            if (GUILayout.Button(LocalizationService.Instance.GetString("set_folder_thumbnail"), GUIStyleManager.Button, GUILayout.Width(150)))
            {
                DebugLogger.Log("SetFolderThumbnail button clicked");
                string folderPath = EditorUtility.OpenFolderPanel(
                    LocalizationService.Instance.GetString("select_directory"),
                    "Assets",
                    ""
                );

                if (!string.IsNullOrEmpty(folderPath))
                {
                    DebugLogger.Log($"Selected folder for thumbnail: {folderPath}");
                    // Assetsフォルダからの相対パスに変換
                    if (folderPath.StartsWith(Application.dataPath))
                    {
                        folderPath = "Assets" + folderPath.Substring(Application.dataPath.Length);
                    }
                    
                    UnityPackageServices.SetFolderThumbnails(new List<string> { folderPath }, imagePath, true);
                }
            }
        }

        /// <summary>
        /// カテゴリの描画
        /// </summary>
        /// <param name="title">タイトル</param>
        /// <param name="category">カテゴリ</param>
        private void DrawCategory(string category)
        {
            GUILayout.Label($"{LocalizationService.Instance.GetString("category")}{category}", GUIStyleManager.Label);
        }

        /// <summary>
        /// 対応アバターの描画
        /// </summary>
        /// <param name="supportedAvatars">対応アバターのパス配列</param>
        private void DrawSupportedAvatars(string[] supportedAvatars)
        {
            string supportedAvatarsText = $"{LocalizationService.Instance.GetString("supported_avatars")}{string.Join(", ", supportedAvatars)}";

            GUILayout.Label(supportedAvatarsText, GUIStyleManager.WordWrappedLabel);
        }

        /// <summary>
        /// メモの描画
        /// </summary>
        /// <param name="title">タイトル</param>
        /// <param name="memo">メモ</param>
        private void DrawMemo(string title, string memo)
        {
            string memoKey = $"{title}_memo";
            if (!_memoFoldouts.ContainsKey(memoKey))
            {
                _memoFoldouts[memoKey] = false;
            }

            var startRect = EditorGUILayout.GetControlRect(false, 0);
            var startY = startRect.y;
            var boxRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

            if (Event.current.type == EventType.MouseDown && boxRect.Contains(Event.current.mousePosition))
            {
                _memoFoldouts[memoKey] = !_memoFoldouts[memoKey];
                GUI.changed = true;
                Event.current.Use();
            }

            string toggleText = _memoFoldouts[memoKey] ? $"▼{LocalizationService.Instance.GetString("memo")}" : $"▶{LocalizationService.Instance.GetString("memo")}";
            EditorGUI.LabelField(boxRect, toggleText, GUIStyleManager.Label);

            if (_memoFoldouts[memoKey])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(memo ?? string.Empty, GUIStyleManager.WordWrappedLabel);
                EditorGUI.indentLevel--;
            }

            var endRect = GUILayoutUtility.GetLastRect();
            var endY = endRect.y + endRect.height;
            var frameRect = new Rect(
                startRect.x,
                startY,
                EditorGUIUtility.currentViewWidth - 20,
                endY - startY + 10
            );
            EditorGUI.DrawRect(frameRect, new Color(0.5f, 0.5f, 0.5f, 0.2f));
            GUI.Box(frameRect, "", GUIStyleManager.BoxStyle);
        }

        /// <summary>
        /// アイテム画像の描画
        /// </summary>
        /// <param name="imagePath">画像パス</param>
        private void DrawItemImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath)) return;

            // URLまたはローカルファイルパスのチェック
            bool isUrl = imagePath.StartsWith("http://") || imagePath.StartsWith("https://");
            if (isUrl || File.Exists(imagePath))
            {
                var texture = ImageServices.Instance.LoadTexture(imagePath);
                if (texture == null) return;

                int size = GUIStyleManager.IconSize;
                GUILayout.Label(texture, GUILayout.Width(size), GUILayout.Height(size));
            }
        }

        /// <summary>
        /// "Explorerで開く"ボタンの描画
        /// </summary>
        /// <param name="itemPath">アイテムパス</param>
        private void DrawExplorerOpenButton(string itemPath)
        {
            if (!Directory.Exists(itemPath)) return;

            if (GUILayout.Button(LocalizationService.Instance.GetString("open_explorer"), GUIStyleManager.Button, GUILayout.Width(150)))
            {
                DebugLogger.Log($"Opening explorer for path: {itemPath}");
                Process.Start("explorer.exe", itemPath);
            }
        }

        /// <summary>
        /// UnityPackageアイテムの描画
        /// </summary>
        /// <param name="package">パッケージパス</param>
        /// <param name="imagePath">サムネイル画像パス</param>
        /// <param name="category">カテゴリ</param>
        private void DrawUnityPackageItem(string package, string imagePath, string category)
        {
            string directory = Path.GetDirectoryName(package);
            var parts = directory.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            string labelText;

            // 親フォルダの1つ上がUUID（GUID）形式、または英小文字+数字形式の場合は、冗長なので表示を省略する
            if (parts.Length >= 2 && (Guid.TryParse(parts[parts.Length - 2], out _) || System.Text.RegularExpressions.Regex.IsMatch(parts[parts.Length - 2], @"^[a-z]\d+$")))
            {
                labelText = parts.Last() + "/" + Path.GetFileName(package);
            }
            else
            {
                labelText = string.Join("/", parts.TakeLast(2)) + "/" + Path.GetFileName(package);
            }

            // 幅計算（インデントやスクロールバーの余裕を考慮）
            float indentPixels = EditorGUI.indentLevel * 15f;
            float padding = 40f; // コンテナのパディングやマージン
            float viewWidth = EditorGUIUtility.currentViewWidth - indentPixels - padding;

            float fullLabelWidth = GUIStyleManager.Label.CalcSize(new GUIContent(labelText)).x;
            float buttonWidth = 100f;
            float spacing = 20f;
            
            bool isMultiLine = (fullLabelWidth + buttonWidth + spacing) > viewWidth;
            
            // 表示用ラベルとツールチップの準備
            string displayLabel = labelText;
            string tooltip = "";

            // 利用可能なテキスト幅
            float availableTextWidth = isMultiLine ? viewWidth : (viewWidth - buttonWidth - spacing);

            // テキストが幅を超える場合の省略処理
            if (fullLabelWidth > availableTextWidth)
            {
                tooltip = labelText; // 全文をツールチップに設定
                
                // "..." の幅
                float ellipsisWidth = GUIStyleManager.Label.CalcSize(new GUIContent("...")).x;
                float targetWidth = availableTextWidth - ellipsisWidth;
                
                if (targetWidth > 0)
                {
                    // 後ろから文字を足していって幅に収まる最大長を探す
                    // パフォーマンスのため、ある程度推測してから調整する
                    float avgCharWidth = fullLabelWidth / labelText.Length;
                    int approxChars = (int)(targetWidth / avgCharWidth);
                    
                    // 安全マージンをとって開始位置を決定
                    int startIndex = Math.Max(0, labelText.Length - approxChars);
                    string tempText = labelText.Substring(startIndex);
                    
                    // 幅を超えている間、開始位置を後ろにずらす（文字数を減らす）
                    while (GUIStyleManager.Label.CalcSize(new GUIContent(tempText)).x > targetWidth && startIndex < labelText.Length)
                    {
                        startIndex++;
                        tempText = labelText.Substring(startIndex);
                    }
                    
                    // 幅に余裕がある間、開始位置を前にずらす（文字数を増やす）
                    while (startIndex > 0)
                    {
                        string nextTry = labelText.Substring(startIndex - 1);
                        if (GUIStyleManager.Label.CalcSize(new GUIContent(nextTry)).x > targetWidth)
                        {
                            break;
                        }
                        startIndex--;
                        tempText = nextTry;
                    }
                    
                    displayLabel = "..." + tempText;
                }
                else
                {
                    displayLabel = "..."; // 幅が極端に狭い場合
                }
            }

            GUIContent labelContent = new GUIContent(displayLabel, tooltip);
            Rect labelRect;

            if (isMultiLine)
            {
                GUILayout.BeginVertical();
                GUILayout.Label(labelContent, GUIStyleManager.Label);
                labelRect = GUILayoutUtility.GetLastRect();
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
            }
            else
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Label(labelContent, GUIStyleManager.Label);
                labelRect = GUILayoutUtility.GetLastRect();
                GUILayout.FlexibleSpace();
            }

            // ドラッグ開始処理
            if (Event.current.type == EventType.MouseDrag && labelRect.Contains(Event.current.mousePosition))
            {
                DebugLogger.Log($"Started dragging package: {Path.GetFileName(package)}");
                DragAndDrop.PrepareStartDrag();
                var item = new Models.ImportQueueItem
                {
                    PackagePath = package,
                    PackageName = Path.GetFileName(package),
                    ThumbnailPath = imagePath,
                    Category = category
                };
                DragAndDrop.SetGenericData("ImportQueueItem", item);
                DragAndDrop.StartDrag(item.PackageName);
                Event.current.Use();
            }

            // ボタンの矩形を確保
            var buttonContent = new GUIContent(LocalizationService.Instance.GetString("import"));
            var buttonRect = GUILayoutUtility.GetRect(buttonContent, GUIStyleManager.Button, GUILayout.Width(buttonWidth));

            // 右クリックメニューの表示
            if (Event.current.type == EventType.ContextClick && buttonRect.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent(LocalizationService.Instance.GetString("import_under_category")), false, () => 
                {
                    DebugLogger.Log($"Context Menu: Import under category selected for {package}");
                    UnityPackageServices.ImportPackageAndSetThumbnails(package, imagePath, category, true);
                });
                menu.AddItem(new GUIContent(LocalizationService.Instance.GetString("import_directly")), false, () => 
                {
                    DebugLogger.Log($"Context Menu: Import directly selected for {package}");
                    UnityPackageServices.ImportPackageAndSetThumbnails(package, imagePath, category, false);
                });
                
                menu.AddSeparator("");
                menu.AddItem(new GUIContent(LocalizationService.Instance.GetString("add_to_import_list") ?? "Add to Import List"), false, () => 
                {
                    DebugLogger.Log($"Context Menu: Add to import list selected for {package}");
                    ImportQueueService.Instance.Add(package, Path.GetFileName(package), imagePath, category);
                    ImportQueueWindow.ShowWindow();
                });

                menu.ShowAsContext();
                Event.current.Use();
            }

            // ラベル上での右クリックメニュー
            if (Event.current.type == EventType.ContextClick && labelRect.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent(LocalizationService.Instance.GetString("add_to_import_list") ?? "Add to Import List"), false, () => 
                {
                    DebugLogger.Log($"Label Context Menu: Add to import list selected for {package}");
                    ImportQueueService.Instance.Add(package, Path.GetFileName(package), imagePath, category);
                    ImportQueueWindow.ShowWindow();
                });
                menu.ShowAsContext();
                Event.current.Use();
            }

            // 右クリックイベントがボタンに干渉しないように制御
            // 右クリック（button 1）のイベント中はGUI.Buttonを実行せず、描画のみ行う
            bool isRightClick = Event.current.button == 1 && buttonRect.Contains(Event.current.mousePosition);
            
            if (isRightClick)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    DebugLogger.Log($"Import button clicked for {package}");
                    GUIStyleManager.Button.Draw(buttonRect, buttonContent, false, false, false, false);
                }
            }
            else
            {
                // 左クリック（通常のボタン動作）
                if (GUI.Button(buttonRect, buttonContent, GUIStyleManager.Button))
                {
                    DebugLogger.Log($"Import button clicked for {package}");   UnityPackageServices.ImportPackageAndSetThumbnails(package, imagePath, category, null);
                }
            }

            GUILayout.EndHorizontal();

            if (isMultiLine)
            {
                GUILayout.EndVertical();
            }
        }

        private readonly Dictionary<string, string[]> _cachedUnitypackages = new Dictionary<string, string[]>();

        /// <summary>
        /// UnityPackageセクションの描画
        /// </summary>
        /// <param name="itemPath">アイテムパス</param>
        /// <param name="itemName">アイテム名</param>
        /// <param name="imagePath">サムネイル画像パス</param>
        /// <param name="category">カテゴリ</param>
        private void DrawUnityPackageSection(string itemPath, string itemName, string imagePath, string category)
        {
            if (!_cachedUnitypackages.TryGetValue(itemName, out var unityPackages))
            {
                unityPackages = UnityPackageServices.FindUnityPackages(itemPath);
                _cachedUnitypackages.Add(itemName, unityPackages);
            }

            if (!unityPackages.Any()) return;

            // フォールドアウトの状態を初期化（キーが存在しない場合）
            if (!_unityPackageFoldouts.ContainsKey(itemName))
            {
                _unityPackageFoldouts[itemName] = false;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                // 行全体をクリック可能にするためのボックスを作成
                var boxRect = EditorGUILayout.GetControlRect(
                    false,
                    EditorGUIUtility.singleLineHeight
                );
                var foldoutRect = new Rect(
                    boxRect.x,
                    boxRect.y,
                    EditorGUIUtility.singleLineHeight,
                    boxRect.height
                );
                var labelRect = new Rect(
                    boxRect.x + EditorGUIUtility.singleLineHeight,
                    boxRect.y,
                    boxRect.width - EditorGUIUtility.singleLineHeight,
                    boxRect.height
                );

                // フォールドアウトの状態を更新
                if (Event.current.type == EventType.MouseDown && boxRect.Contains(Event.current.mousePosition))
                {
                    _unityPackageFoldouts[itemName] = !_unityPackageFoldouts[itemName];
                    DebugLogger.Log($"UnityPackage foldout toggled for {itemName}: {_unityPackageFoldouts[itemName]}");
                    Event.current.Use();
                }

                // フォールドアウトとラベルを描画
                _unityPackageFoldouts[itemName] = EditorGUI.Foldout(
                    foldoutRect,
                    _unityPackageFoldouts[itemName],
                    ""
                );
                EditorGUI.LabelField(labelRect, LocalizationService.Instance.GetString("unity_package"), GUIStyleManager.Label);

                if (_unityPackageFoldouts[itemName])
                {
                    EditorGUI.indentLevel++;

                    // パッケージを分類
                    var keywords = new[] { "mat", "material", "tex", "texture", "base", "source","共通" };
                    
                    var scoredPackages = unityPackages.Select(p => 
                    {
                        string fileName = Path.GetFileName(p);
                        int score = keywords.Sum(k => 
                        {
                            int count = 0;
                            int i = 0;
                            while ((i = fileName.IndexOf(k, i, StringComparison.OrdinalIgnoreCase)) != -1)
                            {
                                i += k.Length;
                                count++;
                            }
                            return count;
                        });
                        return new { Package = p, Score = score };
                    }).ToList();

                    int maxScore = scoredPackages.Any() ? scoredPackages.Max(x => x.Score) : 0;

                    List<string> materialPackages;
                    List<string> otherPackages;

                    if (maxScore > 0)
                    {
                        materialPackages = scoredPackages
                            .Where(x => x.Score == maxScore)
                            .Select(x => x.Package)
                            .ToList();
                        
                        otherPackages = scoredPackages
                            .Where(x => x.Score < maxScore)
                            .Select(x => x.Package)
                            .ToList();
                    }
                    else
                    {
                        materialPackages = new List<string>();
                        otherPackages = unityPackages.ToList();
                    }

                    // unityPackages > 2 かつ materialPackages > otherPackages の場合は全てotherPackagesとして処理
                    if (unityPackages.Count() > 2 && materialPackages.Count > otherPackages.Count)
                    {
                        materialPackages.Clear();
                        otherPackages = unityPackages.ToList();
                    }

                    int lineIndex = 0;

                    // マテリアルパッケージの描画
                    if (materialPackages.Any())
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField(LocalizationService.Instance.GetString("material"), EditorStyles.miniBoldLabel);

                        for (int i = 0; i < materialPackages.Count; i++)
                        {
                            DrawUnityPackageItem(materialPackages[i], imagePath, category);

                            if (i < materialPackages.Count - 1)
                            {
                                var lineRect = EditorGUILayout.GetControlRect(false, 1);
                                Color lineColor = LineColors[lineIndex % LineColors.Length];
                                EditorGUI.DrawRect(lineRect, lineColor);
                                lineIndex++;
                            }
                        }
                        EditorGUILayout.EndVertical();

                        if (otherPackages.Any())
                        {
                            EditorGUILayout.Space(2);
                        }
                    }

                    // その他のパッケージの描画
                    for (int i = 0; i < otherPackages.Count; i++)
                    {
                        DrawUnityPackageItem(otherPackages[i], imagePath, category);

                        if (i < otherPackages.Count - 1)
                        {
                            var lineRect = EditorGUILayout.GetControlRect(false, 1);
                            Color lineColor = LineColors[lineIndex % LineColors.Length];
                            EditorGUI.DrawRect(lineRect, lineColor);
                            lineIndex++;
                        }
                    }

                    EditorGUI.indentLevel--;
                }

                // 次のアイテムとの間に余白を追加
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// Unitypackageのキャッシュをリセットします。
        /// </summary>
        public void ResetUnitypackageCache()
            => _cachedUnitypackages.Clear();
    }
}
