// Copyright (c) 2025-2026 sakurayuki
// This code is borrowed from AETools(https://github.com/puk06/AE-Tools)
// AETools is licensed under the MIT License. https://github.com/puk06/AE-Tools/blob/master/LICENSE.txt

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
        /// BOOTHLMデータベースパスのEditorPrefsキー
        /// </summary>
        private const string BOOTHLM_DATABASE_PATH_KEY = "UnityEditorAssetBrowser_BOOTHLMDatabasePath";

        /// <summary>
        /// AvatarExplorerデータベースのパス
        /// </summary>
        private static string _aeDatabasePath = "";

        /// <summary>
        /// KonoAssetデータベースのパス
        /// </summary>
        private static string _kaDatabasePath = "";

        /// <summary>
        /// BOOTHLMデータベースのパス（データ保存先）
        /// </summary>
        private static string _boothlmDatabasePath = "";

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

        /// <summary>
        /// BOOTHLMのデータベース
        /// </summary>
        private static BOOTHLMDatabase? _boothlmDatabase;

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
            DebugLogger.Log("Loading database settings...");
            _aeDatabasePath = EditorPrefs.GetString(AE_DATABASE_PATH_KEY, "");
            _kaDatabasePath = EditorPrefs.GetString(KA_DATABASE_PATH_KEY, "");
            _boothlmDatabasePath = EditorPrefs.GetString(BOOTHLM_DATABASE_PATH_KEY, "");

            DebugLogger.Log($"AE Path: {_aeDatabasePath}");
            DebugLogger.Log($"KA Path: {_kaDatabasePath}");
            DebugLogger.Log($"BOOTHLM Path: {_boothlmDatabasePath}");

            if (!string.IsNullOrEmpty(_aeDatabasePath)) LoadAEDatabase();
            if (!string.IsNullOrEmpty(_kaDatabasePath)) LoadKADatabase();
            if (!string.IsNullOrEmpty(_boothlmDatabasePath)) LoadBOOTHLMDatabase();
        }

        /// <summary>
        /// データベースの設定を保存する
        /// 現在のパスをEditorPrefsに保存する
        /// </summary>
        public static void SaveSettings()
        {
            EditorPrefs.SetString(AE_DATABASE_PATH_KEY, _aeDatabasePath);
            EditorPrefs.SetString(KA_DATABASE_PATH_KEY, _kaDatabasePath);
            EditorPrefs.SetString(BOOTHLM_DATABASE_PATH_KEY, _boothlmDatabasePath);
        }

        /// <summary>
        /// AvatarExplorerデータベースを読み込み、更新する
        /// パスが無効な場合はエラーメッセージを表示し、パスをリセットする
        /// </summary>
        public static void LoadAEDatabase()
        {
            DebugLogger.Log($"LoadAEDatabase path: {_aeDatabasePath}");

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

            UpdateViewModels();
        }

        /// <summary>
        /// KonoAssetデータベースを読み込み、更新する
        /// パスが無効な場合はエラーメッセージを表示し、パスをリセットする
        /// </summary>
        public static void LoadKADatabase()
        {
            DebugLogger.Log($"LoadKADatabase path: {_kaDatabasePath}");

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

            UpdateViewModels();
        }

        /// <summary>
        /// BOOTHLMデータベースを読み込み、更新する
        /// data.dbのパスは固定
        /// </summary>
        public static void LoadBOOTHLMDatabase()
        {
            // データベースをクリア
            ClearBOOTHLMDatabase();

            // data.dbのパスは固定
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dbPath = Path.Combine(appDataPath, "pm.booth.library-manager", "data.db");
            DebugLogger.Log($"LoadBOOTHLMDatabase path: {dbPath}");

            if (File.Exists(dbPath))
            {
                _boothlmDatabase = BOOTHLMDatabaseHelper.LoadBOOTHLMDatabase(dbPath);
            }
            
            UpdateViewModels();
        }

        private static void UpdateViewModels()
        {
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
                    GetKAOtherAssetsDatabase(),
                    GetBOOTHLMDatabase()
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
                        GetKAOtherAssetsDatabase(),
                        GetBOOTHLMDatabase()
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
                    _assetBrowserViewModel.UpdateDatabases(GetAEDatabase(), null, null, null, null, GetBOOTHLMDatabase());
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

        public static void OnBOOTHLMDatabasePathChanged(string path)
        {
            SetBOOTHLMDatabasePath(path);
            
            // パスが変わってもDBの再読み込みは不要（DBパスは固定だから）
            // ただし、GetItemPathの結果が変わるため、ビューの更新は必要
            // LoadBOOTHLMDatabaseを呼ぶことでUpdateDatabasesが走り、ビューが更新される
            LoadBOOTHLMDatabase();

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
        /// BOOTHLMデータベースのパス（データ保存先）を取得する
        /// </summary>
        /// <returns>データベースのパス</returns>
        public static string GetBOOTHLMDataPath()
            => _boothlmDatabasePath;

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
        /// BOOTHLMデータベースのパス（データ保存先）を設定する
        /// </summary>
        /// <param name="path">設定するパス</param>
        public static void SetBOOTHLMDatabasePath(string path)
            => _boothlmDatabasePath = path;

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
        /// 全てのKonoAssetデータベースを統合して取得する
        /// </summary>
        /// <returns>統合データベース（全てのデータベースが存在しない場合はnull）</returns>
        public static UnifiedKonoAssetDatabase? GetKADatabase()
        {
            if (_kaAvatarsDatabase == null && _kaWearablesDatabase == null && _kaWorldObjectsDatabase == null && _kaOtherAssetsDatabase == null)
            {
                return null;
            }

            var unified = new UnifiedKonoAssetDatabase();
            if (_kaAvatarsDatabase != null) unified.Items.AddRange(_kaAvatarsDatabase.Data);
            if (_kaWearablesDatabase != null) unified.Items.AddRange(_kaWearablesDatabase.Data);
            if (_kaWorldObjectsDatabase != null) unified.Items.AddRange(_kaWorldObjectsDatabase.Data);
            if (_kaOtherAssetsDatabase != null) unified.Items.AddRange(_kaOtherAssetsDatabase.Data);
            
            return unified;
        }

        /// <summary>
        /// BOOTHLMデータベースを取得する
        /// </summary>
        /// <returns>データベース（存在しない場合はnull）</returns>
        public static BOOTHLMDatabase? GetBOOTHLMDatabase()
            => _boothlmDatabase;

        public static List<BOOTHLMList> GetBOOTHLMLists()
        {
            return GetBOOTHLMDatabase()?.Lists ?? new List<BOOTHLMList>();
        }

        public static List<BOOTHLMItem> GetItemsForBOOTHLMList(BOOTHLMList list)
        {
            var ids = BOOTHLMDatabaseHelper.GetListItemRegisteredIds(list);
            var database = GetBOOTHLMDatabase();
            if (database == null) return new List<BOOTHLMItem>();

            // IDリストに含まれるアイテムを抽出
            var idSet = new HashSet<string>(ids);
            return database.Items.Where(i => idSet.Contains(i.RegisteredId)).ToList();
        }

        public static (int TotalCount, List<BOOTHLMItem> PreviewItems) GetPreviewItemsForBOOTHLMList(BOOTHLMList list, int count = 5)
        {
            // 全IDを取得して総数を把握
            var allIds = BOOTHLMDatabaseHelper.GetListItemRegisteredIds(list);
            int totalCount = allIds.Count;
            
            // プレビュー用に指定数だけ取得
            var previewIds = allIds.Take(count).ToList();
            
            var database = GetBOOTHLMDatabase();
            if (database == null) return (0, new List<BOOTHLMItem>());

            // IDリストに含まれるアイテムを抽出
            var idSet = new HashSet<string>(previewIds);
            var items = database.Items.Where(i => idSet.Contains(i.RegisteredId)).ToList();
            
            // 取得順序を維持するために並び替え
            var orderedItems = new List<BOOTHLMItem>();
            foreach (var id in previewIds)
            {
                var item = items.FirstOrDefault(i => i.RegisteredId == id);
                if (item != null) orderedItems.Add(item);
            }
            
            return (totalCount, orderedItems);
        }


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

        /// <summary>
        /// BOOTHLMデータベースをクリアする
        /// </summary>
        public static void ClearBOOTHLMDatabase()
            => _boothlmDatabase = null;
    }
}
