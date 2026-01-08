// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System.Collections.Generic;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Models;
using UnityEditorAssetBrowser.Services;
using UnityEditorAssetBrowser.Helper;

namespace UnityEditorAssetBrowser.ViewModels
{
    /// <summary>
    /// 検索機能を管理するビューモデル
    /// タブごとの検索条件の管理と、アイテムの検索判定を行う
    /// </summary>
    public class SearchViewModel
    {
        /// <summary>アイテム検索サービス</summary>
        private readonly ItemSearchService _itemSearchService;

        /// <summary>タブごとの検索条件</summary>
        private readonly Dictionary<int, SearchCriteria> _tabSearchCriteria = new();

        /// <summary>現在のタブインデックス</summary>
        private int _currentTab;

        /// <summary>
        /// 現在の検索条件
        /// タブ切り替え時に自動的に更新される
        /// </summary>
        public SearchCriteria SearchCriteria { get; private set; } = new();

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="aeDatabase">AvatarExplorerデータベース</param>
        public SearchViewModel(AvatarExplorerDatabase? aeDatabase)
        {
            _itemSearchService = new ItemSearchService(aeDatabase);
        }

        /// <summary>
        /// 現在のタブを設定し、検索条件を切り替える
        /// </summary>
        /// <param name="tab">切り替え先のタブインデックス</param>
        public void SetCurrentTab(int tab)
        {
            DebugLogger.Log($"SearchViewModel Tab Changed: {_currentTab} -> {tab}");
            // データベースがnullの場合は、検索条件の更新をスキップ
            if (_itemSearchService.IsDatabaseNull())
            {
                _currentTab = tab;
                return;
            }

            _tabSearchCriteria[_currentTab] = SearchCriteria.Clone();
            _currentTab = tab;

            if (!_tabSearchCriteria.TryGetValue(tab, out var criteria))
            {
                criteria = new SearchCriteria();
                _tabSearchCriteria[tab] = criteria;
            }

            SearchCriteria = criteria.Clone();
        }

        /// <summary>
        /// 現在のタブの検索条件をクリア
        /// </summary>
        public void ClearSearchCriteria()
        {DebugLogger.Log("SearchViewModel: Clearing Search Criteria");
            
            SearchCriteria = new SearchCriteria();
            _tabSearchCriteria[_currentTab] = SearchCriteria.Clone();
        }

        /// <summary>
        /// アイテムが検索条件に一致するか判定
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        public bool IsItemMatchSearch(IDatabaseItem item)
            => _itemSearchService.IsItemMatchSearch(item, SearchCriteria, _currentTab);
    }
}
