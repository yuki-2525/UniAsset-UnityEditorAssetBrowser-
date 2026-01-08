// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEditorAssetBrowser.Helper;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Services;
using UnityEditorAssetBrowser.ViewModels;
using UnityEditorAssetBrowser.Windows;
using UnityEngine;

namespace UnityEditorAssetBrowser.Views
{
    public class SearchView
    {
        private readonly SearchViewModel _searchViewModel;
        private readonly AssetBrowserViewModel _assetBrowserViewModel;
        private readonly PaginationViewModel _paginationViewModel;
        private readonly AssetItemView _assetItemView;
        private int _lastSelectedTab = -1; // 前回選択されていたタブを記録
        
        // 入力状態管理用
        private readonly Dictionary<string, string> _inputValues = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _committedValues = new Dictionary<string, string>();

        public SearchView(
            SearchViewModel searchViewModel,
            AssetBrowserViewModel assetBrowserViewModel,
            PaginationViewModel paginationViewModel,
            AssetItemView assetItemView
        )
        {
            _searchViewModel = searchViewModel;
            _assetBrowserViewModel = assetBrowserViewModel;
            _paginationViewModel = paginationViewModel;
            _assetItemView = assetItemView;
        }

        public void DrawSearchField()
        {
            // タブが変更された場合の処理
            CheckTabChange();

            EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);

            // 基本検索フィールド
            EditorGUILayout.BeginHorizontal();
            var searchLabel = LocalizationService.Instance.GetString("search") + ":";
            var searchLabelWidth = GUIStyleManager.Label.CalcSize(new GUIContent(searchLabel)).x + 5;
            EditorGUILayout.LabelField(searchLabel, GUIStyleManager.Label, GUILayout.Width(searchLabelWidth));
            
            DrawSearchInput(
                _searchViewModel.SearchCriteria.SearchQuery,
                val => _searchViewModel.SearchCriteria.SearchQuery = val,
                "SearchQuery"
            );

            // 詳細検索のトグル
            var advancedSearchLabel = LocalizationService.Instance.GetString("advanced_search");
            var advancedSearchWidth = GUIStyleManager.Label.CalcSize(new GUIContent(advancedSearchLabel)).x + 25; // Toggle needs more space
            var newShowAdvancedSearch = EditorGUILayout.ToggleLeft(
                advancedSearchLabel,
                _searchViewModel.SearchCriteria.ShowAdvancedSearch,
                GUIStyleManager.Label,
                GUILayout.Width(advancedSearchWidth)
            );
            if (newShowAdvancedSearch != _searchViewModel.SearchCriteria.ShowAdvancedSearch)
            {
                DebugLogger.Log($"Advanced search toggle: {newShowAdvancedSearch}");
                _searchViewModel.SearchCriteria.ShowAdvancedSearch = newShowAdvancedSearch;
                _paginationViewModel.ResetPage();
                GUI.changed = true;
            }

            // クリアボタン
            var clearLabel = LocalizationService.Instance.GetString("clear");
            var clearWidth = GUIStyleManager.Button.CalcSize(new GUIContent(clearLabel)).x + 10;
            if (GUILayout.Button(clearLabel, GUIStyleManager.Button, GUILayout.Width(clearWidth)))
            {
                DebugLogger.Log("Clear search button clicked");
                _searchViewModel.ClearSearchCriteria();
                _paginationViewModel.ResetPage();
                OnSearchResultChanged();
                GUI.changed = true;
            }

            // ソートボタン
            var sortLabel = LocalizationService.Instance.GetString("sort_order");
            var sortWidth = GUIStyleManager.Button.CalcSize(new GUIContent(sortLabel)).x + 10;
            if (GUILayout.Button(sortLabel, GUIStyleManager.Button, GUILayout.Width(sortWidth)))
            {
                var menu = new GenericMenu();
                menu.AddItem(
                    new GUIContent(LocalizationService.Instance.GetString("sort_updated_desc")),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.UpdatedDateDesc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.UpdatedDateDesc
                        )
                );
                menu.AddItem(
                    new GUIContent(LocalizationService.Instance.GetString("sort_updated_asc")),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.UpdatedDateAsc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.UpdatedDateAsc
                        )
                );
                menu.AddItem(
                    new GUIContent(LocalizationService.Instance.GetString("sort_created_desc")),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.CreatedDateDesc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.CreatedDateDesc
                        )
                );
                menu.AddItem(
                    new GUIContent(LocalizationService.Instance.GetString("sort_created_asc")),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.CreatedDateAsc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.CreatedDateAsc
                        )
                );
                menu.AddItem(
                    new GUIContent(LocalizationService.Instance.GetString("sort_title_asc")),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.TitleAsc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.TitleAsc
                        )
                );
                menu.AddItem(
                    new GUIContent(LocalizationService.Instance.GetString("sort_title_desc")),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.TitleDesc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.TitleDesc
                        )
                );
                menu.AddItem(
                    new GUIContent(LocalizationService.Instance.GetString("sort_author_asc")),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.AuthorAsc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.AuthorAsc
                        )
                );
                menu.AddItem(
                    new GUIContent(LocalizationService.Instance.GetString("sort_author_desc")),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.AuthorDesc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.AuthorDesc
                        )
                );
                menu.AddItem(
                    new GUIContent(LocalizationService.Instance.GetString("sort_booth_id_desc")),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.BoothIdDesc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.BoothIdDesc
                        )
                );
                menu.AddItem(
                    new GUIContent(LocalizationService.Instance.GetString("sort_booth_id_asc")),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.BoothIdAsc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.BoothIdAsc
                        )
                );
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();

            // 詳細検索フィールド
            if (_searchViewModel.SearchCriteria.ShowAdvancedSearch)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);

                float labelWidth = 100f; // Default width, maybe scale it?
                // Let's calculate max label width for alignment if we want to be fancy, but fixed width is probably fine for now if it's wide enough.
                // "対応アバター:" is the longest label.
                var maxLabel = "対応アバター:";
                labelWidth = Mathf.Max(100f, GUIStyleManager.Label.CalcSize(new GUIContent(maxLabel)).x + 5);

                // タイトル検索
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("タイトル:", GUIStyleManager.Label, GUILayout.Width(labelWidth));
                DrawSearchInput(
                    _searchViewModel.SearchCriteria.TitleSearch,
                    val => _searchViewModel.SearchCriteria.TitleSearch = val,
                    "TitleSearch"
                );
                EditorGUILayout.EndHorizontal();

                // 作者名検索
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("作者名:", GUIStyleManager.Label, GUILayout.Width(labelWidth));
                DrawSearchInput(
                    _searchViewModel.SearchCriteria.AuthorSearch,
                    val => _searchViewModel.SearchCriteria.AuthorSearch = val,
                    "AuthorSearch"
                );
                EditorGUILayout.EndHorizontal();

                // カテゴリ検索（アバタータブ以外で表示）
                if (_paginationViewModel.SelectedTab != 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("カテゴリ:", GUIStyleManager.Label, GUILayout.Width(labelWidth));
                    DrawSearchInput(
                        _searchViewModel.SearchCriteria.CategorySearch,
                        val => _searchViewModel.SearchCriteria.CategorySearch = val,
                        "CategorySearch"
                    );
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("対応アバター:", GUIStyleManager.Label, GUILayout.Width(labelWidth));
                    DrawSearchInput(
                        _searchViewModel.SearchCriteria.SupportedAvatarsSearch,
                        val => _searchViewModel.SearchCriteria.SupportedAvatarsSearch = val,
                        "SupportedAvatarsSearch"
                    );
                    EditorGUILayout.EndHorizontal();
                }

                // タグ検索
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("タグ:", GUIStyleManager.Label, GUILayout.Width(labelWidth));
                DrawSearchInput(
                    _searchViewModel.SearchCriteria.TagsSearch,
                    val => _searchViewModel.SearchCriteria.TagsSearch = val,
                    "TagsSearch"
                );
                EditorGUILayout.EndHorizontal();

                // メモ検索
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("メモ:", GUIStyleManager.Label, GUILayout.Width(labelWidth));
                DrawSearchInput(
                    _searchViewModel.SearchCriteria.MemoSearch,
                    val => _searchViewModel.SearchCriteria.MemoSearch = val,
                    "MemoSearch"
                );
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        public List<IDatabaseItem> GetSearchResult()
        {
            List<IDatabaseItem> totalItems = _paginationViewModel.GetCurrentTabItems(
                () => _assetBrowserViewModel.GetFilteredAvatars(),
                () => _assetBrowserViewModel.GetFilteredItems(),
                () => _assetBrowserViewModel.GetFilteredWorldObjects(),
                () => _assetBrowserViewModel.GetFilteredOthers()
            );

            return totalItems;
        }

        public void DrawDatabaseButtons()
        {
            EditorGUILayout.BeginHorizontal();

            // 言語選択
            GUILayout.Label("language :", GUIStyleManager.Label, GUILayout.ExpandWidth(false));
            var languages = LocalizationService.Instance.AvailableLanguages;
            var currentLang = LocalizationService.Instance.CurrentLanguage;
            var currentIndex = System.Array.IndexOf(languages, currentLang);
            if (currentIndex < 0) currentIndex = 0;
            
            var newIndex = EditorGUILayout.Popup(currentIndex, languages, GUILayout.Width(80));
            if (newIndex != currentIndex && newIndex >= 0 && newIndex < languages.Length)
            {
                LocalizationService.Instance.CurrentLanguage = languages[newIndex];
            }

            GUILayout.FlexibleSpace();

            // このソフトについてボタン
            if (GUILayout.Button(LocalizationService.Instance.GetString("about"), GUIStyleManager.Button))
            {
                AboutWindow.ShowWindow();
            }

            if (GUILayout.Button(LocalizationService.Instance.GetString("open_import_list"), GUIStyleManager.Button))
            {
                ImportQueueWindow.ShowWindow();
            }

            if (GUILayout.Button(LocalizationService.Instance.GetString("settings"), GUIStyleManager.Button))
            {
                SettingsWindow.ShowWindow(
                    _assetBrowserViewModel,
                    _searchViewModel,
                    _paginationViewModel
                );
            }

            if (GUILayout.Button(LocalizationService.Instance.GetString("update_database"), GUIStyleManager.Button))
            {
                // データベースを更新
                DatabaseService.LoadAEDatabase();
                DatabaseService.LoadKADatabase();
                DatabaseService.LoadBOOTHLMDatabase();
                _searchViewModel.SetCurrentTab(_paginationViewModel.SelectedTab);
                _assetItemView.ResetUnitypackageCache();
                HandleUtility.Repaint();
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
        }

        /// <summary>
        /// 検索結果が変更された時の処理
        /// 画像キャッシュを新しい表示アイテムに更新する
        /// </summary>
        private void OnSearchResultChanged()
        {
            // 現在のタブの新しい検索結果を取得
            var filteredItems = GetCurrentTabFilteredItems();
            var sortedItems = _assetBrowserViewModel.SortItems(filteredItems);
            var pageItems = _paginationViewModel.GetCurrentPageItems(sortedItems);

            // 検索結果数に応じてキャッシュサイズを調整
            ImageServices.Instance.AdaptCacheSizeToSearchResults(filteredItems.Count);

            // 画像キャッシュを新しい表示アイテムに更新
            ImageServices.Instance.UpdateVisibleImages(pageItems);
        }

        /// <summary>
        /// 現在のタブのフィルターされたアイテムを取得
        /// </summary>
        private List<IDatabaseItem> GetCurrentTabFilteredItems()
        {
            return _paginationViewModel.SelectedTab switch
            {
                0 => _assetBrowserViewModel.GetFilteredAvatars(),
                1 => _assetBrowserViewModel.GetFilteredItems(),
                2 => _assetBrowserViewModel.GetFilteredWorldObjects(),
                3 => _assetBrowserViewModel.GetFilteredOthers(),
                _ => new List<IDatabaseItem>()
            };
        }

        /// <summary>
        /// タブの変更をチェックし、変更された場合は検索欄をリセット
        /// </summary>
        private void CheckTabChange()
        {
            int currentTab = _paginationViewModel.SelectedTab;

            // タブが変更された場合
            if (_lastSelectedTab != -1 && _lastSelectedTab != currentTab)
            {
                // 検索条件をクリア
                _searchViewModel.ClearSearchCriteria();
                _paginationViewModel.ResetPage();
                OnSearchResultChanged();
                GUI.changed = true;
            }

            _lastSelectedTab = currentTab;
        }

        private void DrawSearchInput(string currentValue, System.Action<string> onValueChanged, string controlName)
        {
            bool autoSearch = EditorPrefs.GetBool("UnityEditorAssetBrowser_AutoSearch", true);
            
            // 初期化または外部変更の検知
            if (!_inputValues.ContainsKey(controlName) || 
                (_committedValues.ContainsKey(controlName) && _committedValues[controlName] != currentValue))
            {
                _inputValues[controlName] = currentValue;
                _committedValues[controlName] = currentValue;
            }

            GUI.SetNextControlName(controlName);

            // Enterキーの検出（TextField描画前に行うことで、TextFieldによるイベント消費の影響を避ける）
            bool enterPressed = Event.current.type == EventType.KeyDown && 
                               (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && 
                               GUI.GetNameOfFocusedControl() == controlName;

            var newValue = EditorGUILayout.TextField(_inputValues[controlName], GUIStyleManager.TextField);
            
            // 入力値を更新
            if (newValue != _inputValues[controlName])
            {
                _inputValues[controlName] = newValue;
            }

            // 自動検索ONの場合、値が変わったら即座にコミットして検索
            if (autoSearch && newValue != currentValue)
            {
                onValueChanged(newValue);
                _committedValues[controlName] = newValue;
                _paginationViewModel.ResetPage();
                OnSearchResultChanged();
                GUI.changed = true;
            }
            // 自動検索OFFの場合、Enterキーでコミットして検索
            else if (!autoSearch && enterPressed)
            {
                onValueChanged(_inputValues[controlName]);
                _committedValues[controlName] = _inputValues[controlName];
                _paginationViewModel.ResetPage();
                OnSearchResultChanged();
                GUI.changed = true;
                Event.current.Use();
            }
        }
    }
}
