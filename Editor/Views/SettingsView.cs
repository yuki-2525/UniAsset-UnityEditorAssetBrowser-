// Copyright (c) 2025 sakurayuki

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorAssetBrowser.Helper;
using UnityEditorAssetBrowser.Models;
using UnityEditorAssetBrowser.Services;
using UnityEngine;

namespace UnityEditorAssetBrowser.Views
{
    /// <summary>
    /// 設定画面のUIを管理するクラス
    /// データベースパスの設定と、AvatarExplorerのカテゴリごとのアセットタイプ設定を提供
    /// </summary>
    public class SettingsView
    {
        private readonly Action<string> _onAEDatabasePathChanged;
        private readonly Action<string> _onKADatabasePathChanged;
        private readonly Action<string> _onBOOTHLMDataPathChanged;
        
        /// <summary>
        /// 設定が変更された時に発生するイベント
        /// </summary>
        public event Action OnSettingsChanged;

        private Vector2 _categoryScrollPosition;
        private bool _showDatabaseSettings;
        private bool _showCategorySettings;
        private bool _showAECategories;
        private bool _showKACategories;
        private bool _showBOOTHLMCategories;
        private bool _showFolderThumbnailSettings = false;
        private bool _showImportSettings = false;
        private List<string> _userExcludeFolders;
        private HashSet<string> _enabledDefaultExcludeFolders;
        private string _newExcludeFolder = "";
        private Vector2 _excludeFoldersScrollPosition;
        private bool _showDefaultExcludeFolders = false;

        /// <summary>
        /// 指定された順序で表示するカテゴリのリスト
        /// </summary>
        private readonly string[] _orderedCategories = new[]
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

        /// <summary>
        /// カテゴリに設定可能なアセットタイプのリスト
        /// </summary>
        private readonly string[] _assetTypes = new[]
        {
            "アバター",
            "アバター関連アセット",
            "ワールドアセット",
            "その他",
        };

        /// <summary>
        /// カテゴリごとのアセットタイプ設定を保持する辞書
        /// </summary>
        private readonly Dictionary<string, int> _categoryAssetTypes = new Dictionary<string, int>();

        /// <summary>
        /// BOOTHLMのカテゴリごとのアセットタイプ設定を保持する辞書
        /// </summary>
        private readonly Dictionary<string, int> _boothlmCategoryAssetTypes = new Dictionary<string, int>();

        /// <summary>
        /// EditorPrefsに保存する際のキーのプレフィックス
        /// </summary>
        private const string PREFS_KEY_PREFIX = "UnityEditorAssetBrowser_CategoryAssetType_";
        private const string PREFS_KEY_BOOTHLM_PREFIX = "UnityEditorAssetBrowser_BOOTHLMCategoryAssetType_";
        private const string PREFS_KEY_CATEGORY_FOLDER_NAME_PREFIX = "UnityEditorAssetBrowser_CategoryFolderName_";

        // EditorPrefsキー
        private const string PREFS_KEY_SHOW_FOLDER_THUMBNAIL = "UnityEditorAssetBrowser_ShowFolderThumbnail";
        private const string PREFS_KEY_GENERATE_FOLDER_THUMBNAIL = "UnityEditorAssetBrowser_GenerateFolderThumbnail";
        private const string PREFS_KEY_EXCLUDE_FOLDERS = "UnityEditorAssetBrowser_ExcludeFolders";
        private const string PREFS_KEY_IMPORT_TO_CATEGORY_FOLDER = "UnityEditorAssetBrowser_ImportToCategoryFolder";
        private const string PREFS_KEY_ICON_SIZE = "UnityEditorAssetBrowser_IconSize";
        private const string PREFS_KEY_FONT_SIZE = "UnityEditorAssetBrowser_FontSize";
        private const string PREFS_KEY_AUTO_SEARCH = "UnityEditorAssetBrowser_AutoSearch";
        public const string PREFS_KEY_SHOW_IMPORT_DIALOG = "UnityEditorAssetBrowser_ShowImportDialog";

        // 表示サイズ設定
        private bool _showDisplaySizeSettings = false;
        private bool _showSearchSettings = false;
        private readonly int[] _iconSizes = new[] { 80, 100, 120, 140, 160, 180, 200, 220, 240, 260, 280, 300 };
        private readonly int[] _fontSizes = new[] { 10, 11, 12, 13, 14, 15, 16 };

        // 初期設定リスト（abc順）
        private static readonly List<string> _allDefaultExcludeFolders = ExcludeFolderService.GetAllDefaultExcludeFolders()
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="onAEDatabasePathChanged">AEデータベースのパスが変更された時のコールバック</param>
        /// <param name="onKADatabasePathChanged">KAデータベースのパスが変更された時のコールバック</param>
        /// <param name="onBOOTHLMDataPathChanged">BOOTHLMデータパスが変更された時のコールバック</param>
        public SettingsView(
            Action<string> onAEDatabasePathChanged,
            Action<string> onKADatabasePathChanged,
            Action<string> onBOOTHLMDataPathChanged
        )
        {
            _onAEDatabasePathChanged = onAEDatabasePathChanged;
            _onKADatabasePathChanged = onKADatabasePathChanged;
            _onBOOTHLMDataPathChanged = onBOOTHLMDataPathChanged;
            
            ExcludeFolderService.InitializeDefaultExcludeFolders();
            InitializeCategoryAssetTypes();
            InitializeSettingsVisibility();
            InitializeExcludeFolders();
        }

        /// <summary>
        /// 設定の表示状態を初期化
        /// </summary>
        private void InitializeSettingsVisibility()
        {
            var aePath = DatabaseService.GetAEDatabasePath();
            var kaPath = DatabaseService.GetKADatabasePath();
            var boothlmPath = DatabaseService.GetBOOTHLMDataPath();

            // データベース設定の表示状態を初期化
            _showDatabaseSettings = string.IsNullOrEmpty(aePath) && string.IsNullOrEmpty(kaPath) && string.IsNullOrEmpty(boothlmPath);

            // カテゴリ設定の表示状態を初期化
            _showCategorySettings = !string.IsNullOrEmpty(aePath) || !string.IsNullOrEmpty(kaPath) || !string.IsNullOrEmpty(boothlmPath);
            _showAECategories = !string.IsNullOrEmpty(aePath);
            _showKACategories = !string.IsNullOrEmpty(kaPath);
            _showBOOTHLMCategories = !string.IsNullOrEmpty(boothlmPath);
        }

        /// <summary>
        /// カテゴリごとのアセットタイプ設定を初期化
        /// EditorPrefsから値を読み込むか、デフォルト値を設定する
        /// </summary>
        private void InitializeCategoryAssetTypes()
        {
            // 指定された順序のカテゴリの初期化
            foreach (var category in _orderedCategories)
            {
                var key = PREFS_KEY_PREFIX + category;
                var value = EditorPrefs.GetInt(key);
                _categoryAssetTypes[category] = value;
            }

            // その他のカテゴリの初期化
            var aeDatabase = DatabaseService.GetAEDatabase();
            var kaDatabase = DatabaseService.GetKADatabase();
            var allCategories = new HashSet<string>();

            if (aeDatabase != null)
            {
                foreach (var item in aeDatabase.Items)
                {
                    allCategories.Add(item.GetAECategoryName());
                }
            }
            if (kaDatabase != null)
            {
                foreach (var item in kaDatabase.Items)
                {
                    allCategories.Add(item.GetCategory());
                }
            }

            var otherCategories = allCategories
                .Where(category => !_orderedCategories.Contains(category))
                .OrderBy(category => category);

            foreach (var category in otherCategories)
            {
                var key = PREFS_KEY_PREFIX + category;
                var value = EditorPrefs.GetInt(key);
                _categoryAssetTypes[category] = value;
            }

            // BOOTHLMのカテゴリ初期化
            var boothlmDatabase = DatabaseService.GetBOOTHLMDatabase();
            if (boothlmDatabase != null)
            {
                var categories = boothlmDatabase.Items
                    .Select(item => item.CategoryName)
                    .Distinct()
                    .OrderBy(c => c);

                foreach (var category in categories)
                {
                    var key = PREFS_KEY_BOOTHLM_PREFIX + category;
                    if (EditorPrefs.HasKey(key))
                    {
                        _boothlmCategoryAssetTypes[category] = EditorPrefs.GetInt(key);
                    }
                    else
                    {
                        _boothlmCategoryAssetTypes[category] = GetDefaultBOOTHLMAssetType(category);
                    }
                }
            }
        }

        /// <summary>
        /// BOOTHLMカテゴリのデフォルトアセットタイプを取得
        /// </summary>
        private int GetDefaultBOOTHLMAssetType(string category)
        {
            if (category.Contains("3D Characters") || category.Contains("3Dキャラクター") || category.Contains("Avatar") || category.Contains("アバター"))
                return (int)AssetTypeConstants.Avatar;
            if (category.Contains("3D Costumes") || category.Contains("3D衣装") || category.Contains("3D Accessories") || category.Contains("3D装飾品") || category.Contains("Fashion") || category.Contains("ファッション"))
                return (int)AssetTypeConstants.AvatarRelated;
            if (category.Contains("3D Environments") || category.Contains("3D環境") || category.Contains("World") || category.Contains("ワールド"))
                return (int)AssetTypeConstants.World;
            return (int)AssetTypeConstants.Other;
        }

        /// <summary>
        /// カテゴリのアセットタイプ設定をEditorPrefsに保存
        /// </summary>
        /// <param name="category">カテゴリ名</param>
        /// <param name="value">設定するアセットタイプのインデックス</param>
        private void SaveCategoryAssetType(string category, int value)
        {
            var key = PREFS_KEY_PREFIX + category;
            EditorPrefs.SetInt(key, value);
        }

        /// <summary>
        /// BOOTHLMカテゴリのアセットタイプ設定をEditorPrefsに保存
        /// </summary>
        private void SaveBOOTHLMCategoryAssetType(string category, int value)
        {
            var key = PREFS_KEY_BOOTHLM_PREFIX + category;
            EditorPrefs.SetInt(key, value);
        }

        /// <summary>
        /// 除外フォルダ設定を初期化
        /// </summary>
        private void InitializeExcludeFolders()
        {
            var prefs = ExcludeFolderService.LoadPrefs();
            _userExcludeFolders = prefs?.userFolders ?? new List<string>();
            _enabledDefaultExcludeFolders = new HashSet<string>(prefs?.enabledDefaults ?? ExcludeFolderService.GetAllDefaultExcludeFolders());
        }

        /// <summary>
        /// 除外フォルダ設定を保存
        /// </summary>
        private void SaveExcludeFoldersAndCombined()
        {
            ExcludeFolderService.SaveExcludeFolders(
                _userExcludeFolders,
                _enabledDefaultExcludeFolders.ToList()
            );

            var combined = new List<string>(_userExcludeFolders);
            combined.AddRange(_enabledDefaultExcludeFolders);

            ExcludeFolderService.SaveCombinedExcludePatterns(combined);
        }

        [Serializable]
        private class ExcludeFoldersData
        {
            public List<string> Folders = new List<string>();
        }

        /// <summary>
        /// 設定画面のUIを描画
        /// </summary>
        public void Draw()
        {
            // 言語設定
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("language", GUILayout.Width(100));
            var currentLang = LocalizationService.Instance.CurrentLanguage;
            var availableLangs = LocalizationService.Instance.AvailableLanguages;
            var displayLangs = availableLangs.Select(l => l == "ja" ? "日本語" : (l == "en" ? "English" : l)).ToArray();
            var currentIndex = Array.IndexOf(availableLangs, currentLang);
            if (currentIndex < 0) currentIndex = 0;
            
            var newIndex = EditorGUILayout.Popup(currentIndex, displayLangs);
            if (newIndex != currentIndex)
            {
                LocalizationService.Instance.CurrentLanguage = availableLangs[newIndex];
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            // データベース設定セクション
            _showDatabaseSettings = EditorGUILayout.Foldout(
                _showDatabaseSettings,
                LocalizationService.Instance.GetString("database_settings"),
                true,
                GUIStyleManager.Foldout
            );

            if (_showDatabaseSettings)
            {
                EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);
                DrawDatabasePathField(
                    "AE Database Path:",
                    DatabaseService.GetAEDatabasePath(),
                    _onAEDatabasePathChanged
                );

                DrawDatabasePathField(
                    "KA Database Path:",
                    DatabaseService.GetKADatabasePath(),
                    _onKADatabasePathChanged
                );

                DrawDatabasePathField(
                    "BOOTHLM Data Path:",
                    DatabaseService.GetBOOTHLMDataPath(),
                    _onBOOTHLMDataPathChanged
                );

                EditorGUILayout.EndVertical();
            }

            // 検索設定セクション
            EditorGUILayout.Space(10);
            _showSearchSettings = EditorGUILayout.Foldout(
                _showSearchSettings,
                LocalizationService.Instance.GetString("search_settings"),
                true,
                GUIStyleManager.Foldout
            );

            if (_showSearchSettings)
            {
                EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);

                bool autoSearch = EditorPrefs.GetBool(PREFS_KEY_AUTO_SEARCH, true);
                bool newAutoSearch = EditorGUILayout.ToggleLeft(
                    LocalizationService.Instance.GetString("auto_search"),
                    autoSearch,
                    GUIStyleManager.Label
                );

                if (newAutoSearch != autoSearch)
                {
                    EditorPrefs.SetBool(PREFS_KEY_AUTO_SEARCH, newAutoSearch);
                }

                EditorGUILayout.EndVertical();
            }

            // 表示サイズ設定セクション
            EditorGUILayout.Space(10);
            _showDisplaySizeSettings = EditorGUILayout.Foldout(
                _showDisplaySizeSettings,
                LocalizationService.Instance.GetString("display_size_settings"),
                true,
                GUIStyleManager.Foldout
            );

            if (_showDisplaySizeSettings)
            {
                EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);

                // アイコンサイズ設定
                int currentIconSize = EditorPrefs.GetInt(PREFS_KEY_ICON_SIZE, 120);
                int iconSizeIndex = GetClosestIndex(_iconSizes, currentIconSize);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(LocalizationService.Instance.GetString("icon_size"), GUIStyleManager.Label, GUILayout.Width(120));
                int newIconSizeIndex = Mathf.RoundToInt(GUILayout.HorizontalSlider(iconSizeIndex, 0, _iconSizes.Length - 1));
                EditorGUILayout.LabelField($"{_iconSizes[newIconSizeIndex]}px", GUIStyleManager.Label, GUILayout.Width(50));
                
                if (newIconSizeIndex != iconSizeIndex)
                {
                    EditorPrefs.SetInt(PREFS_KEY_ICON_SIZE, _iconSizes[newIconSizeIndex]);
                    OnSettingsChanged?.Invoke();
                }
                EditorGUILayout.EndHorizontal();

                // 文字サイズ設定
                int currentFontSize = EditorPrefs.GetInt(PREFS_KEY_FONT_SIZE, 13);
                int fontSizeIndex = GetClosestIndex(_fontSizes, currentFontSize);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(LocalizationService.Instance.GetString("font_size"), GUIStyleManager.Label, GUILayout.Width(120));
                int newFontSizeIndex = Mathf.RoundToInt(GUILayout.HorizontalSlider(fontSizeIndex, 0, _fontSizes.Length - 1));
                EditorGUILayout.LabelField($"{_fontSizes[newFontSizeIndex]}px", GUIStyleManager.Label, GUILayout.Width(50));

                if (newFontSizeIndex != fontSizeIndex)
                {
                    EditorPrefs.SetInt(PREFS_KEY_FONT_SIZE, _fontSizes[newFontSizeIndex]);
                    OnSettingsChanged?.Invoke();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            // カテゴリ設定セクション
            EditorGUILayout.Space(10);
            _showCategorySettings = EditorGUILayout.Foldout(
                _showCategorySettings,
                LocalizationService.Instance.GetString("category_settings"),
                true,
                GUIStyleManager.Foldout
            );

            if (_showCategorySettings)
            {
                var aeDatabase = DatabaseService.GetAEDatabase();
                var kaDatabase = DatabaseService.GetKADatabase();
                var boothlmDatabase = DatabaseService.GetBOOTHLMDatabase();

                {
                    EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);

                    bool anyExpanded = (aeDatabase != null && _showAECategories) || 
                                       (kaDatabase != null && _showKACategories) || 
                                       (boothlmDatabase != null && _showBOOTHLMCategories);

                    var scrollOptions = new List<GUILayoutOption> { GUILayout.ExpandHeight(false) };
                    if (anyExpanded)
                    {
                        scrollOptions.Add(GUILayout.MaxHeight(500));
                    }

                    _categoryScrollPosition = EditorGUILayout.BeginScrollView(
                        _categoryScrollPosition,
                        scrollOptions.ToArray()
                    );

                    if (aeDatabase != null)
                    {
                        _showAECategories = EditorGUILayout.Foldout(
                            _showAECategories,
                            "Avatar Explorer Categories",
                            true,
                            GUIStyleManager.Foldout
                        );

                        if (_showAECategories)
                        {
                            var aeCategories = new HashSet<string>();
                            foreach (var item in aeDatabase.Items)
                            {
                                aeCategories.Add(item.GetAECategoryName());
                            }

                            // 指定された順序のカテゴリを表示
                            foreach (var category in _orderedCategories)
                            {
                                if (!aeCategories.Contains(category)) continue;

                                int count = aeDatabase.Items.Count(item => item.GetAECategoryName() == category);
                                if (count > 0)
                                {
                                    DrawCategorySettingRow(category, count, _categoryAssetTypes, SaveCategoryAssetType);
                                }
                            }

                            // その他のカテゴリを表示
                            var otherCategories = aeCategories
                                .Where(category => !_orderedCategories.Contains(category))
                                .OrderBy(category => category);

                            foreach (var category in otherCategories)
                            {
                                int count = aeDatabase.Items.Count(item => item.GetAECategoryName() == category);
                                DrawCategorySettingRow(category, count, _categoryAssetTypes, SaveCategoryAssetType);
                            }
                        }
                        EditorGUILayout.Space(10);
                    }

                    if (kaDatabase != null)
                    {
                        _showKACategories = EditorGUILayout.Foldout(
                            _showKACategories,
                            "KonoAsset Categories",
                            true,
                            GUIStyleManager.Foldout
                        );

                        if (_showKACategories)
                        {
                            var kaCategories = kaDatabase.Items
                                .Select(item => item.GetCategory())
                                .Distinct()
                                .OrderBy(c => c);

                            foreach (var category in kaCategories)
                            {
                                int count = kaDatabase.Items.Count(item => item.GetCategory() == category);
                                DrawCategorySettingRow(category, count, _categoryAssetTypes, SaveCategoryAssetType, false);
                            }
                        }
                        EditorGUILayout.Space(10);
                    }

                    if (boothlmDatabase != null)
                    {
                        _showBOOTHLMCategories = EditorGUILayout.Foldout(
                            _showBOOTHLMCategories,
                            "BOOTHLM Categories",
                            true,
                            GUIStyleManager.Foldout
                        );

                        if (_showBOOTHLMCategories)
                        {
                            var categories = boothlmDatabase.Items
                                .Select(item => item.CategoryName)
                                .Distinct()
                                .OrderBy(c => c);

                            foreach (var category in categories)
                            {
                                if (!_boothlmCategoryAssetTypes.ContainsKey(category))
                                {
                                    _boothlmCategoryAssetTypes[category] = GetDefaultBOOTHLMAssetType(category);
                                }

                                var items = boothlmDatabase.Items.Where(i => i.CategoryName == category).ToList();
                                DrawCategorySettingRow(category, items.Count, _boothlmCategoryAssetTypes, SaveBOOTHLMCategoryAssetType);
                            }
                        }
                    }

                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();
                }
            }

            // フォルダサムネイル設定セクション
            EditorGUILayout.Space(10);

            _showFolderThumbnailSettings = EditorGUILayout.Foldout(
                _showFolderThumbnailSettings,
                LocalizationService.Instance.GetString("folder_thumbnail_settings"),
                true,
                GUIStyleManager.Foldout
            );

            if (_showFolderThumbnailSettings)
            {
                EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);

                // フォルダサムネイルを表示する
                bool showFolderThumbnail = EditorPrefs.GetBool(
                    PREFS_KEY_SHOW_FOLDER_THUMBNAIL,
                    true
                );

                bool newShowFolderThumbnail = EditorGUILayout.ToggleLeft(
                    LocalizationService.Instance.GetString("show_folder_thumbnail"),
                    showFolderThumbnail,
                    GUIStyleManager.Label
                );

                bool newGenerateFolderThumbnail = false;
                if (newShowFolderThumbnail != showFolderThumbnail)
                {
                    EditorPrefs.SetBool(PREFS_KEY_SHOW_FOLDER_THUMBNAIL, newShowFolderThumbnail);
                    FolderIconDrawer.SetEnabled(newShowFolderThumbnail);

                    // 設定ウィンドウ内の変数で判定し、必要ならサムネイルもONに
                    if (newShowFolderThumbnail && !newGenerateFolderThumbnail)
                    {
                        newGenerateFolderThumbnail = true;
                        EditorPrefs.SetBool(PREFS_KEY_GENERATE_FOLDER_THUMBNAIL, true);
                    }
                }

                // フォルダサムネイルを生成する
                bool generateFolderThumbnail = EditorPrefs.GetBool(
                    PREFS_KEY_GENERATE_FOLDER_THUMBNAIL,
                    true
                );

                EditorGUI.BeginDisabledGroup(newShowFolderThumbnail); // ONの間はグレーアウト
                newGenerateFolderThumbnail = EditorGUILayout.ToggleLeft(
                    LocalizationService.Instance.GetString("generate_folder_thumbnail"),
                    generateFolderThumbnail,
                    GUIStyleManager.Label
                );
                EditorGUI.EndDisabledGroup();

                // ON→OFFにしようとしたときのみ警告ダイアログ
                if (!newShowFolderThumbnail && generateFolderThumbnail && !newGenerateFolderThumbnail)
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        LocalizationService.Instance.GetString("warning"),
                        LocalizationService.Instance.GetString("thumbnail_warning_message"),
                        LocalizationService.Instance.GetString("ok"),
                        LocalizationService.Instance.GetString("cancel")
                    );

                    if (confirm)
                    {
                        EditorPrefs.SetBool(PREFS_KEY_GENERATE_FOLDER_THUMBNAIL, false);
                    }
                    else
                    {
                        newGenerateFolderThumbnail = true; // チェックを戻す
                    }
                }
                else if (newGenerateFolderThumbnail != generateFolderThumbnail && !newShowFolderThumbnail)
                {
                    EditorPrefs.SetBool(
                        PREFS_KEY_GENERATE_FOLDER_THUMBNAIL,
                        newGenerateFolderThumbnail
                    );
                }

                // 初期設定領域（トグル式、デフォルト閉じ）
                _showDefaultExcludeFolders = EditorGUILayout.Foldout(
                    _showDefaultExcludeFolders,
                    LocalizationService.Instance.GetString("default_exclude_folders"),
                    true,
                    GUIStyleManager.Foldout
                );

                if (_showDefaultExcludeFolders)
                {
                    EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);
                    foreach (var def in _allDefaultExcludeFolders)
                    {
                        EditorGUILayout.BeginHorizontal();
                        bool isOn = _enabledDefaultExcludeFolders.Contains(def);
                        bool newIsOn = EditorGUILayout.ToggleLeft(def, isOn, GUIStyleManager.Label);
                        if (newIsOn != isOn)
                        {
                            if (newIsOn)
                            {
                                _enabledDefaultExcludeFolders.Add(def);
                            }
                            else
                            {
                                _enabledDefaultExcludeFolders.Remove(def);
                            }

                            SaveExcludeFoldersAndCombined();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();
                }

                // ユーザー追加領域
                EditorGUILayout.LabelField(LocalizationService.Instance.GetString("user_exclude_folders"), GUIStyleManager.BoldLabel);
                EditorGUILayout.HelpBox(LocalizationService.Instance.GetString("exclude_folder_help"), MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                GUI.SetNextControlName("NewExcludeFolderField");
                _newExcludeFolder = EditorGUILayout.TextField(
                    LocalizationService.Instance.GetString("add_exclude_folder"),
                    _newExcludeFolder,
                    GUIStyleManager.TextField
                );

                bool shouldAdd = false;

                // エンターキー対応
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "NewExcludeFolderField")
                {
                    shouldAdd = true;
                    Event.current.Use();
                }

                if (GUILayout.Button(LocalizationService.Instance.GetString("add"), GUIStyleManager.Button, GUILayout.Width(60)))
                {
                    shouldAdd = true;
                }

                if (shouldAdd)
                {
                    if (!string.IsNullOrEmpty(_newExcludeFolder))
                    {
                        if (!_userExcludeFolders.Contains(_newExcludeFolder) && !_allDefaultExcludeFolders.Contains(_newExcludeFolder))
                        {
                            _userExcludeFolders.Insert(0, _newExcludeFolder); // 先頭に追加
                            SaveExcludeFoldersAndCombined();
                        }
                    }
                    
                    _newExcludeFolder = "";
                }
                
                EditorGUILayout.EndHorizontal();

                // ユーザー追加分リスト（上から順に）
                float userListMaxHeight = 300f;
                _excludeFoldersScrollPosition = EditorGUILayout.BeginScrollView(
                    _excludeFoldersScrollPosition,
                    GUILayout.Height(
                        Mathf.Min(_userExcludeFolders.Count * 28 + 10, userListMaxHeight)
                    )
                );

                for (int i = 0; i < _userExcludeFolders.Count; i++)
                {
                    EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(_userExcludeFolders[i], GUIStyleManager.Label);

                    if (GUILayout.Button("削除", GUIStyleManager.Button, GUILayout.Width(60)))
                    {
                        _userExcludeFolders.RemoveAt(i);
                        SaveExcludeFoldersAndCombined();
                        i--;
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
            }

            // インポート設定セクション
            EditorGUILayout.Space(10);

            _showImportSettings = EditorGUILayout.Foldout(
                _showImportSettings,
                LocalizationService.Instance.GetString("import_settings"),
                true,
                GUIStyleManager.Foldout
            );

            if (_showImportSettings)
            {
                EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);

                // ダイアログ表示設定
                bool showImportDialog = EditorPrefs.GetBool(PREFS_KEY_SHOW_IMPORT_DIALOG, true);
                bool newShowImportDialog = EditorGUILayout.ToggleLeft(
                    LocalizationService.Instance.GetString("show_import_dialog"),
                    showImportDialog,
                    GUIStyleManager.Label
                );

                if (newShowImportDialog != showImportDialog)
                {
                    EditorPrefs.SetBool(PREFS_KEY_SHOW_IMPORT_DIALOG, newShowImportDialog);
                }

                EditorGUILayout.HelpBox(LocalizationService.Instance.GetString("dialog_info"), MessageType.Info);

                EditorGUILayout.Space(5);

                // カテゴリフォルダインポート設定
                bool importToCategoryFolder = EditorPrefs.GetBool(PREFS_KEY_IMPORT_TO_CATEGORY_FOLDER, false);
                bool newValue = EditorGUILayout.ToggleLeft(
                    LocalizationService.Instance.GetString("import_to_category_folder_long"),
                    importToCategoryFolder,
                    GUIStyleManager.Label
                );

                EditorGUILayout.HelpBox(LocalizationService.Instance.GetString("import_warning"), MessageType.Warning);
                EditorGUILayout.HelpBox(LocalizationService.Instance.GetString("import_info"), MessageType.Info);

                if (newValue != importToCategoryFolder)
                {
                    EditorPrefs.SetBool(PREFS_KEY_IMPORT_TO_CATEGORY_FOLDER, newValue);
                }

                EditorGUILayout.EndVertical();
            }
        }

        /// <summary>
        /// データベースパス設定フィールドを描画
        /// </summary>
        /// <param name="label">フィールドのラベル</param>
        /// <param name="path">現在のパス</param>
        /// <param name="onPathChanged">パスが変更された時のコールバック</param>
        private void DrawDatabasePathField(string label, string path, Action<string> onPathChanged)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUIStyleManager.Label, GUILayout.Width(140));

            // パスを編集不可のテキストフィールドとして表示
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(path, GUIStyleManager.TextField);
            EditorGUI.EndDisabledGroup();

            // 削除ボタン
            if (!string.IsNullOrEmpty(path) && GUILayout.Button(LocalizationService.Instance.GetString("remove"), GUIStyleManager.Button, GUILayout.Width(60)))
            {
                onPathChanged("");
                if (label == "AE Database Path:" || label == "BOOTHLM Data Path:") InitializeCategoryAssetTypes();
            }

            // 参照ボタン
            if (GUILayout.Button(LocalizationService.Instance.GetString("browse"), GUIStyleManager.Button, GUILayout.Width(60)))
            {
                var selectedPath = EditorUtility.OpenFolderPanel(
                    LocalizationService.Instance.GetString("select_directory"),
                    "",
                    ""
                );

                if (!string.IsNullOrEmpty(selectedPath))
                {
                    onPathChanged(selectedPath);
                    if (label == "AE Database Path:" || label == "BOOTHLM Data Path:") InitializeCategoryAssetTypes();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// カテゴリ設定の行を描画する
        /// </summary>
        private void DrawCategorySettingRow(
            string category,
            int itemCount,
            Dictionary<string, int> assetTypesDict,
            Action<string, int> onAssetTypeChanged,
            bool showAssetType = true)
        {
            EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                category,
                GUIStyleManager.BoldLabel,
                GUILayout.Width(200)
            );
            EditorGUILayout.LabelField(string.Format(LocalizationService.Instance.GetString("items_count"), itemCount), GUIStyleManager.Label);
            EditorGUILayout.EndHorizontal();

            // アセットタイプの選択
            if (showAssetType)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(LocalizationService.Instance.GetString("asset_type"), GUIStyleManager.Label, GUILayout.Width(150));

                if (!assetTypesDict.ContainsKey(category))
                {
                    assetTypesDict[category] = 0; // Default
                }

                var newValue = EditorGUILayout.Popup(
                    assetTypesDict[category],
                    _assetTypes,
                    GUIStyleManager.Popup,
                    GUILayout.Width(200)
                );

                if (newValue != assetTypesDict[category])
                {
                    assetTypesDict[category] = newValue;
                    onAssetTypeChanged(category, newValue);
                    OnSettingsChanged?.Invoke();
                }

                EditorGUILayout.EndHorizontal();
            }

            // カテゴリフォルダ名の設定
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationService.Instance.GetString("import_folder_name"), GUIStyleManager.Label, GUILayout.Width(150));
            string folderNameKey = PREFS_KEY_CATEGORY_FOLDER_NAME_PREFIX + category;
            string currentFolderName = EditorPrefs.GetString(folderNameKey, category);
            string newFolderName = EditorGUILayout.TextField(currentFolderName, GUIStyleManager.TextField, GUILayout.Width(200));
            if (newFolderName != currentFolderName)
            {
                EditorPrefs.SetString(folderNameKey, newFolderName);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 配列内で指定された値に最も近い値のインデックスを取得する
        /// </summary>
        private int GetClosestIndex(int[] values, int target)
        {
            int closestIndex = 0;
            int minDiff = int.MaxValue;
            for (int i = 0; i < values.Length; i++)
            {
                int diff = Math.Abs(values[i] - target);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestIndex = i;
                }
            }
            return closestIndex;
        }
    }
}
