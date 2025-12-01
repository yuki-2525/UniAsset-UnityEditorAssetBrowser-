// Copyright (c) 2025 sakurayuki

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// UnityPackage操作を支援するサービスクラス
    /// UnityPackageファイルの検索、読み込み、書き込みなどの機能を提供する
    /// </summary>
    public static class UnityPackageServices
    {
        private const string PREFS_KEY_IMPORT_TO_CATEGORY_FOLDER = "UnityEditorAssetBrowser_ImportToCategoryFolder";

        /// <summary>
        /// 指定されたディレクトリ内のUnityPackageファイルを検索する
        /// サブディレクトリも再帰的に検索する
        /// </summary>
        /// <param name="directory">検索対象のディレクトリパス</param>
        /// <returns>見つかったUnityPackageファイルのパス配列。ディレクトリが存在しない場合は空の配列を返す</returns>
        public static string[] FindUnityPackages(string directory)
        {
            if (directory == null)
            {
                Debug.LogError("ディレクトリパスがnullです");
                return Array.Empty<string>();
            }

            if (string.IsNullOrEmpty(directory))
            {
                Debug.LogError("ディレクトリパスが空です");
                return Array.Empty<string>();
            }

            if (!Directory.Exists(directory))
            {
                Debug.LogError($"ディレクトリが存在しません: {directory}");
                return Array.Empty<string>();
            }

            try
            {
                return Directory.GetFiles(directory, "*.unitypackage", SearchOption.AllDirectories);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is PathTooLongException)
            {
                Debug.LogError($"UnityPackageファイルの検索中にエラーが発生しました: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// UnityPackageをインポートし、フォルダのサムネイルを設定する
        /// </summary>
        /// <param name="packagePath">パッケージパス</param>
        /// <param name="imagePath">サムネイル画像パス</param>
        /// <param name="category">カテゴリ</param>
        public static async void ImportPackageAndSetThumbnails(string packagePath, string imagePath, string category)
        {
            var beforeFolders = GetAssetFolders();

            try
            {
                bool importToCategoryFolder = EditorPrefs.GetBool(PREFS_KEY_IMPORT_TO_CATEGORY_FOLDER, false);
                string pathToImport = packagePath;
                bool isModified = false;

                if (importToCategoryFolder && !string.IsNullOrEmpty(category))
                {
                    try
                    {
                        EditorUtility.DisplayProgressBar("Preparing Package", "Modifying package structure...", 0.5f);
                        pathToImport = await UnityPackageModifier.CreateModifiedPackageAsync(packagePath, category);
                        isModified = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UnityPackageService] Failed to modify package: {ex.Message}");
                        pathToImport = packagePath;
                        isModified = false;
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
                
                AssetDatabase.ImportPackage(pathToImport, true);

                // インポート完了後の処理を設定
                if (_importCompletedHandler != null)
                {
                    AssetDatabase.importPackageCompleted -= _importCompletedHandler;
                }

                _importCompletedHandler = packageName =>
                {
                    try
                    {
                        if (isModified && pathToImport != packagePath && File.Exists(pathToImport))
                        {
                            try { File.Delete(pathToImport); } catch { }
                        }

                        // アセットデータベースを更新
                        AssetDatabase.Refresh();

                        // インポート後のフォルダ一覧を取得
                        var afterFolders = GetAssetFolders();

                        // 新しく追加されたフォルダを特定
                        var newFolders = afterFolders.Except(beforeFolders).ToList();

                        // サムネイルの設定
                        if (newFolders.Any())
                        {
                            SetFolderThumbnails(newFolders, imagePath);
                        }
                        else
                        {
                            Debug.LogWarning("[UnityPackageService] 新規フォルダが見つかりませんでした");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UnityPackageService] インポート後の処理に失敗しました: {ex.Message}");
                    }
                    finally
                    {
                        // ハンドラを使い捨てにする
                        if (_importCompletedHandler != null)
                        {
                            AssetDatabase.importPackageCompleted -= _importCompletedHandler;
                            _importCompletedHandler = null;
                        }
                    }
                };

                AssetDatabase.importPackageCompleted += _importCompletedHandler;
            }
            catch (Exception ex)
            {
                Debug.LogError($"パッケージのインポートに失敗しました: {ex.Message}");
            }
        }



        /// <summary>
        /// Assetsフォルダ内のフォルダ一覧を取得
        /// </summary>
        /// <returns>フォルダパスのリスト</returns>
        private static List<string> GetAssetFolders()
        {
            var folders = new List<string>();
            // AssetDatabaseを使用してフォルダを取得
            string[] guids = AssetDatabase.FindAssets("t:Folder", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    folders.Add(path);
                }
            }
            return folders;
        }

        /// <summary>
        /// フォルダのサムネイルを設定する
        /// </summary>
        /// <param name="folders">フォルダパスのリスト</param>
        /// <param name="imagePath">サムネイル画像パス</param>
        private static void SetFolderThumbnails(List<string> folders, string imagePath)
        {
            if (!ValidateInputParameters(folders, imagePath))
                return;

            string fullImagePath = GetValidatedImagePath(imagePath);
            if (string.IsNullOrEmpty(fullImagePath))
                return;

            var targetFolders = DetermineTargetFolders(folders);
            if (!targetFolders.Any())
            {
                Debug.LogWarning("[UnityPackageService] 対象フォルダが見つかりませんでした");
                return;
            }

            CopyThumbnailsToTargetFolders(targetFolders, fullImagePath);
        }

        /// <summary>
        /// 入力パラメータを検証
        /// </summary>
        private static bool ValidateInputParameters(List<string> folders, string imagePath)
        {
            if (folders == null || !folders.Any())
            {
                Debug.LogWarning("[UnityPackageService] フォルダが指定されていません");
                return false;
            }

            if (string.IsNullOrEmpty(imagePath))
            {
                Debug.LogWarning("[UnityPackageService] サムネイル画像パスが指定されていません");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 検証済みの画像パスを取得
        /// </summary>
        private static string GetValidatedImagePath(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
            {
                Debug.LogWarning("[UnityPackageService] 完全な画像パスを取得できませんでした");
                return string.Empty;
            }

            if (!File.Exists(imagePath))
            {
                Debug.LogWarning($"[UnityPackageService] サムネイル画像が見つかりません: {imagePath}");
                return string.Empty;
            }

            return imagePath;
        }

        /// <summary>
        /// サムネイル保存対象フォルダを決定
        /// </summary>
        private static HashSet<string> DetermineTargetFolders(List<string> folders)
        {
            var targetFolders = new HashSet<string>();
            var excludedFolders = GetExcludedFolders(folders);

            if (excludedFolders.Any())
            {
                ProcessExcludedFolders(excludedFolders, targetFolders);
            }
            else
            {
                ProcessNormalFolders(folders, targetFolders);
            }

            return targetFolders;
        }

        /// <summary>
        /// 除外フォルダのリストを取得
        /// </summary>
        private static List<string> GetExcludedFolders(List<string> folders)
        {
            return folders
                .Where(f => ExcludeFolderService.IsExcludedFolder(f.Split('/').Last()))
                .ToList();
        }

        /// <summary>
        /// 除外フォルダが含まれる場合の処理
        /// </summary>
        private static void ProcessExcludedFolders(List<string> excludedFolders, HashSet<string> targetFolders)
        {
            var shallowest = excludedFolders.OrderBy(f => f.Count(c => c == '/')).First();
            var parts = shallowest.Split('/');
            if (parts.Length > 1)
            {
                string parent = string.Join("/", parts.Take(parts.Length - 1));
                if (!string.IsNullOrEmpty(parent) && !IsRootFolderIcon(parent))
                    targetFolders.Add(parent);
            }
        }

        /// <summary>
        /// 通常フォルダの処理
        /// </summary>
        private static void ProcessNormalFolders(List<string> folders, HashSet<string> targetFolders)
        {
            if (!folders.Any()) return;

            string commonParent = GetDeepestCommonParent(folders);
            if (!string.IsNullOrEmpty(commonParent))
            {
                string bestFolder = FindBestThumbnailFolder(commonParent);
                if (!string.IsNullOrEmpty(bestFolder) && !IsRootFolderIcon(bestFolder))
                {
                    targetFolders.Add(bestFolder);
                }
            }
        }

        /// <summary>
        /// 対象フォルダにサムネイルをコピー
        /// </summary>
        private static void CopyThumbnailsToTargetFolders(HashSet<string> targetFolders, string fullImagePath)
        {
            foreach (string folder in targetFolders)
            {
                try
                {
                    string targetPath = Path.Combine(folder, "FolderIcon.jpg");
                    File.Copy(fullImagePath, targetPath, true);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UnityPackageService] サムネイル画像のコピーに失敗しました: {folder} - {ex.Message}");
                }
            }

            // アセットデータベースを更新して表示を更新
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 最適なサムネイル保存先を決定する
        /// </summary>
        private static string FindBestThumbnailFolder(string folder)
        {
            string[] parts = folder.Split('/');
            
            // 除外フォルダがパスに含まれる場合は、最初の除外フォルダの1つ上を返す
            for (int i = 1; i < parts.Length; i++)
            {
                if (ExcludeFolderService.IsExcludedFolder(parts[i]))
                {
                    return string.Join("/", parts.Take(i));
                }
            }
            // 再帰的に最適な深さを探す
            string current = folder;
            while (true)
            {
                var dirs = Directory.GetDirectories(current).ToList();
                var files = Directory
                    .GetFiles(current)
                    .Where(f => Path.GetExtension(f) != ".meta")
                    .ToList();
                // フォルダが1つだけ、かつ除外フォルダでなく、ファイルが無い場合はさらに深く
                if (
                    dirs.Count == 1
                    && !ExcludeFolderService.IsExcludedFolder(Path.GetFileName(dirs[0]))
                    && files.Count == 0
                )
                {
                    current = dirs[0];
                    continue;
                }
                break;
            }
            return current;
        }

        // importPackageCompleted 用の一時ハンドラ
        private static AssetDatabase.ImportPackageCallback? _importCompletedHandler;

        /// <summary>
        /// 指定したパスがAssets直下のFolderIcon.jpgか判定する
        /// 例: Assets/FolderIcon.jpg → true, Assets/Folder1/FolderIcon.jpg → false
        /// </summary>
        /// <param name="folderPath">判定するフォルダパス</param>
        /// <returns>Assets直下のFolderIcon.jpgならtrue</returns>
        private static bool IsRootFolderIcon(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return false;

            var parts = folderPath.Split('/');
            return parts.Length == 2 && parts[0] == "Assets" && parts[1] == "FolderIcon.jpg";
        }

        // 複数パスの最も深い共通の親ディレクトリを求める
        private static string GetDeepestCommonParent(IEnumerable<string> paths)
        {
            if (paths == null || !paths.Any()) return string.Empty;

            var splitPaths = paths.Select(p => p.Split('/')).ToList();
            int minLen = splitPaths.Min(arr => arr.Length);

            List<string> common = new List<string>();
            for (int i = 0; i < minLen; i++)
            {
                string part = splitPaths[0][i];
                if (splitPaths.All(arr => arr[i] == part))
                {
                    common.Add(part);
                }
                else
                {
                    break;
                }
            }

            return common.Count > 0 ? string.Join("/", common) : string.Empty;
        }

        [Serializable]
        private class ExcludeFoldersData
        {
            public List<string> folders = new List<string>();
        }
    }
}
