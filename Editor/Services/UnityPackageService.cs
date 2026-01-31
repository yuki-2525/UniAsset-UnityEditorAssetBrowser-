// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditorAssetBrowser.Helper;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// UnityPackage操作を支援するサービスクラス
    /// UnityPackageファイルの検索、読み込み、書き込みなどの機能を提供する
    /// </summary>
    public static class UnityPackageServices
    {
        private const string PREFS_KEY_IMPORT_TO_CATEGORY_FOLDER = "UnityEditorAssetBrowser_ImportToCategoryFolder";
        private const string PREFS_KEY_GENERATE_FOLDER_THUMBNAIL = "UnityEditorAssetBrowser_GenerateFolderThumbnail";
        private const string PREFS_KEY_SHOW_IMPORT_DIALOG = "UnityEditorAssetBrowser_ShowImportDialog";
        private const string PREFS_KEY_CATEGORY_FOLDER_NAME_PREFIX = "UnityEditorAssetBrowser_CategoryFolderName_";

        /// <summary>
        /// 指定されたディレクトリ内のUnityPackageファイルを検索する
        /// サブディレクトリも再帰的に検索する
        /// </summary>
        /// <param name="directory">検索対象のディレクトリパス</param>
        /// <returns>見つかったUnityPackageファイルのパス配列。ディレクトリが存在しない場合は空の配列を返す</returns>
        public static string[] FindUnityPackages(string directory)
        {
            DebugLogger.Log($"Finding UnityPackages in: {directory}");
            if (directory == null)
            {
                DebugLogger.LogError(LocalizationService.Instance.GetString("error_directory_null"));
                return Array.Empty<string>();
            }

            if (string.IsNullOrEmpty(directory))
            {
                DebugLogger.LogError(LocalizationService.Instance.GetString("error_directory_empty"));
                return Array.Empty<string>();
            }

            if (!Directory.Exists(directory))
            {
                // ディレクトリが存在しない場合はエラーとせず空の配列を返す（UI側で警告表示するため）
                return Array.Empty<string>();
            }

            try
            {
                var files = Directory.GetFiles(directory, "*.unitypackage", SearchOption.AllDirectories);
                DebugLogger.Log($"Found {files.Length} packages.");
                return files;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is PathTooLongException)
            {
                DebugLogger.LogError(string.Format(LocalizationService.Instance.GetString("error_search_unitypackage"), ex.Message));
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// UnityPackageをインポートし、フォルダのサムネイルを設定する
        /// </summary>
        /// <param name="packagePath">パッケージパス</param>
        /// <param name="imagePath">サムネイル画像パス</param>
        /// <param name="category">カテゴリ</param>
        /// <param name="forceImportToCategoryFolder">カテゴリフォルダへのインポートを強制するかどうか（nullの場合は設定に従う）</param>
        /// <param name="showDialog">インポートダイアログを表示するかどうか（nullの場合は設定に従う）</param>
        /// <param name="onPreImportError">インポート開始前のエラー通知コールバック</param>
        public static async void ImportPackageAndSetThumbnails(
            string packagePath, 
            string imagePath, 
            string category, 
            bool? forceImportToCategoryFolder = null,
            bool? showDialog = null,
            Action<string>? onPreImportError = null)
        {
            bool generateThumbnail = EditorPrefs.GetBool(PREFS_KEY_GENERATE_FOLDER_THUMBNAIL, true);
            var beforeFolders = generateThumbnail ? GetAssetFolders() : new List<string>();

            string processedImagePath = imagePath;
            string? tempImagePath = null;

            DebugLogger.Log($"ImportPackageAndSetThumbnails: {packagePath}, GenerateThumbnail: {generateThumbnail}");

            try
            {
                if (generateThumbnail && IsUrl(imagePath))
                {
                    try
                    {
                        DebugLogger.Log($"Downloading thumbnail from URL: {imagePath}");
                        processedImagePath = await DownloadAndResizeImageAsync(imagePath);
                        tempImagePath = processedImagePath;
                        DebugLogger.Log($"Downloaded to: {tempImagePath}");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogWarning($"Failed to process thumbnail from URL: {ex.Message}");
                    }
                }

                bool importToCategoryFolder = forceImportToCategoryFolder ?? EditorPrefs.GetBool(PREFS_KEY_IMPORT_TO_CATEGORY_FOLDER, false);
                string pathToImport = packagePath;
                bool isModified = false;

                if (importToCategoryFolder && !string.IsNullOrEmpty(category))
                {
                    try
                    {
                        DebugLogger.Log($"Modifying package structure for category: {category}");
                        // クリーンアップ（前回のゴミがあれば）
                        UnityPackageModifier.Cleanup();

                        // カテゴリフォルダ名の取得（設定があればそれを使用、なければカテゴリ名をそのまま使用）
                        string folderNameKey = PREFS_KEY_CATEGORY_FOLDER_NAME_PREFIX + category;
                        string targetFolderName = EditorPrefs.GetString(folderNameKey, category);

                        EditorUtility.DisplayProgressBar("Preparing Package", "Modifying package structure...", 0.5f);
                        pathToImport = await UnityPackageModifier.CreateModifiedPackageAsync(packagePath, targetFolderName);
                        isModified = true;
                        DebugLogger.Log($"Modified package created at: {pathToImport}");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogError(string.Format(LocalizationService.Instance.GetString("error_modify_package"), ex.Message));
                        pathToImport = packagePath;
                        isModified = false;
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
                
                // サムネイル生成もパッケージ変更も不要なら、単純にインポートして終了
                if (!generateThumbnail && !isModified)
                {
                    bool dialog = showDialog ?? EditorPrefs.GetBool(PREFS_KEY_SHOW_IMPORT_DIALOG, true);
                    AssetDatabase.ImportPackage(pathToImport, dialog);
                    return;
                }

                {
                    bool dialog = showDialog ?? EditorPrefs.GetBool(PREFS_KEY_SHOW_IMPORT_DIALOG, true);
                    AssetDatabase.ImportPackage(pathToImport, dialog);
                }

                // イベントハンドラの解除ヘルパー
                void UnregisterHandlers()
                {
                    if (_importCompletedHandler != null)
                    {
                        AssetDatabase.importPackageCompleted -= _importCompletedHandler;
                        _importCompletedHandler = null;
                    }
                    if (_importCancelledHandler != null)
                    {
                        AssetDatabase.importPackageCancelled -= _importCancelledHandler;
                        _importCancelledHandler = null;
                    }
                    if (_importFailedHandler != null)
                    {
                        AssetDatabase.importPackageFailed -= _importFailedHandler;
                        _importFailedHandler = null;
                    }
                }

                UnregisterHandlers();

                // 一時ファイル削除ヘルパー
                void DeleteTempPackage()
                {
                    if (isModified && pathToImport != packagePath && File.Exists(pathToImport))
                    {
                        try 
                        { 
                            File.Delete(pathToImport);
                        }
                        catch (Exception ex) 
                        { 
                            DebugLogger.LogWarning(string.Format(LocalizationService.Instance.GetString("warning_delete_temp_package_failed"), pathToImport, ex.Message));
                        }
                    }

                    if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
                    {
                        try
                        {
                            File.Delete(tempImagePath);
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.LogWarning($"Failed to delete temp thumbnail: {ex.Message}");
                        }
                    }
                }

                _importCompletedHandler = packageName =>
                {
                    try
                    {
                        if (generateThumbnail)
                        {
                            // アセットデータベースを更新
                            AssetDatabase.Refresh();

                            // インポート後のフォルダ一覧を取得
                            var afterFolders = GetAssetFolders();

                            // 新しく追加されたフォルダを特定
                            var newFolders = afterFolders.Except(beforeFolders).ToList();

                            // サムネイルの設定
                            if (newFolders.Any())
                            {
                                SetFolderThumbnails(newFolders, processedImagePath);
                            }
                            else
                            {
                                DebugLogger.LogWarning(LocalizationService.Instance.GetString("warning_new_folder_not_found"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogError(string.Format(LocalizationService.Instance.GetString("error_post_import_failed"), ex.Message));
                    }
                    finally
                    {
                        DeleteTempPackage();
                        UnregisterHandlers();
                    }
                };

                _importCancelledHandler = packageName =>
                {
                    DeleteTempPackage();
                    UnregisterHandlers();
                };

                _importFailedHandler = (packageName, error) =>
                {
                    DeleteTempPackage();
                    UnregisterHandlers();
                    DebugLogger.LogError(string.Format(LocalizationService.Instance.GetString("error_import_failed"), error));
                };

                AssetDatabase.importPackageCompleted += _importCompletedHandler;
                AssetDatabase.importPackageCancelled += _importCancelledHandler;
                AssetDatabase.importPackageFailed += _importFailedHandler;
            }
            catch (Exception ex)
            {
                string errorMessage = string.Format(LocalizationService.Instance.GetString("error_package_import_failed"), ex.Message);
                DebugLogger.LogError(errorMessage);
                onPreImportError?.Invoke(errorMessage);
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
        /// <param name="direct">指定されたフォルダに直接設定するかどうか</param>
        public static async void SetFolderThumbnails(List<string> folders, string imagePath, bool direct = false)
        {
            if (!ValidateInputParameters(folders, imagePath))
                return;

            string fullImagePath = imagePath;
            string? tempImagePath = null;

            // URLの場合はダウンロードして一時ファイルを作成
            if (IsUrl(imagePath))
            {
                try
                {
                    fullImagePath = await DownloadAndResizeImageAsync(imagePath);
                    tempImagePath = fullImagePath;
                }
                catch (Exception ex)
                {
                    DebugLogger.LogWarning($"Failed to process thumbnail from URL: {ex.Message}");
                    return;
                }
            }
            else
            {
                fullImagePath = GetValidatedImagePath(imagePath);
            }

            if (string.IsNullOrEmpty(fullImagePath))
                return;

            try
            {
                HashSet<string> targetFolders;
                if (direct)
                {
                    targetFolders = new HashSet<string>(folders);
                }
                else
                {
                    targetFolders = DetermineTargetFolders(folders);
                }

                if (!targetFolders.Any())
                {
                    DebugLogger.LogWarning(LocalizationService.Instance.GetString("warning_target_folder_not_found"));
                    return;
                }

                CopyThumbnailsToTargetFolders(targetFolders, fullImagePath);
            }
            finally
            {
                // 一時ファイルの削除
                if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
                {
                    try
                    {
                        File.Delete(tempImagePath);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogWarning($"Failed to delete temp thumbnail: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 入力パラメータを検証
        /// </summary>
        private static bool ValidateInputParameters(List<string> folders, string imagePath)
        {
            if (folders == null || !folders.Any())
            {
                DebugLogger.LogWarning(LocalizationService.Instance.GetString("warning_folder_not_specified"));
                return false;
            }

            if (string.IsNullOrEmpty(imagePath))
            {
                DebugLogger.LogWarning(LocalizationService.Instance.GetString("warning_thumbnail_path_not_specified"));
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
                DebugLogger.LogWarning(LocalizationService.Instance.GetString("warning_full_image_path_failed"));
                return string.Empty;
            }

            if (!File.Exists(imagePath))
            {
                DebugLogger.LogWarning(string.Format(LocalizationService.Instance.GetString("warning_thumbnail_not_found"), imagePath));
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
            if (!excludedFolders.Any()) return;

            // まず全体の共通親を探す
            string commonParent = GetDeepestCommonParent(excludedFolders);

            // 共通親が適切（Assets直下やカテゴリ直下でない＝特定のアイテムフォルダ内）なら、
            // 従来通り最も浅いものの親を採用
            if (!IsRootOrCategoryRoot(commonParent))
            {
                var shallowest = excludedFolders.OrderBy(f => f.Count(c => c == '/')).First();
                var parts = shallowest.Split('/');
                if (parts.Length > 1)
                {
                    string parent = string.Join("/", parts.Take(parts.Length - 1));
                    if (!string.IsNullOrEmpty(parent) && !IsRootFolderIcon(parent))
                        targetFolders.Add(parent);
                }
                return;
            }

            // 共通親が不適切な場合（複数アイテムの可能性がある場合）、グルーピングして処理
            // 例: Assets/Category/ItemA/Editor, Assets/Category/ItemB/Editor
            var groups = excludedFolders.GroupBy(f =>
            {
                if (f.Length <= commonParent.Length) return f;
                
                string relative = f.Substring(commonParent.Length).TrimStart('/');
                int slashIndex = relative.IndexOf('/');
                if (slashIndex == -1) return f;
                
                string firstPart = relative.Substring(0, slashIndex);
                return Path.Combine(commonParent, firstPart).Replace('\\', '/');
            });

            foreach (var group in groups)
            {
                // 各グループ内で最も浅い除外フォルダを探す
                var shallowestInGroup = group.OrderBy(f => f.Count(c => c == '/')).First();
                var parts = shallowestInGroup.Split('/');
                if (parts.Length > 1)
                {
                    string parent = string.Join("/", parts.Take(parts.Length - 1));
                    
                    // 親フォルダがルートやカテゴリルートそのものでない場合のみ追加
                    // (ItemA/Editor -> ItemA はOK。 Category/Editor -> Category はNG)
                    if (!string.IsNullOrEmpty(parent) && !IsRootFolderIcon(parent) && !IsRootOrCategoryRoot(parent))
                    {
                        targetFolders.Add(parent);
                    }
                }
            }
        }

        /// <summary>
        /// 通常フォルダの処理
        /// </summary>
        private static void ProcessNormalFolders(List<string> folders, HashSet<string> targetFolders)
        {
            if (!folders.Any()) return;

            // まず全体の共通親を探す
            string commonParent = GetDeepestCommonParent(folders);
            
            // 共通親が適切（Assets直下やカテゴリ直下でない）なら、それを採用
            if (!IsRootOrCategoryRoot(commonParent))
            {
                string bestFolder = FindBestThumbnailFolder(commonParent);
                if (!string.IsNullOrEmpty(bestFolder) && !IsRootFolderIcon(bestFolder))
                {
                    targetFolders.Add(bestFolder);
                }
                return;
            }

            // 共通親が不適切な場合、フォルダリストをグルーピングして、それぞれのグループごとに処理を行う
            // 例: Assets/Category/ItemA/..., Assets/Category/ItemB/... 
            // -> ItemAグループとItemBグループに分けて、それぞれでサムネイル設定先を探す
            
            // 共通親の直下のフォルダでグルーピング
            var groups = folders.GroupBy(f =>
            {
                // commonParentより1階層深い部分を取得
                // commonParent = "Assets/Category"
                // f = "Assets/Category/ItemA/File"
                // -> "Assets/Category/ItemA" をキーにする
                
                if (f.Length <= commonParent.Length) return f; // ありえないはずだが念のため
                
                string relative = f.Substring(commonParent.Length).TrimStart('/');
                int slashIndex = relative.IndexOf('/');
                if (slashIndex == -1) return f; // ファイルパスそのものか、直下のフォルダ
                
                string firstPart = relative.Substring(0, slashIndex);
                return Path.Combine(commonParent, firstPart).Replace('\\', '/');
            });

            foreach (var group in groups)
            {
                // 各グループ（ItemA, ItemB...）に対して再帰的に処理を行うか、
                // あるいはそのグループのキー（ItemAフォルダ）を起点にFindBestThumbnailFolderを呼ぶ
                
                string groupKey = group.Key;
                
                // グループキー自体が除外フォルダならスキップ
                if (ExcludeFolderService.IsExcludedFolder(Path.GetFileName(groupKey))) continue;

                // グループ内のパスを使って、そのグループ内での最適なフォルダを探す
                // ここではシンプルにグループキー（ItemAなど）を起点にする
                string bestFolder = FindBestThumbnailFolder(groupKey);
                if (!string.IsNullOrEmpty(bestFolder) && !IsRootFolderIcon(bestFolder))
                {
                    targetFolders.Add(bestFolder);
                }
            }
        }

        /// <summary>
        /// 指定されたパスがルートフォルダまたはカテゴリルートフォルダかどうかを判定する
        /// </summary>
        /// <param name="path">判定するパス</param>
        /// <returns>ルートまたはカテゴリルートの場合はtrue</returns>
        private static bool IsRootOrCategoryRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path == "Assets") return true;

            bool importToCategoryFolder = EditorPrefs.GetBool(PREFS_KEY_IMPORT_TO_CATEGORY_FOLDER, false);
            if (importToCategoryFolder)
            {
                var parts = path.Split('/');
                if (parts.Length == 2 && parts[0] == "Assets")
                {
                    return true;
                }
            }
            return false;
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
                    DebugLogger.LogWarning(string.Format(LocalizationService.Instance.GetString("warning_thumbnail_copy_failed"), folder, ex.Message));
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
        private static AssetDatabase.ImportPackageCallback? _importCancelledHandler;
        private static AssetDatabase.ImportPackageFailedCallback? _importFailedHandler;

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

        /// <summary>
        /// 複数のパスに共通する最も深い親ディレクトリを取得する
        /// </summary>
        /// <param name="paths">パスのリスト</param>
        /// <returns>共通の親ディレクトリパス。共通部分がない場合は空文字列</returns>
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

        /// <summary>
        /// 指定されたパスがURLかどうかを判定する
        /// </summary>
        /// <param name="path">判定するパス</param>
        /// <returns>http:// または https:// で始まる場合はtrue</returns>
        private static bool IsUrl(string path)
        {
            return !string.IsNullOrEmpty(path) && (path.StartsWith("http://") || path.StartsWith("https://"));
        }

        /// <summary>
        /// URLから画像をダウンロードし、適切なサイズにリサイズして一時ファイルとして保存する
        /// </summary>
        /// <param name="url">画像のURL</param>
        /// <returns>保存された一時ファイルのパス</returns>
        private static async Task<string> DownloadAndResizeImageAsync(string url)
        {
            using (var uwr = UnityWebRequestTexture.GetTexture(url))
            {
                var operation = uwr.SendWebRequest();
                while (!operation.isDone)
                    await Task.Delay(10);

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Failed to download image: {uwr.error}");
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                if (texture == null) throw new Exception("Failed to get texture content");

                try
                {
                    // 必要に応じてリサイズ（幅300px基準）
                    int targetWidth = 300;
                    int targetHeight = texture.height;
                    
                    if (texture.width > targetWidth)
                    {
                        float scale = (float)targetWidth / texture.width;
                        targetHeight = Mathf.RoundToInt(texture.height * scale);
                    }
                    else
                    {
                        targetWidth = texture.width;
                    }

                    RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0);
                    RenderTexture.active = rt;
                    Graphics.Blit(texture, rt);
                    
                    Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
                    result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                    result.Apply();
                    
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(rt);

                    byte[] bytes = result.EncodeToJPG(75);
                    string tempPath = Path.Combine(Application.temporaryCachePath, "temp_thumbnail_" + Guid.NewGuid() + ".jpg");
                    File.WriteAllBytes(tempPath, bytes);
                    
                    // 生成したテクスチャを解放
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(result);
                    else
                        UnityEngine.Object.DestroyImmediate(result);

                    return tempPath;
                }
                finally
                {
                    // 元のテクスチャを解放
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(texture);
                    else
                        UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        [Serializable]
        private class ExcludeFoldersData
        {
            public List<string> folders = new List<string>();
        }
    }
}
