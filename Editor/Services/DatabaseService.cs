// Copyright (c) 2025 sakurayuki
// This code is borrowed from AETools(https://github.com/puk06/AE-Tools)
// AETools is licensed under the MIT License. https://github.com/puk06/AE-Tools/blob/master/LICENSE.txt

#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEditorAssetBrowser.Helper;
using UnityEditorAssetBrowser.Models;
using UnityEditorAssetBrowser.ViewModels;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// データベース操作を支援するサービスクラス
    /// AvatarExplorerとKonoAssetのデータベースの読み込み、保存、更新を管理する
    /// </summary>
    public static class DatabaseService
    {
        public static event Action? OnPathChanged = null;
        
        /// <summary>
        /// AvatarExplorerデータベースパスのEditorPrefsキー
        /// </summary>
        private const string AE_DATABASE_PATH_KEY = "UnityEditorAssetBrowser_AEDatabasePath";

        /// <summary>
        /// KonoAssetデータベースパスのEditorPrefsキー
        /// </summary>
        private const string KA_DATABASE_PATH_KEY = "UnityEditorAssetBrowser_KADatabasePath";

        /// <summary>
        /// AvatarExplorerデータベースのパス
        /// </summary>
        private static string _aeDatabasePath = "";

        /// <summary>
        /// KonoAssetデータベースのパス
        /// </summary>
        private static string _kaDatabasePath = "";

        /// <summary>
        /// AvatarExplorerのデータベース
        /// </summary>
        private static AvatarExplorerDatabase? _aeDatabase;

        /// <summary>
        /// KonoAssetのアバターデータベース
        /// </summary>
        private static KonoAssetAvatarsDatabase? _kaAvatarsDatabase;

        /// <summary>
        /// KonoAssetのウェアラブルデータベース
        /// </summary>
        private static KonoAssetWearablesDatabase? _kaWearablesDatabase;

        /// <summary>
        /// KonoAssetのワールドオブジェクトデータベース
        /// </summary>
        private static KonoAssetWorldObjectsDatabase? _kaWorldObjectsDatabase;

        /// <summary>
        /// KonoAssetのその他アセットデータベース
        /// </summary>
        private static KonoAssetOtherAssetsDatabase? _kaOtherAssetsDatabase;

        private static AssetBrowserViewModel? _assetBrowserViewModel;
        private static SearchViewModel? _searchViewModel;
        private static PaginationViewModel? _paginationViewModel;

        /// <summary>
        /// ViewModelの参照を設定する
        /// </summary>
        public static void SetViewModels(
            AssetBrowserViewModel assetBrowserViewModel,
            SearchViewModel searchViewModel,
            PaginationViewModel paginationViewModel
        )
        {
            _assetBrowserViewModel = assetBrowserViewModel;
            _searchViewModel = searchViewModel;
            _paginationViewModel = paginationViewModel;
        }

        /// <summary>
        /// データベースの設定を読み込む
        /// 保存されたパスからデータベースを読み込み、更新する
        /// </summary>
        public static void LoadSettings()
        {
            _aeDatabasePath = EditorPrefs.GetString(AE_DATABASE_PATH_KEY, "");
            _kaDatabasePath = EditorPrefs.GetString(KA_DATABASE_PATH_KEY, "");

            if (!string.IsNullOrEmpty(_aeDatabasePath)) LoadAEDatabase();
            if (!string.IsNullOrEmpty(_kaDatabasePath)) LoadKADatabase();
        }

        /// <summary>
        /// データベースの設定を保存する
        /// 現在のパスをEditorPrefsに保存する
        /// </summary>
        public static void SaveSettings()
        {
            EditorPrefs.SetString(AE_DATABASE_PATH_KEY, _aeDatabasePath);
            EditorPrefs.SetString(KA_DATABASE_PATH_KEY, _kaDatabasePath);
        }

        /// <summary>
        /// AvatarExplorerデータベースを読み込み、更新する
        /// パスが無効な場合はエラーメッセージを表示し、パスをリセットする
        /// </summary>
        public static void LoadAEDatabase()
        {
            // データベースをクリア
            ClearAEDatabase();

            if (!string.IsNullOrEmpty(_aeDatabasePath))
            {
                _aeDatabase = AEDatabaseHelper.LoadAEDatabaseFile(_aeDatabasePath);
                if (_aeDatabase == null)
                {
                    OnAEDatabasePathChanged("");
                    ShowErrorDialog(
                        LocalizationService.Instance.GetString("error_path_title"),
                        LocalizationService.Instance.GetString("error_ae_path_message")
                    );
                    return;
                }
            }

            // データベースを更新
            if (
                _assetBrowserViewModel != null
                && _searchViewModel != null
                && _paginationViewModel != null
            )
            {
                _assetBrowserViewModel.UpdateDatabases(
                    GetAEDatabase(),
                    GetKAAvatarsDatabase(),
                    GetKAWearablesDatabase(),
                    GetKAWorldObjectsDatabase(),
                    GetKAOtherAssetsDatabase()
                );
                _searchViewModel.SetCurrentTab(_paginationViewModel.SelectedTab);
            }
        }

        /// <summary>
        /// KonoAssetデータベースを読み込み、更新する
        /// パスが無効な場合はエラーメッセージを表示し、パスをリセットする
        /// </summary>
        public static void LoadKADatabase()
        {
            // データベースをクリア
            ClearKADatabase();

            if (!string.IsNullOrEmpty(_kaDatabasePath))
            {
                var metadataPath = Path.Combine(_kaDatabasePath, "metadata");
                if (!Directory.Exists(metadataPath))
                {
                    OnKADatabasePathChanged("");
                    ShowErrorDialog(
                        LocalizationService.Instance.GetString("error_path_title"),
                        LocalizationService.Instance.GetString("error_ka_path_message")
                    );
                    return;
                }

                var result = KADatabaseHelper.LoadKADatabaseFiles(metadataPath);
                _kaAvatarsDatabase = result.AvatarsDatabase;
                _kaWearablesDatabase = result.WearablesDatabase;
                _kaWorldObjectsDatabase = result.WorldObjectsDatabase;
                _kaOtherAssetsDatabase = result.OtherAssetsDatabase;
            }

            // データベースを更新
            if (
                _assetBrowserViewModel != null
                && _searchViewModel != null
                && _paginationViewModel != null
            )
            {
                _assetBrowserViewModel.UpdateDatabases(
                    GetAEDatabase(),
                    GetKAAvatarsDatabase(),
                    GetKAWearablesDatabase(),
                    GetKAWorldObjectsDatabase(),
                    GetKAOtherAssetsDatabase()
                );
                _searchViewModel.SetCurrentTab(_paginationViewModel.SelectedTab);
            }
        }

        /// <summary>
        /// エラーダイアログを表示する
        /// </summary>
        /// <param name="title">ダイアログのタイトル</param>
        /// <param name="message">表示するメッセージ</param>
        private static void ShowErrorDialog(string title, string message)
        {
            EditorUtility.DisplayDialog(title, message, "OK");
        }

        public static void OnAEDatabasePathChanged(string path)
        {
            SetAEDatabasePath(path);

            if (string.IsNullOrEmpty(path))
            {
                // パスが空の場合は、データベースをクリアして即座に更新
                ClearAEDatabase();

                if (
                    _assetBrowserViewModel != null
                    && _searchViewModel != null
                    && _paginationViewModel != null
                )
                {
                    _assetBrowserViewModel.UpdateDatabases(
                        null,
                        GetKAAvatarsDatabase(),
                        GetKAWearablesDatabase(),
                        GetKAWorldObjectsDatabase(),
                        GetKAOtherAssetsDatabase()
                    );
                    // SetCurrentTabは呼ばない
                }
            }
            else
            {
                LoadAEDatabase();
            }

            SaveSettings();
            OnPathChanged?.Invoke();
        }

        public static void OnKADatabasePathChanged(string path)
        {
            SetKADatabasePath(path);

            if (string.IsNullOrEmpty(path))
            {
                // パスが空の場合は、データベースをクリアして即座に更新
                ClearKADatabase();
                if (
                    _assetBrowserViewModel != null
                    && _searchViewModel != null
                    && _paginationViewModel != null
                )
                {
                    _assetBrowserViewModel.UpdateDatabases(GetAEDatabase(), null, null, null, null);
                    // SetCurrentTabは呼ばない
                }
            }
            else
            {
                LoadKADatabase();
            }

            SaveSettings();
            OnPathChanged?.Invoke();
        }

        /// <summary>
        /// AvatarExplorerデータベースのパスを取得する
        /// </summary>
        /// <returns>データベースのパス</returns>
        public static string GetAEDatabasePath()
        {
            if (string.IsNullOrEmpty(_aeDatabasePath)) return string.Empty;
            if (!_aeDatabasePath.EndsWith("Datas")) return Path.GetFullPath(Path.Combine(_aeDatabasePath, "Datas"));
            return Path.GetFullPath(_aeDatabasePath);
        }

        /// <summary>
        /// KonoAssetデータベースのパスを取得する
        /// </summary>
        /// <returns>データベースのパス</returns>
        public static string GetKADatabasePath()
            => _kaDatabasePath;

        /// <summary>
        /// AvatarExplorerデータベースのパスを設定する
        /// </summary>
        /// <param name="path">設定するパス</param>
        public static void SetAEDatabasePath(string path)
            => _aeDatabasePath = path;

        /// <summary>
        /// KonoAssetデータベースのパスを設定する
        /// </summary>
        /// <param name="path">設定するパス</param>
        public static void SetKADatabasePath(string path)
            => _kaDatabasePath = path;

        /// <summary>
        /// AvatarExplorerデータベースを取得する
        /// </summary>
        /// <returns>データベース（存在しない場合はnull）</returns>
        public static AvatarExplorerDatabase? GetAEDatabase()
            => _aeDatabase;

        /// <summary>
        /// KonoAssetアバターデータベースを取得する
        /// </summary>
        /// <returns>データベース（存在しない場合はnull）</returns>
        public static KonoAssetAvatarsDatabase? GetKAAvatarsDatabase()
            => _kaAvatarsDatabase;

        /// <summary>
        /// KonoAssetウェアラブルデータベースを取得する
        /// </summary>
        /// <returns>データベース（存在しない場合はnull）</returns>
        public static KonoAssetWearablesDatabase? GetKAWearablesDatabase()
            => _kaWearablesDatabase;

        /// <summary>
        /// KonoAssetワールドオブジェクトデータベースを取得する
        /// </summary>
        /// <returns>データベース（存在しない場合はnull）</returns>
        public static KonoAssetWorldObjectsDatabase? GetKAWorldObjectsDatabase()
            => _kaWorldObjectsDatabase;

        /// <summary>
        /// KonoAssetその他アセットデータベースを取得する
        /// </summary>
        /// <returns>データベース（存在しない場合はnull）</returns>
        public static KonoAssetOtherAssetsDatabase? GetKAOtherAssetsDatabase()
            => _kaOtherAssetsDatabase;

        /// <summary>
        /// AvatarExplorerデータベースをクリアする
        /// </summary>
        public static void ClearAEDatabase()
            => _aeDatabase = null;

        /// <summary>
        /// KonoAssetデータベースをクリアする
        /// </summary>
        public static void ClearKADatabase()
        {
            _kaAvatarsDatabase = null;
            _kaWearablesDatabase = null;
            _kaWorldObjectsDatabase = null;
            _kaOtherAssetsDatabase = null;
        }
    }
}
