// Copyright (c) 2025 sakurayuki

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
            var searchLabel = "検索:";
            var searchLabelWidth = GUIStyleManager.Label.CalcSize(new GUIContent(searchLabel)).x + 5;
            EditorGUILayout.LabelField(searchLabel, GUIStyleManager.Label, GUILayout.Width(searchLabelWidth));
            
            var newSearchQuery = EditorGUILayout.TextField(
                _searchViewModel.SearchCriteria.SearchQuery,
                GUIStyleManager.TextField
            );
            if (newSearchQuery != _searchViewModel.SearchCriteria.SearchQuery)
            {
                _searchViewModel.SearchCriteria.SearchQuery = newSearchQuery;
                _paginationViewModel.ResetPage();
                OnSearchResultChanged();
                GUI.changed = true;
            }

            // 詳細検索のトグル
            var advancedSearchLabel = "詳細検索";
            var advancedSearchWidth = GUIStyleManager.Label.CalcSize(new GUIContent(advancedSearchLabel)).x + 25; // Toggle needs more space
            var newShowAdvancedSearch = EditorGUILayout.ToggleLeft(
                advancedSearchLabel,
                _searchViewModel.SearchCriteria.ShowAdvancedSearch,
                GUIStyleManager.Label,
                GUILayout.Width(advancedSearchWidth)
            );
            if (newShowAdvancedSearch != _searchViewModel.SearchCriteria.ShowAdvancedSearch)
            {
                _searchViewModel.SearchCriteria.ShowAdvancedSearch = newShowAdvancedSearch;
                _paginationViewModel.ResetPage();
                GUI.changed = true;
            }

            // クリアボタン
            var clearLabel = "クリア";
            var clearWidth = GUIStyleManager.Button.CalcSize(new GUIContent(clearLabel)).x + 10;
            if (GUILayout.Button(clearLabel, GUIStyleManager.Button, GUILayout.Width(clearWidth)))
            {
                _searchViewModel.ClearSearchCriteria();
                _paginationViewModel.ResetPage();
                OnSearchResultChanged();
                GUI.changed = true;
            }

            // ソートボタン
            var sortLabel = "▼ 表示順";
            var sortWidth = GUIStyleManager.Button.CalcSize(new GUIContent(sortLabel)).x + 10;
            if (GUILayout.Button(sortLabel, GUIStyleManager.Button, GUILayout.Width(sortWidth)))
            {
                var menu = new GenericMenu();
                menu.AddItem(
                    new GUIContent("追加順（新しい順）"),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.CreatedDateDesc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.CreatedDateDesc
                        )
                );
                menu.AddItem(
                    new GUIContent("追加順（古い順）"),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.CreatedDateAsc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.CreatedDateAsc
                        )
                );
                menu.AddItem(
                    new GUIContent("アセット名（A-Z順）"),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.TitleAsc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.TitleAsc
                        )
                );
                menu.AddItem(
                    new GUIContent("アセット名（Z-A順）"),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.TitleDesc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.TitleDesc
                        )
                );
                menu.AddItem(
                    new GUIContent("ショップ名（A-Z順）"),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.AuthorAsc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.AuthorAsc
                        )
                );
                menu.AddItem(
                    new GUIContent("ショップ名（Z-A順）"),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.AuthorDesc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.AuthorDesc
                        )
                );
                menu.AddItem(
                    new GUIContent("Booth Id順（新しい順）"),
                    _assetBrowserViewModel.CurrentSortMethod
                        == AssetBrowserViewModel.SortMethod.BoothIdDesc,
                    () =>
                        _assetBrowserViewModel.SetSortMethod(
                            AssetBrowserViewModel.SortMethod.BoothIdDesc
                        )
                );
                menu.AddItem(
                    new GUIContent("Booth Id順（古い順）"),
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
                var newTitleSearch = EditorGUILayout.TextField(
                    _searchViewModel.SearchCriteria.TitleSearch,
                    GUIStyleManager.TextField
                );
                if (newTitleSearch != _searchViewModel.SearchCriteria.TitleSearch)
                {
                    _searchViewModel.SearchCriteria.TitleSearch = newTitleSearch;
                    _paginationViewModel.ResetPage();
                    OnSearchResultChanged();
                    GUI.changed = true;
                }
                EditorGUILayout.EndHorizontal();

                // 作者名検索
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("作者名:", GUIStyleManager.Label, GUILayout.Width(labelWidth));
                var newAuthorSearch = EditorGUILayout.TextField(
                    _searchViewModel.SearchCriteria.AuthorSearch,
                    GUIStyleManager.TextField
                );
                if (newAuthorSearch != _searchViewModel.SearchCriteria.AuthorSearch)
                {
                    _searchViewModel.SearchCriteria.AuthorSearch = newAuthorSearch;
                    _paginationViewModel.ResetPage();
                    OnSearchResultChanged();
                    GUI.changed = true;
                }
                EditorGUILayout.EndHorizontal();

                // カテゴリ検索（アバタータブ以外で表示）
                if (_paginationViewModel.SelectedTab != 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("カテゴリ:", GUIStyleManager.Label, GUILayout.Width(labelWidth));
                    var newCategorySearch = EditorGUILayout.TextField(
                        _searchViewModel.SearchCriteria.CategorySearch,
                        GUIStyleManager.TextField
                    );
                    if (newCategorySearch != _searchViewModel.SearchCriteria.CategorySearch)
                    {
                        _searchViewModel.SearchCriteria.CategorySearch = newCategorySearch;
                        _paginationViewModel.ResetPage();
                        OnSearchResultChanged();
                        GUI.changed = true;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // 対応アバター検索（アイテムタブのみで表示）
                if (_paginationViewModel.SelectedTab == 1)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("対応アバター:", GUIStyleManager.Label, GUILayout.Width(labelWidth));
                    var newSupportedAvatarsSearch = EditorGUILayout.TextField(
                        _searchViewModel.SearchCriteria.SupportedAvatarsSearch,
                        GUIStyleManager.TextField
                    );
                    if (
                        newSupportedAvatarsSearch
                        != _searchViewModel.SearchCriteria.SupportedAvatarsSearch
                    )
                    {
                        _searchViewModel.SearchCriteria.SupportedAvatarsSearch =
                            newSupportedAvatarsSearch;
                        _paginationViewModel.ResetPage();
                        OnSearchResultChanged();
                        GUI.changed = true;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // タグ検索
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("タグ:", GUIStyleManager.Label, GUILayout.Width(labelWidth));

                var newTagsSearch = EditorGUILayout.TextField(
                    _searchViewModel.SearchCriteria.TagsSearch,
                    GUIStyleManager.TextField
                );
                if (newTagsSearch != _searchViewModel.SearchCriteria.TagsSearch)
                {
                    _searchViewModel.SearchCriteria.TagsSearch = newTagsSearch;
                    _paginationViewModel.ResetPage();
                    OnSearchResultChanged();
                    GUI.changed = true;
                }

                EditorGUILayout.EndHorizontal();

                // メモ検索
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("メモ:", GUIStyleManager.Label, GUILayout.Width(labelWidth));

                var newMemoSearch = EditorGUILayout.TextField(
                    _searchViewModel.SearchCriteria.MemoSearch,
                    GUIStyleManager.TextField
                );
                if (newMemoSearch != _searchViewModel.SearchCriteria.MemoSearch)
                {
                    _searchViewModel.SearchCriteria.MemoSearch = newMemoSearch;
                    _paginationViewModel.ResetPage();
                    OnSearchResultChanged();
                    GUI.changed = true;
                }
                
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
            GUILayout.FlexibleSpace();

            // 追加: このソフトについてボタン
            if (GUILayout.Button("このソフトについて", GUIStyleManager.Button))
            {
                AboutWindow.ShowWindow();
            }

            if (GUILayout.Button("設定", GUIStyleManager.Button))
            {
                SettingsWindow.ShowWindow(
                    _assetBrowserViewModel,
                    _searchViewModel,
                    _paginationViewModel
                );
            }

            if (GUILayout.Button("更新", GUIStyleManager.Button))
            {
                // データベースを更新
                DatabaseService.LoadAEDatabase();
                DatabaseService.LoadKADatabase();
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
    }
}
