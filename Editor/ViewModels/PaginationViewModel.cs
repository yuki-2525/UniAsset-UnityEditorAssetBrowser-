// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System;
using System.Collections.Generic;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Models;
using UnityEditorAssetBrowser.Helper;

namespace UnityEditorAssetBrowser.ViewModels
{
    /// <summary>
    /// ページネーションのビューモデル
    /// UIとPaginationInfoモデルの間の橋渡し役として機能し、ページネーションの制御と表示を管理する
    /// </summary>
    public class PaginationViewModel
    {
        /// <summary>ページネーション情報</summary>
        private readonly PaginationInfo _paginationInfo;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="paginationInfo">ページネーション情報</param>
        public PaginationViewModel(PaginationInfo paginationInfo)
        {
            _paginationInfo = paginationInfo;
        }

        /// <summary>
        /// 現在のページ番号
        /// </summary>
        public int CurrentPage => _paginationInfo.CurrentPage;

        /// <summary>
        /// 選択中のタブ（0: アバター, 1: アイテム, 2: ワールドオブジェクト）
        /// </summary>
        public int SelectedTab
        {
            get => _paginationInfo.SelectedTab;
            set => _paginationInfo.SelectedTab = value;
        }

        /// <summary>
        /// 1ページあたりのアイテム数
        /// </summary>
        public int ItemsPerPage
        {
            get => _paginationInfo.ItemsPerPage;
            set => _paginationInfo.ItemsPerPage = value;
        }

        /// <summary>
        /// 総ページ数を取得
        /// </summary>
        /// <param name="items">アイテムリスト</param>
        /// <returns>総ページ数（アイテム数が0の場合は1）</returns>
        public int GetTotalPages(List<IDatabaseItem> items)
            => _paginationInfo.GetTotalPages(items);

        /// <summary>
        /// 現在のページのアイテムを取得
        /// </summary>
        /// <param name="items">アイテムリスト</param>
        /// <returns>現在のページに表示するアイテム</returns>
        public IEnumerable<IDatabaseItem> GetCurrentPageItems(List<IDatabaseItem> items)
            => _paginationInfo.GetCurrentPageItems(items);

        /// <summary>
        /// ページをリセット（1ページ目に戻す）
        /// </summary>
        public void ResetPage()
            => _paginationInfo.ResetPage();

        /// <summary>
        /// 次のページに移動
        /// </summary>
        /// <param name="totalPages">総ページ数</param>
        /// <returns>移動が成功したかどうか（現在のページが最後のページの場合はfalse）</returns>
        public bool MoveToNextPage(int totalPages)
        {
            bool result = _paginationInfo.MoveToNextPage(totalPages);
            if (result) DebugLogger.Log($"Moved to next page: {_paginationInfo.CurrentPage}");
            return result;
        }

        /// <summary>
        /// 前のページに移動
        /// </summary>
        /// <returns>移動が成功したかどうか（現在のページが1ページ目の場合はfalse）</returns>
        public bool MoveToPreviousPage()
        {
            bool result = _paginationInfo.MoveToPreviousPage();
            if (result) DebugLogger.Log($"Moved to previous page: {_paginationInfo.CurrentPage}");
            return result;
        }

        /// <summary>
        /// 指定したページに移動
        /// </summary>
        /// <param name="page">移動先のページ番号（1以上）</param>
        /// <param name="totalPages">総ページ数</param>
        /// <returns>移動が成功したかどうか（ページ番号が無効な場合はfalse）</returns>
        public bool MoveToPage(int page, int totalPages)
        {
            bool result = _paginationInfo.MoveToPage(page, totalPages);
            if (result) DebugLogger.Log($"Moved to page: {page}");
            return result;
        }

        /// <summary>
        /// 現在のタブのアイテムを取得
        /// </summary>
        /// <param name="getFilteredAvatars">フィルターされたアバターを取得する関数</param>
        /// <param name="getFilteredItems">フィルターされたアイテムを取得する関数</param>
        /// <param name="getFilteredWorldObjects">フィルターされたワールドオブジェクトを取得する関数</param>
        /// <param name="getFilteredOthers">フィルターされたその他のアイテムを取得する関数</param>
        /// <returns>現在のタブのアイテムリスト</returns>
        public List<IDatabaseItem> GetCurrentTabItems(
            Func<List<IDatabaseItem>> getFilteredAvatars,
            Func<List<IDatabaseItem>> getFilteredItems,
            Func<List<IDatabaseItem>> getFilteredWorldObjects,
            Func<List<IDatabaseItem>> getFilteredOthers
        )
        {
            return _paginationInfo.SelectedTab switch
            {
                0 => getFilteredAvatars(),
                1 => getFilteredItems(),
                2 => getFilteredWorldObjects(),
                3 => getFilteredOthers(),
                _ => new List<IDatabaseItem>(),
            };
        }
    }
}
