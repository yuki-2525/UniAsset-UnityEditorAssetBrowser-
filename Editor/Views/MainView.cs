// Copyright (c) 2025 sakurayuki

#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEditorAssetBrowser.Helper;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Services;
using UnityEditorAssetBrowser.ViewModels;
using UnityEngine;

namespace UnityEditorAssetBrowser.Views
{
    /// <summary>
    /// メインウィンドウの表示を管理するビュー
    /// アバター、アバター関連アセット、ワールドアセット、その他のタブ切り替えと表示を制御する
    /// </summary>
    public class MainView
    {
        /// <summary>検索のViewModel</summary>
        private readonly SearchViewModel _searchViewModel;

        /// <summary>ページネーションのViewModel</summary>
        private readonly PaginationViewModel _paginationViewModel;

        /// <summary>検索ビュー</summary>
        private readonly SearchView _searchView;

        /// <summary>ページネーションビュー</summary>
        private readonly PaginationView _paginationView;

        /// <summary>アセットアイテムビュー</summary>
        private readonly AssetItemView _assetItemView;

        /// <summary>アセットブラウザービューモデル</summary>
        private readonly AssetBrowserViewModel _assetBrowserViewModel;

        /// <summary>スクロール位置</summary>
        private Vector2 _scrollPosition;
        
        /// <summary> アイテムのキャッシュ</summary>
        private List<IDatabaseItem>? _cachedItems = null;

        /// <summary>タブのラベル</summary>
        private static readonly string[] Tabs =
        {
            "アバター",
            "アバター関連アセット",
            "ワールドアセット",
            "その他",
        };

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="searchViewModel">検索のViewModel</param>
        /// <param name="paginationViewModel">ページネーションのViewModel</param>
        /// <param name="searchView">検索ビュー</param>
        /// <param name="paginationView">ページネーションビュー</param>
        public MainView(
            SearchViewModel searchViewModel,
            PaginationViewModel paginationViewModel,
            SearchView searchView,
            PaginationView paginationView,
            AssetItemView assetItemView,
            AssetBrowserViewModel assetBrowserViewModel

        )
        {
            _searchViewModel = searchViewModel;
            _paginationViewModel = paginationViewModel;
            _searchView = searchView;
            _paginationView = paginationView;
            _assetItemView = assetItemView;
            _assetBrowserViewModel = assetBrowserViewModel;

            _assetBrowserViewModel.SortMethodChanged += () =>
            {
                if (_cachedItems == null) return;
                _cachedItems = _assetBrowserViewModel.SortItems(_cachedItems);
            };

            DatabaseService.OnPathChanged += () =>
            {
                _cachedItems = null;
            };
        }

        /// <summary>
        /// メインウィンドウの描画
        /// </summary>
        public void DrawMainWindow()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(10);

            _searchView.DrawDatabaseButtons();
            DrawTabBar();
            _searchView.DrawSearchField();

            if (Event.current.type == EventType.Used || _cachedItems == null)
            {
                _cachedItems = _searchView.GetSearchResult();
            }

            if (_cachedItems != null)
            {
                DrawSearchResult(_cachedItems);
                DrawContentArea(_cachedItems);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// タブバーの描画
        /// </summary>
        private void DrawTabBar()
        {
            var newTab = GUILayout.SelectionGrid(_paginationViewModel.SelectedTab, Tabs, Tabs.Length, GUIStyleManager.TabButton);
            if (newTab != _paginationViewModel.SelectedTab)
            {
                _paginationViewModel.SelectedTab = newTab;
                _paginationViewModel.ResetPage();
                _searchViewModel.SetCurrentTab(newTab);
                if (EditorWindow.focusedWindow != null) EditorWindow.focusedWindow.Repaint();
            }
            EditorGUILayout.Space(10);
        }

        private void DrawSearchResult(List<IDatabaseItem> totalItems)
        {
            EditorGUILayout.LabelField($"検索結果: {totalItems.Count}件", GUIStyleManager.Label);
            EditorGUILayout.Space(10);
        }

        /// <summary>
        /// コンテンツエリアの描画
        /// </summary>
        private void DrawContentArea(List<IDatabaseItem> totalItems)
        {
            GUILayout.BeginVertical();
            DrawScrollView(totalItems);
            _paginationView.DrawPaginationButtons();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// スクロールビューの描画
        /// </summary>
        private void DrawScrollView(List<IDatabaseItem> totalItems)
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(
                _scrollPosition,
                GUILayout.ExpandHeight(true)
            );
            DrawCurrentTabContent(totalItems);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 現在のタブのコンテンツを描画
        /// </summary>
        private void DrawCurrentTabContent(List<IDatabaseItem> totalItems)
            => ShowContents(totalItems);

        /// <summary>
        /// アバターコンテンツの表示
        /// </summary>
        private void ShowContents(List<IDatabaseItem> totalItems)
        {
            var pageItems = _paginationViewModel.GetCurrentPageItems(totalItems);

            // 表示前に必要な画像のみ読み込み
            ImageServices.Instance.UpdateVisibleImages(pageItems);

            foreach (var item in pageItems)
            {
                _assetItemView.ShowAvatarItem(item);
            }
        }
    }
}
