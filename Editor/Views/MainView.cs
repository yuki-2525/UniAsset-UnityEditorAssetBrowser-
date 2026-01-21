// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorAssetBrowser.Helper;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Models;
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

            // ページ切り替え時にスクロールを先頭へ戻す
            _paginationViewModel.OnPageChanged += () => { _scrollPosition = Vector2.zero; };

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
            // DebugLogger.Log("DrawMainWindow"); // Too frequent, but good for tracing repaint performance issues. Keeping commented out.

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
            bool hasListTab = !string.IsNullOrEmpty(DatabaseService.GetBOOTHLMDataPath());

            // 表示用インデックスと実際のタブIDを分離して管理
            var tabIds = new List<int> { 0, 1, 2, 3 };
            var tabs = new List<string>
            {
                LocalizationService.Instance.GetString("tab_avatar"),
                LocalizationService.Instance.GetString("tab_avatar_assets"),
                LocalizationService.Instance.GetString("tab_world_assets"),
                LocalizationService.Instance.GetString("tab_others"),
            };

            if (hasListTab)
            {
                tabIds.Add(4);
                tabs.Add(_assetBrowserViewModel.CurrentList != null
                    ? _assetBrowserViewModel.CurrentList.Title
                    : LocalizationService.Instance.GetString("tab_list"));
            }

            int currentDisplayIndex = tabIds.IndexOf(_paginationViewModel.SelectedTab);
            if (currentDisplayIndex < 0) currentDisplayIndex = 0; // 不正値は先頭に戻す

            int newDisplayIndex = GUILayout.SelectionGrid(currentDisplayIndex, tabs.ToArray(), tabs.Count, GUIStyleManager.TabButton);
            int newTab = tabIds[newDisplayIndex];

            if (newTab != _paginationViewModel.SelectedTab)
            {
                DebugLogger.Log($"Tab switched: {_paginationViewModel.SelectedTab} -> {newTab}");
                
                // リストタブ以外をクリックした場合は、選択中リストをリセット（オプション）
                // ここではリセットせず、リストタブに戻った時に再表示できるようにする
                // ただしリストタブ(4)を選択した時、リスト未選択ならリスト一覧に戻る挙動は MainView.DrawCurrentTabContent で制御される

                _paginationViewModel.SelectedTab = newTab;
                _paginationViewModel.ResetPage();
                _searchViewModel.SetCurrentTab(newTab);
                if (EditorWindow.focusedWindow != null) EditorWindow.focusedWindow.Repaint();
            }
            EditorGUILayout.Space(10);
        }

        private void DrawSearchResult(List<IDatabaseItem> totalItems)
        {
            // リスト選択画面では件数表示をスキップ（混乱を避けるため）
            // リスト一覧はタブインデックス4
            if (_paginationViewModel.SelectedTab == 4 && _assetBrowserViewModel.CurrentList == null)
            {
                EditorGUILayout.Space(10);
                return;
            }

            EditorGUILayout.LabelField(string.Format(LocalizationService.Instance.GetString("search_result_count"), totalItems.Count), GUIStyleManager.Label);
            EditorGUILayout.Space(10);
        }

        /// <summary>
        /// コンテンツエリアの描画
        /// </summary>
        private void DrawContentArea(List<IDatabaseItem> totalItems)
        {
            GUILayout.BeginVertical();
            DrawScrollView(totalItems);
            
            // リスト選択画面ではページネーションを表示しない
            if (!(_paginationViewModel.SelectedTab == 4 && _assetBrowserViewModel.CurrentList == null))
            {
                _paginationView.DrawPaginationButtons();
            }
            
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
        {
            if (_paginationViewModel.SelectedTab == 4) // リストタブのインデックス
            {
                if (_assetBrowserViewModel.CurrentList == null)
                {
                    DrawListSelectionView();
                    return;
                }
                else
                {
                    if (GUILayout.Button("← " + LocalizationService.Instance.GetString("list_back"), GUIStyleManager.Button, GUILayout.Width(150)))
                    {
                        _assetBrowserViewModel.CurrentList = null;
                        _paginationViewModel.ResetPage();
                        _searchViewModel.ClearSearchCriteria(); // クエリをクリアして全リスト表示に戻す（オプション）
                        _cachedItems = null; 
                        if (EditorWindow.focusedWindow != null) EditorWindow.focusedWindow.Repaint();
                        return;
                    }
                    EditorGUILayout.Space(5);
                }
            }
            ShowContents(totalItems);
        }

        private void DrawListSelectionView()
        {
            var lists = DatabaseService.GetBOOTHLMLists();
            string filterText = _searchViewModel.SearchCriteria.SearchQuery;
            
            var filteredLists = string.IsNullOrEmpty(filterText) 
                ? lists 
                : lists.Where(l => l.Title.Contains(filterText, System.StringComparison.OrdinalIgnoreCase)).ToList();

            if (filteredLists.Count == 0)
            {
                EditorGUILayout.HelpBox("No lists found.", MessageType.Info);
                return;
            }

            foreach (var list in filteredLists)
            {
                DrawListSelectionButton(list);
            }
        }

        private void DrawListSelectionButton(BOOTHLMList list)
        {
            // ボタンの矩形領域を確保（高さ100px）
            Rect rect = GUILayoutUtility.GetRect(0, 100, GUIStyleManager.Button, GUILayout.ExpandWidth(true));
            
            // クリック判定
            if (GUI.Button(rect, "", GUIStyleManager.Button))
            {
                _assetBrowserViewModel.CurrentList = list;
                _paginationViewModel.ResetPage();
                _searchViewModel.ClearSearchCriteria();
                _cachedItems = null;
                if (EditorWindow.focusedWindow != null) EditorWindow.focusedWindow.Repaint();
            }

            // コンテンツの描画位置
            float iconSize = 80;
            float padding = 10;
            float startX = rect.x + padding;
            float centerY = rect.y + (rect.height - iconSize) / 2;
            
            // プレビューアイテムを取得してサムネイルを描画
            var result = _assetBrowserViewModel.GetListPreviewItems(list);
            var totalCount = result.TotalCount;
            var previewItems = result.Items;
            
            // 画像のロードをリクエスト
            if (previewItems.Count > 0)
            {
                 ImageServices.Instance.UpdateVisibleImages(previewItems.Cast<IDatabaseItem>().ToList());
            }

            // 描画設定
            int maxStack = 5;
            float overlapOffset = 20f;
            float totalStackWidth = iconSize + (Mathf.Min(previewItems.Count, maxStack) - 1) * overlapOffset;
            if (previewItems.Count <= 1) totalStackWidth = iconSize;
            
            // アイコン領域の幅 (最大幅)
            float fixedIconAreaWidth = iconSize + (maxStack - 1) * overlapOffset; // 80 + 4 * 20 = 160
            
            // 中央寄せのための開始X座標計算
            float currentStacksStartX = startX + (fixedIconAreaWidth - totalStackWidth) / 2;

            int drawCount = Mathf.Min(previewItems.Count, maxStack);
            
            // 左(i=0)が手前、右に行くにつれ奥(i=4)
            // 奥のもの(indexが大きいもの)から描画する
            for (int i = drawCount - 1; i >= 0; i--)
            {
                var item = previewItems[i];
                var tex = ImageServices.Instance.LoadTexture(item.GetImagePath());
                
                // 左(0)から右(4)へずらす
                float xOffset = i * overlapOffset;
                
                Rect iconRect = new Rect(currentStacksStartX + xOffset, centerY, iconSize, iconSize);
                
                if (tex != null)
                {
                    GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
                }
                else
                {
                    // No Image placeholder
                    GUI.Box(iconRect, "No Image", GUIStyleManager.BoxStyle);
                }
            }

            // タイトルと情報の表示
            float textStartX = startX + fixedIconAreaWidth + 20;

            string displayName = list.Title;
            if (list.Type == BOOTHLMListType.Smart) displayName += " [Smart]";
            
            Rect titleRect = new Rect(textStartX, rect.y + 10, rect.width - textStartX - padding, 30);
            GUI.Label(titleRect, displayName, GUIStyleManager.BoldLabel);
            
            // リストの右端にアイテム数を表示
            string countText = $"{totalCount} items";
            Vector2 countSize = GUIStyleManager.Label.CalcSize(new GUIContent(countText));
            Rect countRect = new Rect(rect.width - countSize.x - padding - 5, rect.y + (rect.height - countSize.y) / 2, countSize.x, countSize.y);
            GUI.Label(countRect, countText, GUIStyleManager.Label);
            
            Rect infoRect = new Rect(textStartX, rect.y + 40, rect.width - textStartX - padding - countSize.x - 20, 20);
            GUI.Label(infoRect, list.Description, GUIStyleManager.Label);
            
            // マウスオーバー時のハイライト等はGUI.Buttonがやってくれる
        }

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
