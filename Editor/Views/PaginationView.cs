// Copyright (c) 2025 sakurayuki

#nullable enable

using UnityEditorAssetBrowser.Helper;
using UnityEditorAssetBrowser.ViewModels;
using UnityEngine;

namespace UnityEditorAssetBrowser.Views
{
    /// <summary>
    /// ページネーションの表示を管理するビュー
    /// 前へ/次へボタンと現在のページ情報を表示する
    /// </summary>
    public class PaginationView
    {
        /// <summary>ページネーションのViewModel</summary>
        private readonly PaginationViewModel _paginationViewModel;

        /// <summary>アセットブラウザーのViewModel</summary>
        private readonly AssetBrowserViewModel _assetBrowserViewModel;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="paginationViewModel">ページネーションのViewModel</param>
        /// <param name="assetBrowserViewModel">アセットブラウザーのViewModel</param>
        public PaginationView(
            PaginationViewModel paginationViewModel,
            AssetBrowserViewModel assetBrowserViewModel
        )
        {
            _paginationViewModel = paginationViewModel;
            _assetBrowserViewModel = assetBrowserViewModel;
        }

        /// <summary>
        /// ページネーションボタンの描画
        /// 前へ/次へボタンと現在のページ情報を表示する
        /// </summary>
        public void DrawPaginationButtons()
        {
            GUILayout.BeginHorizontal();
            DrawPreviousButton();
            DrawPageInfo();
            DrawNextButton();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 前へボタンの描画
        /// </summary>
        private void DrawPreviousButton()
        {
            if (GUILayout.Button("前へ", GUIStyleManager.Button, GUILayout.Width(100)))
            {
                _paginationViewModel.MoveToPreviousPage();
            }
        }

        /// <summary>
        /// ページ情報の描画
        /// </summary>
        private void DrawPageInfo()
        {
            var currentItems = _paginationViewModel.GetCurrentTabItems(
                () => _assetBrowserViewModel.GetFilteredAvatars(),
                () => _assetBrowserViewModel.GetFilteredItems(),
                () => _assetBrowserViewModel.GetFilteredWorldObjects(),
                () => _assetBrowserViewModel.GetFilteredOthers()
            );
            
            int totalPages = _paginationViewModel.GetTotalPages(currentItems);
            GUILayout.Label($"ページ {_paginationViewModel.CurrentPage + 1} / {totalPages}", GUIStyleManager.Label);
        }

        /// <summary>
        /// 次へボタンの描画
        /// </summary>
        private void DrawNextButton()
        {
            if (GUILayout.Button("次へ", GUIStyleManager.Button, GUILayout.Width(100)))
            {
                var currentItems = _paginationViewModel.GetCurrentTabItems(
                    () => _assetBrowserViewModel.GetFilteredAvatars(),
                    () => _assetBrowserViewModel.GetFilteredItems(),
                    () => _assetBrowserViewModel.GetFilteredWorldObjects(),
                    () => _assetBrowserViewModel.GetFilteredOthers()
                );

                int totalPages = _paginationViewModel.GetTotalPages(currentItems);
                _paginationViewModel.MoveToNextPage(totalPages);
            }
        }
    }
}
