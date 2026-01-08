// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditorAssetBrowser.Helper;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// バージョンアップデート検出サービス
    /// リモートからバージョン情報を取得し、現在のバージョンと比較して更新通知を提供する
    /// </summary>
    public static class VersionUpdateService
    {
        // 定数定義
        /// <summary>最後にチェックした日付のEditorPrefsキー</summary>
        private const string LAST_CHECK_DATE_KEY = "UnityEditorAssetBrowser_LastVersionCheckDate";

        /// <summary>バージョン通知を無視するかどうかのEditorPrefsキー</summary>
        private const string IGNORE_VERSION_KEY = "UnityEditorAssetBrowser_IgnoreVersion";

        /// <summary>バージョンチェック間隔（時間）</summary>
        private const int VERSION_CHECK_INTERVAL_HOURS = 24;

        /// <summary>HTTPリクエストタイムアウト（秒）</summary>
        private const int HTTP_TIMEOUT_SECONDS = 30;

        /// <summary>HTTPリダイレクト制限回数</summary>
        private const int HTTP_REDIRECT_LIMIT = 10;

        /// <summary>フォールバックバージョン</summary>
        private const string FALLBACK_VERSION = "1.0.0";

        /// <summary>
        /// リモートバージョン情報の構造体
        /// </summary>
        [Serializable]
        private class RemoteVersionInfo
        {
            public string name = "";
            public string version = "";
            public string displayName = "";
            public string description = "";
            public string url = "";
            public string changelogUrl = "";
        }

        /// <summary>
        /// パッケージのpackage.jsonパスを取得
        /// </summary>
        /// <returns>package.jsonの絶対パス。見つからない場合は空文字</returns>
        private static string GetPackageJsonPath([CallerFilePath] string sourceFilePath = "")
        {
            try
            {
                if (string.IsNullOrEmpty(sourceFilePath)) return "";

                // Unityパッケージ構造（Packages/パッケージ名/package.json）を最優先で確認
                var packagePath = TryFindInPackagesDirectory(sourceFilePath);
                if (!string.IsNullOrEmpty(packagePath)) return packagePath;

                // 親ディレクトリを遡って検索
                return TryFindInParentDirectories(sourceFilePath);
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// Unityパッケージ構造でのpackage.json検索
        /// </summary>
        private static string TryFindInPackagesDirectory(string sourceFilePath)
        {
            var normalizedPath = sourceFilePath.Replace('\\', '/');
            var packagesIndex = normalizedPath.ToLower().IndexOf("/packages/");
            
            if (packagesIndex < 0)
            {
                return "";
            }

            var packagesPath = normalizedPath.Substring(0, packagesIndex + "/packages/".Length);
            var remainingPath = normalizedPath.Substring(packagesIndex + "/packages/".Length);
            var pathParts = remainingPath.Split('/');
            
            if (pathParts.Length == 0)
            {
                return "";
            }

            var packageName = pathParts[0];
            var packageJsonPath = System.IO.Path.Combine(packagesPath, packageName, "package.json")
                .Replace('/', System.IO.Path.DirectorySeparatorChar);
            
            return System.IO.File.Exists(packageJsonPath) ? packageJsonPath : "";
        }

        /// <summary>
        /// 親ディレクトリを遡ってpackage.json検索
        /// </summary>
        private static string TryFindInParentDirectories(string sourceFilePath)
        {
            var directory = Path.GetDirectoryName(sourceFilePath);

            while (!string.IsNullOrEmpty(directory))
            {
                var packageJsonPath = Path.Combine(directory, "package.json");
                if (File.Exists(packageJsonPath))
                {
                    return packageJsonPath;
                }

                var parentDirectory = Directory.GetParent(directory)?.FullName;
                if (parentDirectory == directory) // ルートディレクトリに到達
                {
                    break;
                }
                directory = parentDirectory;
            }

            return "";
        }

        /// <summary>
        /// Package.json読み込みヘルパークラス
        /// </summary>
        private static class PackageJsonReader
        {
            private static JObject? cachedPackageInfo = null;
            private static string cachedPath = "";

            /// <summary>
            /// Package.json情報を取得（キャッシュ付き）
            /// </summary>
            /// <returns>パッケージ情報。取得できない場合はnull</returns>
            private static JObject? GetPackageInfo()
            {
                var packageJsonPath = GetPackageJsonPath();
                if (string.IsNullOrEmpty(packageJsonPath)) return null;

                // キャッシュが有効かチェック
                if (cachedPackageInfo != null && cachedPath == packageJsonPath)
                {
                    return cachedPackageInfo;
                }

                try
                {
                    var json = File.ReadAllText(packageJsonPath);
                    cachedPackageInfo = JsonConvert.DeserializeObject<JObject>(json);
                    cachedPath = packageJsonPath;

                    return cachedPackageInfo;
                }
                catch (Exception)
                {
                    cachedPackageInfo = null;
                    cachedPath = "";

                    return null;
                }
            }

            /// <summary>
            /// パッケージ名を取得
            /// </summary>
            public static string GetName()
            {
                var packageInfo = GetPackageInfo();
                return packageInfo?["name"]?.ToString() ?? "";
            }

            /// <summary>
            /// 表示名を取得
            /// </summary>
            public static string GetDisplayName()
            {
                var packageInfo = GetPackageInfo();
                return packageInfo?["displayName"]?.ToString() ?? "";
            }

            /// <summary>
            /// バージョンを取得
            /// </summary>
            public static string GetVersion()
            {
                var packageInfo = GetPackageInfo();
                return packageInfo?["version"]?.ToString() ?? "";
            }

            /// <summary>
            /// リポジトリURLを取得
            /// </summary>
            public static string GetRepoUrl()
            {
                var packageInfo = GetPackageInfo();
                return packageInfo?["repo"]?.ToString() ?? "";
            }
        }

        /// <summary>
        /// 現在のパッケージ名を取得
        /// </summary>
        private static string GetCurrentPackageName()
            => PackageJsonReader.GetName();

        /// <summary>
        /// 現在のパッケージの表示名を取得
        /// </summary>
        private static string GetCurrentDisplayName()
            => PackageJsonReader.GetDisplayName();

        /// <summary>
        /// 現在のバージョンを取得
        /// </summary>
        private static string GetCurrentVersion()
            => PackageJsonReader.GetVersion();

        /// <summary>
        /// AboutWindow など外部から現在のパッケージ情報を取得するための公開メソッド
        /// </summary>
        public static class External
        {
            /// <summary>
            /// 現在のバージョンを取得（外部用）
            /// </summary>
            public static string GetVersion() => PackageJsonReader.GetVersion();

            /// <summary>
            /// 現在のパッケージのリポジトリ URL を取得（外部用）
            /// </summary>
            public static string GetRepoUrl() => PackageJsonReader.GetRepoUrl();

            /// <summary>
            /// 現在のパッケージの表示名を取得（外部用）
            /// </summary>
            public static string GetDisplayName() => PackageJsonReader.GetDisplayName();
        }

        /// <summary>
        /// リモートのバージョン情報取得URLを取得
        /// </summary>
        private static string GetRemoteVersionUrl()
            => PackageJsonReader.GetRepoUrl();

        /// <summary>
        /// HTTP User-Agentを生成
        /// </summary>
        /// <returns>User-Agent文字列</returns>
        private static string GenerateUserAgent()
        {
            var displayName = GetCurrentDisplayName();
            if (string.IsNullOrEmpty(displayName)) displayName = "UnityEditorAssetBrowser";
            
            var version = GetCurrentVersion();
            if (string.IsNullOrEmpty(version)) version = FALLBACK_VERSION;

            return $"Unity-{displayName.Replace(" ", "")}/{version}";
        }

        /// <summary>
        /// バージョンを比較して新しいバージョンがあるかチェック
        /// </summary>
        /// <param name="currentVersion">現在のバージョン</param>
        /// <param name="remoteVersion">リモートのバージョン</param>
        /// <returns>新しいバージョンがある場合true</returns>
        private static bool IsNewerVersionAvailable(string currentVersion, string remoteVersion)
        {
            try
            {
                if (Version.TryParse(currentVersion, out Version current) && Version.TryParse(remoteVersion, out Version remote))
                {
                    return remote > current;
                }

                // バージョンのパースに失敗した場合は文字列比較
                return string.Compare(remoteVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// バージョン文字列のコレクションから最新バージョンを取得
        /// </summary>
        /// <param name="versionStrings">バージョン文字列のコレクション</param>
        /// <returns>最新バージョン文字列。空の場合は空文字</returns>
        private static string GetLatestVersionFromStrings(IEnumerable<string> versionStrings)
        {
            var versionList = new List<Version>();
            var versionMapping = new Dictionary<Version, string>();

            foreach (var versionString in versionStrings)
            {
                if (Version.TryParse(versionString, out Version parsedVersion))
                {
                    versionList.Add(parsedVersion);
                    versionMapping[parsedVersion] = versionString;
                }
            }

            if (versionList.Count > 0)
            {
                versionList.Sort((v1, v2) => v2.CompareTo(v1)); // 降順でソート
                return versionMapping[versionList[0]];
            }

            // パースに失敗した場合は最初の文字列を返す
            foreach (var versionString in versionStrings)
            {
                return versionString;
            }

            // 全てのバージョン文字列が空の場合は空文字を返す（処理スキップ用）
            return string.Empty;
        }

        /// <summary>
        /// JObjectから最新バージョンを取得
        /// </summary>
        /// <param name="versionsObj">バージョンJObject</param>
        /// <returns>最新バージョン文字列</returns>
        private static string GetLatestVersionFromJObject(JObject versionsObj)
        {
            var versionStrings = versionsObj.Properties().Select(prop => prop.Name);
            return GetLatestVersionFromStrings(versionStrings);
        }

        /// <summary>
        /// アップデート通知ダイアログを表示
        /// </summary>
        /// <param name="currentVersion">現在のバージョン</param>
        /// <param name="remoteInfo">リモートバージョン情報</param>
        private static void ShowUpdateNotification(string currentVersion, RemoteVersionInfo remoteInfo)
        {
            // package.jsonからdisplayNameを取得
            var displayName = GetCurrentDisplayName();
            if (string.IsNullOrEmpty(displayName)) displayName = LocalizationService.Instance.GetString("default_package_name");

            var message = string.Format(
                LocalizationService.Instance.GetString("update_available_message"),
                displayName,
                currentVersion,
                remoteInfo.version
            );

            // ウィンドウタイトルにもdisplayNameを含める
            var windowTitle = string.IsNullOrEmpty(displayName) || displayName == LocalizationService.Instance.GetString("default_package_name")
                ? LocalizationService.Instance.GetString("update_available_title")
                : string.Format(LocalizationService.Instance.GetString("update_available_title_format"), displayName);

            var result = EditorUtility.DisplayDialogComplex(
                windowTitle,
                message,
                LocalizationService.Instance.GetString("update_dialog_details"),
                LocalizationService.Instance.GetString("update_dialog_later"),
                LocalizationService.Instance.GetString("update_dialog_ignore")
            );

            switch (result)
            {
                case 0: // 詳細を確認
                    if (!string.IsNullOrEmpty(remoteInfo.changelogUrl))
                    {
                        Application.OpenURL(remoteInfo.changelogUrl);
                    }
                    else if (!string.IsNullOrEmpty(remoteInfo.url))
                    {
                        Application.OpenURL(remoteInfo.url);
                    }
                    break;

                case 1: // 後で通知
                    // 何もしない（次回起動時に再度通知）
                    break;

                case 2: // このバージョンを無視
                    EditorPrefs.SetString(IGNORE_VERSION_KEY, remoteInfo.version);
                    break;
            }
        }

        /// <summary>
        /// リモートからバージョン情報を取得
        /// </summary>
        private static void FetchRemoteVersion()
        {
            var url = GetRemoteVersionUrl();
            if (string.IsNullOrEmpty(url)) return;

            var currentVersion = GetCurrentVersion();
            if (string.IsNullOrEmpty(currentVersion)) return;

            // Unity 2022以降のセキュリティ制限によりHTTPS必須
            if (url.StartsWith("http://")) url = url.Replace("http://", "https://");

            var request = UnityWebRequest.Get(url);
            request.timeout = HTTP_TIMEOUT_SECONDS;
            request.redirectLimit = HTTP_REDIRECT_LIMIT;

            var userAgent = GenerateUserAgent();
            request.SetRequestHeader("User-Agent", userAgent);
            request.SetRequestHeader("Accept", "application/json");

            var operation = request.SendWebRequest();

            // 完了を監視
            void updateFunc()
            {
                if (!operation.isDone) return;

                EditorApplication.update -= updateFunc!;
                ProcessWebRequestResult(request, currentVersion);
                request.Dispose();
            }

            EditorApplication.update += updateFunc;
        }

        /// <summary>
        /// WebRequestの結果を処理
        /// </summary>
        private static void ProcessWebRequestResult(UnityWebRequest request, string currentVersion)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var jsonData = request.downloadHandler.text;
                    DebugLogger.Log($"Version check response received: {jsonData}");
                    var remoteInfo = ParseRemoteVersionInfo(jsonData);
                    if (remoteInfo != null) HandleVersionInfo(currentVersion, remoteInfo);
                }
                catch (Exception ex)
                {
                    DebugLogger.LogError($"Version check process failed: {ex.Message}");
                }
            }
            else
            {
                DebugLogger.LogWarning($"Version check failed: {request.error}");
            }
        }

        /// <summary>
        /// JSONデータからリモートバージョン情報を解析
        /// </summary>
        private static RemoteVersionInfo? ParseRemoteVersionInfo(string jsonData)
        {
            // VPM形式での解析を最初に試行
            var remoteInfo = TryParseVpmFormat(jsonData);
            if (remoteInfo != null) return remoteInfo;

            // VPM形式での解析に失敗した場合、直接のJSON形式を試す
            return TryParseDirectFormat(jsonData);
        }

        /// <summary>
        /// VPM形式でのJSON解析
        /// </summary>
        private static RemoteVersionInfo? TryParseVpmFormat(string jsonData)
        {
            try
            {
                var vpmData = JsonConvert.DeserializeObject<JObject>(jsonData);
                var currentPackageName = GetCurrentPackageName();
                if (vpmData?["packages"] == null || string.IsNullOrEmpty(currentPackageName)) return null;

                var packagesObj = vpmData["packages"] as JObject;
                if (packagesObj == null) return null;

                foreach (var packageProp in packagesObj.Properties())
                {
                    if (packageProp.Name != currentPackageName) continue;
                    return ExtractVersionInfoFromPackage(packageProp.Value);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// パッケージ情報からバージョン情報を抽出
        /// </summary>
        private static RemoteVersionInfo? ExtractVersionInfoFromPackage(JToken? packageValue)
        {
            if (packageValue == null) return null;

            var versionsToken = packageValue["versions"];
            var versionsObj = versionsToken as JObject;
            if (versionsObj == null) return null;

            string latestVersion = GetLatestVersionFromJObject(versionsObj);
            if (string.IsNullOrEmpty(latestVersion)) return null;

            var latestPackageInfo = versionsObj[latestVersion];
            if (latestPackageInfo == null) return null;

            return new RemoteVersionInfo
            {
                name = latestPackageInfo["name"]?.ToString() ?? string.Empty,
                version = latestVersion,
                displayName = latestPackageInfo["displayName"]?.ToString() ?? string.Empty,
                description = latestPackageInfo["description"]?.ToString() ?? string.Empty,
                url = latestPackageInfo["url"]?.ToString() ?? string.Empty,
                changelogUrl = latestPackageInfo["changelogUrl"]?.ToString() ?? string.Empty
            };
        }

        /// <summary>
        /// 直接JSON形式での解析
        /// </summary>
        private static RemoteVersionInfo? TryParseDirectFormat(string jsonData)
        {
            try
            {
                return JsonConvert.DeserializeObject<RemoteVersionInfo>(jsonData);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// バージョン情報の処理（比較、通知、設定更新）
        /// </summary>
        private static void HandleVersionInfo(string currentVersion, RemoteVersionInfo remoteInfo)
        {
            if (IsNewerVersionAvailable(currentVersion, remoteInfo.version))
            {
                // 無視リストにある場合はスキップ
                var ignoredVersion = EditorPrefs.GetString(IGNORE_VERSION_KEY, "");
                if (ignoredVersion != remoteInfo.version)
                {
                    EditorApplication.delayCall += () => ShowUpdateNotification(currentVersion, remoteInfo);
                }
            }

            // 成功時のみチェック日時を更新
            EditorPrefs.SetString(LAST_CHECK_DATE_KEY, DateTime.Now.ToString());
        }

        /// <summary>
        /// バージョンチェックを実行し、必要に応じて通知を表示
        /// ウィンドウ表示時に呼び出される
        /// </summary>
        public static void CheckForUpdates()
        {
            // package.jsonにrepoが設定されていない場合はスキップ
            var url = GetRemoteVersionUrl();
            if (string.IsNullOrEmpty(url)) return;

            // package.jsonにversionが設定されていない場合はスキップ
            var currentVersion = GetCurrentVersion();
            if (string.IsNullOrEmpty(currentVersion)) return;

            // 前回のチェック日を確認
            var lastCheckDateString = EditorPrefs.GetString(LAST_CHECK_DATE_KEY, "");
            if (DateTime.TryParse(lastCheckDateString, out DateTime lastCheckDate))
            {
                var timeSinceLastCheck = DateTime.Now - lastCheckDate;

                // 24時間以内にチェック済みの場合はスキップ
                if (timeSinceLastCheck < TimeSpan.FromHours(VERSION_CHECK_INTERVAL_HOURS))
                {
                    DebugLogger.Log("Skipping version check (checked within 24 hours).");
                    return;
                }
            }
            
            DebugLogger.Log("Checking for updates...");
            // バージョンチェックを実行
            FetchRemoteVersion();
        }
    }
}
