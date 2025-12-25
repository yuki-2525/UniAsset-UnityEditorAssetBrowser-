// Copyright (c) 2025 sakurayuki

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorAssetBrowser.Models;
using UnityEditorAssetBrowser.Services;
using UnityEditorAssetBrowser.ViewModels;
using UnityEditorAssetBrowser.Views;
using UnityEngine;

namespace UnityEditorAssetBrowser
{
    /// <summary>
    /// Unity Editor用のアセットブラウザーウィンドウ
    /// AvatarExplorerとKonoAssetのデータベースを統合して表示・管理する
    /// </summary>
    public class UnityEditorAssetBrowser : EditorWindow
    {
        #region Fields
        /// <summary>ページネーション情報</summary>
        private readonly PaginationInfo _paginationInfo = new();

        /// <summary>ページネーションのViewModel</summary>
        private PaginationViewModel _paginationViewModel = null!;

        /// <summary>アセットブラウザーのViewModel</summary>
        private AssetBrowserViewModel _assetBrowserViewModel = null!;

        /// <summary>検索のViewModel</summary>
        private SearchViewModel _searchViewModel = null!;

        /// <summary>検索ビュー</summary>
        private SearchView _searchView = null!;

        /// <summary>ページネーションビュー</summary>
        private PaginationView _paginationView = null!;

        /// <summary>メインビュー</summary>
        private MainView _mainView = null!;

        /// <summary>アセットアイテムビュー</summary>
        private AssetItemView _assetItemView = null!;
        #endregion

        #region Unity Editor Window Methods
        /// <summary>
        /// メニューからウィンドウを表示する
        /// </summary>
        [MenuItem("Window/Unity Editor Asset Browser")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnityEditorAssetBrowser>();
            window.titleContent = new GUIContent(LocalizationService.Instance.GetString("window_title") ?? "Asset Browser");
            window.Show();
        }

        /// <summary>
        /// ウィンドウが有効になった時の処理
        /// </summary>
        private void OnEnable()
        {
            titleContent = new GUIContent(LocalizationService.Instance.GetString("window_title") ?? "Asset Browser");
            LocalizationService.Instance.OnLanguageChanged += UpdateTitle;

            // 除外フォルダ初期化と合成済みリスト保存
            ExcludeFolderService.InitializeDefaultExcludeFolders();

            var combined = new List<string>();

            var prefs = ExcludeFolderService.LoadPrefs();
            if (prefs != null)
            {
                combined.AddRange(prefs.userFolders);
                combined.AddRange(prefs.enabledDefaults);
            }
            
            ExcludeFolderService.SaveCombinedExcludePatterns(combined);

            InitializeCategoryAssetTypes();
            InitializeServices();
            InitializeViewModels();
            InitializeViews();
            RegisterEventHandlers();

            // バージョンチェックを実行
            try
            {
                VersionUpdateService.CheckForUpdates();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Version check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// サービスの初期化
        /// </summary>
        private void InitializeServices()
        {
            DatabaseService.LoadSettings();
        }

        /// <summary>
        /// ViewModelの初期化
        /// </summary>
        private void InitializeViewModels()
        {
            _paginationViewModel = new PaginationViewModel(_paginationInfo);
            _searchViewModel = new SearchViewModel(DatabaseService.GetAEDatabase());
            _assetBrowserViewModel = new AssetBrowserViewModel(
                DatabaseService.GetAEDatabase(),
                DatabaseService.GetKAAvatarsDatabase(),
                DatabaseService.GetKAWearablesDatabase(),
                DatabaseService.GetKAWorldObjectsDatabase(),
                DatabaseService.GetKAOtherAssetsDatabase(),
                DatabaseService.GetBOOTHLMDatabase(),
                _paginationInfo,
                _searchViewModel
            );
            _assetItemView = new AssetItemView();
        }

        /// <summary>
        /// Viewの初期化
        /// </summary>
        private void InitializeViews()
        {
            _searchView = new SearchView(
                _searchViewModel,
                _assetBrowserViewModel,
                _paginationViewModel,
                _assetItemView
            );
            _paginationView = new PaginationView(_paginationViewModel, _assetBrowserViewModel);
            _mainView = new MainView(
                _searchViewModel,
                _paginationViewModel,
                _searchView,
                _paginationView,
                _assetItemView,
                _assetBrowserViewModel
            );
        }

        /// <summary>
        /// イベントハンドラの登録
        /// </summary>
        private void RegisterEventHandlers()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        /// <summary>
        /// ウィンドウが無効になった時の処理
        /// </summary>
        private void OnDisable()
        {
            LocalizationService.Instance.OnLanguageChanged -= UpdateTitle;
            UnregisterEventHandlers();
        }

        private void UpdateTitle()
        {
            titleContent = new GUIContent(LocalizationService.Instance.GetString("window_title"));
            Repaint();
        }

        /// <summary>
        /// イベントハンドラの解除
        /// </summary>
        private void UnregisterEventHandlers()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        /// <summary>
        /// シーン階層が変更された時の処理
        /// </summary>
        private void OnHierarchyChanged()
        {
            // 画像キャッシュをクリア
            ImageServices.Instance.ClearCache();

            // データベースを再読み込み
            DatabaseService.LoadAEDatabase();
            DatabaseService.LoadKADatabase();
            DatabaseService.LoadBOOTHLMDatabase();
            _searchViewModel.SetCurrentTab(_paginationViewModel.SelectedTab);

            // 現在表示中のアイテムの画像を再読み込み
            var currentItems = _assetBrowserViewModel.GetCurrentTabItems(_paginationViewModel.SelectedTab);
            ImageServices.Instance.ReloadCurrentItemsImages(currentItems);
        }

        /// <summary>
        /// 画像キャッシュの更新（非推奨 - 新しい実装では自動管理）
        /// 新しい実装では表示時に自動的に必要な画像のみ読み込まれる
        /// </summary>
        private void UpdateImageCache()
        {
            // 新しい実装では画像キャッシュは表示時に自動管理されるため、
            // ここではキャッシュクリアのみ実行
            ImageServices.Instance.ClearCache();
        }

        /// <summary>
        /// GUIの描画処理
        /// </summary>
        private void OnGUI()
        {
            _mainView.DrawMainWindow();
        }

        /// <summary>
        /// カテゴリごとのアセットタイプ設定を初期化
        /// EditorPrefsから値を読み込むか、デフォルト値を設定する
        /// </summary>
        private void InitializeCategoryAssetTypes()
        {
            // 初期値の設定
            var defaultTypes = new Dictionary<string, int>
            {
                { "アバター", (int)AssetTypeConstants.Avatar },
                { "衣装", (int)AssetTypeConstants.AvatarRelated },
                { "テクスチャ", (int)AssetTypeConstants.AvatarRelated },
                { "ギミック", (int)AssetTypeConstants.AvatarRelated },
                { "アクセサリー", (int)AssetTypeConstants.AvatarRelated },
                { "髪型", (int)AssetTypeConstants.AvatarRelated },
                { "アニメーション", (int)AssetTypeConstants.AvatarRelated },
                { "ツール", (int)AssetTypeConstants.Other },
                { "シェーダー", (int)AssetTypeConstants.Other },
            };

            // 指定された順序のカテゴリの初期化
            var orderedCategories = new[]
            {
                "アバター",
                "衣装",
                "テクスチャ",
                "ギミック",
                "アクセサリー",
                "髪型",
                "アニメーション",
                "ツール",
                "シェーダー",
            };

            foreach (var category in orderedCategories)
            {
                var key = "UnityEditorAssetBrowser_CategoryAssetType_" + category;
                if (!EditorPrefs.HasKey(key) && defaultTypes.ContainsKey(category))
                {
                    EditorPrefs.SetInt(key, defaultTypes[category]);
                }
            }

            // その他のカテゴリの初期化
            var aeDatabase = DatabaseService.GetAEDatabase();
            if (aeDatabase != null)
            {
                var otherCategories = aeDatabase.Items
                    .Select(item => item.GetAECategoryName())
                    .Distinct()
                    .Where(category => !orderedCategories.Contains(category))
                    .OrderBy(category => category);

                foreach (var category in otherCategories)
                {
                    var key = "UnityEditorAssetBrowser_CategoryAssetType_" + category;
                    if (!EditorPrefs.HasKey(key))
                    {
                        var defaultValue = GetDefaultAssetTypeForCategory(category);
                        EditorPrefs.SetInt(key, defaultValue);
                    }
                }
            }
        }

        /// <summary>
        /// カテゴリのデフォルトアセットタイプを取得
        /// </summary>
        /// <param name="category">カテゴリ名</param>
        /// <returns>デフォルトのアセットタイプのインデックス</returns>
        private int GetDefaultAssetTypeForCategory(string category)
        {
            return IsWorldCategory(category) ? (int)AssetTypeConstants.World : (int)AssetTypeConstants.Other;
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// カテゴリーがワールド関連かどうかを判定
        /// </summary>
        /// <param name="category">カテゴリー名</param>
        /// <returns>ワールド関連の場合true</returns>
        private bool IsWorldCategory(string category)
        {
            return AssetItem.IsWorldCategory(category);
        }
        #endregion
    }
}
