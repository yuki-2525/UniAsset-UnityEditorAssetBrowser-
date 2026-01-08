// Copyright (c) 2025-2026 sakurayuki

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorAssetBrowser.Services;
using UnityEngine;
using UnityEditorAssetBrowser.Helper;

namespace UnityEditorAssetBrowser
{
    /// <summary>
    /// フォルダ内の "FolderIcon.jpg" をプロジェクトウィンドウでのフォルダサムネイルとして描画するクラス
    /// </summary>
    [InitializeOnLoad]
    public static class FolderIconDrawer
    {
        // EditorPrefsのキー：フォルダアイコン表示設定
        private const string PREFS_KEY_SHOW_FOLDER_THUMBNAIL = "UnityEditorAssetBrowser_ShowFolderThumbnail";
        private const string PREFS_KEY_EXCLUDE_FOLDERS = "UnityEditorAssetBrowser_ExcludeFolders";

        // 現在イベントが登録されているかどうかのフラグ
        private static bool _isRegistered = false;

        // キャッシュシステム
        private static readonly Dictionary<string, FolderIconCache> _folderCache = new Dictionary<string, FolderIconCache>();
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, bool> _directoryCache = new Dictionary<string, bool>();
        private const int MAX_CACHE_SIZE = 1000;
        private static int _cacheAccessCounter = 0;

        private struct FolderIconCache
        {
            public List<string> IconPaths;
            public long LastWriteTime;
            public bool IsValid;
        }

        // 静的コンストラクタ：エディタ起動時に呼ばれる
        static FolderIconDrawer()
        {
            // 起動時にEditorPrefsの値で初期化
            SetEnabled(EditorPrefs.GetBool(PREFS_KEY_SHOW_FOLDER_THUMBNAIL, true));
            
            // キャッシュクリーンアップのためのイベント登録
            EditorApplication.update += CleanupCacheIfNeeded;
        }

        /// <summary>
        /// フォルダアイコン描画の有効/無効を切り替える
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            if (enabled && !_isRegistered)
            {
                Helper.DebugLogger.Log("FolderIconDrawer enabled.");
                // 有効化：イベント登録
                EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
                EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
                _isRegistered = true;
            }
            else if (!enabled && _isRegistered)
            {
                Helper.DebugLogger.Log("FolderIconDrawer disabled.");
                // 無効化：イベント解除
                EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
                _isRegistered = false;
            }
        }

        /// <summary>
        /// プロジェクトウィンドウの各アイテム描画コールバック
        /// </summary>
        private static void OnProjectWindowItemGUI(string guid, Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            // ディレクトリチェック（キャッシュ使用）
            if (!IsDirectoryCached(path)) return;

            // 除外フォルダ名判定
            string folderName = Path.GetFileName(path);
            if (ExcludeFolderService.IsExcludedFolder(folderName)) return;

            // キャッシュからアイコンパスを取得
            var iconPaths = GetIconPathsCached(path);
            if (iconPaths == null || iconPaths.Count == 0) return;

            // テクスチャをキャッシュから取得
            var textures = GetTexturesCached(iconPaths);
            if (textures.Count == 0) return;

            // アイコン領域に合わせて描画矩形を計算
            Rect imageRect;
            if (rect.height > 20)
            {
                // 一覧ビュー・大きいサムネイルビュー
                imageRect = new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.width + 2);
            }
            else if (rect.x > 20)
            {
                // 詳細ビュー（リスト形式）
                imageRect = new Rect(rect.x - 1, rect.y - 1, rect.height + 2, rect.height + 2);
            }
            else
            {
                // プロジェクトウィンドウ左ペイン
                imageRect = new Rect(rect.x + 2, rect.y - 1, rect.height + 2, rect.height + 2);
            }

            // imageRectの幅・高さが奇数なら+1して偶数に揃える
            float evenWidth = imageRect.width % 2 == 0 ? imageRect.width : imageRect.width + 1;
            float evenHeight = imageRect.height % 2 == 0 ? imageRect.height : imageRect.height + 1;

            if (textures.Count == 1)
            {
                // 1枚だけなら全体に表示
                GUI.DrawTexture(imageRect, textures[0]);
            }
            else
            {
                // 2～4枚なら4分割して表示、足りない領域は白
                int halfW = Mathf.FloorToInt(evenWidth / 2f);
                int halfH = Mathf.FloorToInt(evenHeight / 2f);
                int rightW = Mathf.RoundToInt(evenWidth) - halfW;
                int bottomH = Mathf.RoundToInt(evenHeight) - halfH;
                Rect[] subRects = new Rect[]
                {
                    new Rect(imageRect.x, imageRect.y, halfW, halfH), // 左上
                    new Rect(imageRect.x + halfW, imageRect.y, rightW, halfH), // 右上
                    new Rect(imageRect.x, imageRect.y + halfH, halfW, bottomH), // 左下
                    new Rect(imageRect.x + halfW, imageRect.y + halfH, rightW, bottomH), // 右下
                };
                for (int i = 0; i < 4; i++)
                {
                    if (i < textures.Count)
                    {
                        GUI.DrawTexture(subRects[i], textures[i]);
                    }
                    else
                    {
                        EditorGUI.DrawRect(subRects[i], Color.white);
                    }
                }
            }
        }

        // 指定フォルダ以下で除外フォルダをスキップしつつFolderIcon.jpgを最大maxCount枚まで再帰的に探索（最大depth階層）
        private static List<string> FindFolderIconsRecursive(
            string root,
            int currentDepth,
            int maxDepth,
            int maxCount
        )
        {
            var result = new List<string>();
            if (currentDepth >= maxDepth) return result;

            try
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    if (result.Count >= maxCount) break;

                    string folderName = Path.GetFileName(dir);
                    if (ExcludeFolderService.IsExcludedFolder(folderName)) continue;

                    string iconPath = Path.GetFullPath(Path.Combine(dir, "FolderIcon.jpg"));
                    if (File.Exists(iconPath)) result.Add(iconPath);

                    if (result.Count >= maxCount) break;

                    // 再帰探索
                    var found = FindFolderIconsRecursive(dir, currentDepth + 1, maxDepth, maxCount - result.Count);
                    result.AddRange(found);
                }
            }
            catch
            {
                // Ignored
            }

            return result;
        }

        /// <summary>
        /// ディレクトリかどうかをキャッシュ付きで判定
        /// </summary>
        private static bool IsDirectoryCached(string path)
        {
            if (_directoryCache.TryGetValue(path, out bool isDirectory)) return isDirectory;

            try
            {
                isDirectory = Directory.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.Directory);
                _directoryCache[path] = isDirectory;
                return isDirectory;
            }
            catch
            {
                _directoryCache[path] = false;
                return false;
            }
        }

        /// <summary>
        /// アイコンパスをキャッシュ付きで取得
        /// </summary>
        private static List<string> GetIconPathsCached(string path)
        {
            try
            {
                long currentWriteTime = Directory.GetLastWriteTime(path).ToBinary();
                
                if (_folderCache.TryGetValue(path, out var cache) && cache.IsValid && cache.LastWriteTime == currentWriteTime)
                {
                    return cache.IconPaths;
                }

                // 自フォルダ内のFolderIcon.jpgを最優先で取得
                string selfIconPath = Path.GetFullPath(Path.Combine(path, "FolderIcon.jpg"));
                List<string> iconPaths;
                
                if (File.Exists(selfIconPath))
                {
                    iconPaths = new List<string> { selfIconPath };
                }
                else
                {
                    iconPaths = FindFolderIconsRecursive(path, 0, 4, 4);
                }

                // キャッシュに保存
                _folderCache[path] = new FolderIconCache
                {
                    IconPaths = iconPaths,
                    LastWriteTime = currentWriteTime,
                    IsValid = true
                };

                return iconPaths;
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// テクスチャをキャッシュ付きで取得
        /// </summary>
        private static List<Texture2D> GetTexturesCached(List<string> iconPaths)
        {
            var textures = new List<Texture2D>();
            
            foreach (var iconPath in iconPaths)
            {
                if (_textureCache.TryGetValue(iconPath, out var cachedTexture) && cachedTexture != null)
                {
                    textures.Add(cachedTexture);
                }
                else
                {
                    // フルパス -> プロジェクト相対パス ("Assets/...") に変換
                    string assetPath = null;
                    try
                    {
                        if (string.IsNullOrEmpty(iconPath)) continue;
                        string normalized = iconPath.Replace('\\', '/');
                        string dataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
                        if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                        {
                            assetPath = "Assets" + normalized.Substring(dataPath.Length);
                        }
                        else
                        {
                            // 既に "Assets/..." の形式で渡されている場合はそのまま使う
                            if (normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                int idx = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
                                assetPath = normalized.Substring(idx + 1); // remove leading '/'
                            }
                            else if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                            {
                                assetPath = normalized;
                            }
                            else
                            {
                                // プロジェクト外パスなどはスキップ
                                assetPath = null;
                            }
                        }
                    }
                    catch
                    {
                        assetPath = null;
                    }

                    if (string.IsNullOrEmpty(assetPath)) continue;

                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (texture != null)
                    {
                        _textureCache[iconPath] = texture;
                        textures.Add(texture);
                    }
                }
            }
            
            return textures;
        }

        /// <summary>
        /// 定期的なキャッシュクリーンアップ
        /// </summary>
        private static void CleanupCacheIfNeeded()
        {
            _cacheAccessCounter++;
            
            // 1000フレームごとにクリーンアップ
            if (_cacheAccessCounter % 1000 == 0)
            {
                CleanupCache();
            }
        }

        /// <summary>
        /// キャッシュのクリーンアップ
        /// </summary>
        private static void CleanupCache()
        {
            // テクスチャキャッシュが上限を超えた場合、半分をクリア
            if (_textureCache.Count > MAX_CACHE_SIZE)
            {
                Helper.DebugLogger.Log($"Cleaning up texture cache (Count: {_textureCache.Count})");
                var keysToRemove = _textureCache.Keys.Take(_textureCache.Count / 2).ToList();
                foreach (var key in keysToRemove)
                {
                    _textureCache.Remove(key);
                }
            }

            // フォルダキャッシュも同様にクリーンアップ
            if (_folderCache.Count > MAX_CACHE_SIZE)
            {
                Helper.DebugLogger.Log($"Cleaning up folder cache (Count: {_folderCache.Count})");
                var keysToRemove = _folderCache.Keys.Take(_folderCache.Count / 2).ToList();
                foreach (var key in keysToRemove)
                {
                    _folderCache.Remove(key);
                }
            }

            // ディレクトリキャッシュも同様にクリーンアップ
            if (_directoryCache.Count > MAX_CACHE_SIZE)
            {
                Helper.DebugLogger.Log($"Cleaning up directory cache (Count: {_directoryCache.Count})");
                var keysToRemove = _directoryCache.Keys.Take(_directoryCache.Count / 2).ToList();
                foreach (var key in keysToRemove)
                {
                    _directoryCache.Remove(key);
                }
            }
        }

        [Serializable]
        private class ExcludeFoldersData
        {
            public List<string> Folders = new List<string>();
        }
    }
}
