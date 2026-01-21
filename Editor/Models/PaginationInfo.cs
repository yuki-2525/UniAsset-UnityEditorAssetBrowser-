// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditorAssetBrowser.Interfaces;
using UnityEngine;

namespace UnityEditorAssetBrowser.Models
{
    /// <summary>
    /// ページネーション情報を管理するクラス
    /// アイテムの表示ページ管理、ページ移動、アイテムの取得機能を提供する
    /// </summary>
    public class PaginationInfo
    {
        /// <summary>
        /// 1ページあたりのデフォルトアイテム表示数
        /// </summary>
        public const int ITEMS_PER_PAGE = 10;

        /// <summary>
        /// 現在のページ番号（0から開始）
        /// </summary>
        public int CurrentPage { get; private set; } = 0;

        /// <summary>
        /// 選択中のタブ番号（0から開始）
        /// </summary>
        public int SelectedTab { get; set; } = 0;

        private int _itemsPerPage = ITEMS_PER_PAGE;

        /// <summary>
        /// 1ページあたりのアイテム数
        /// 最小値は1に制限される
        /// </summary>
        public int ItemsPerPage
        {
            get => _itemsPerPage;
            set => _itemsPerPage = Math.Max(1, value);
        }

        /// <summary>
        /// 総ページ数を計算
        /// </summary>
        /// <param name="items">全アイテムのリスト</param>
        /// <returns>計算された総ページ数（最小値は1）</returns>
        public int GetTotalPages(List<IDatabaseItem> items)
        {
            int totalItems = items.Count;
            return Mathf.Max(1, Mathf.CeilToInt((float)totalItems / ItemsPerPage));
        }

        /// <summary>
        /// 現在のページに表示するアイテムを取得
        /// </summary>
        /// <param name="items">全アイテムのリスト</param>
        /// <returns>現在のページに表示するアイテムの列挙</returns>
        public IEnumerable<IDatabaseItem> GetCurrentPageItems(List<IDatabaseItem> items)
        {
            int startIndex = CurrentPage * ItemsPerPage;
            return items.Skip(startIndex).Take(ItemsPerPage);
        }

        /// <summary>
        /// ページ番号を0にリセット
        /// </summary>
        public void ResetPage()
            => CurrentPage = 0;

        /// <summary>
        /// 次のページに移動
        /// </summary>
        /// <param name="totalPages">総ページ数</param>
        /// <returns>移動が成功した場合はtrue、失敗した場合はfalse</returns>
        public bool MoveToNextPage(int totalPages)
        {
            if (CurrentPage < totalPages - 1)
            {
                CurrentPage++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 前のページに移動
        /// </summary>
        /// <returns>移動が成功した場合はtrue、失敗した場合はfalse</returns>
        public bool MoveToPreviousPage()
        {
            if (CurrentPage > 0)
            {
                CurrentPage--;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 指定したページ番号に移動
        /// </summary>
        /// <param name="page">移動先のページ番号（0から開始）</param>
        /// <param name="totalPages">総ページ数</param>
        /// <returns>移動が成功した場合はtrue、失敗した場合はfalse</returns>
        public bool MoveToPage(int page, int totalPages)
        {
            if (page >= 0 && page < totalPages)
            {
                CurrentPage = page;
                return true;
            }
            
            return false;
        }
    }
}
