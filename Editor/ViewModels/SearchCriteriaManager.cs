// Copyright (c) 2025-2026 sakurayuki

using System.Collections.Generic;
using UnityEditorAssetBrowser.Models;
using UnityEditorAssetBrowser.Helper;

namespace UnityEditorAssetBrowser.ViewModels
{
    /// <summary>
    /// 検索条件を管理するクラス
    /// タブごとに検索条件を保持し、タブ切り替え時に適切な検索条件を復元する
    /// </summary>
    public class SearchCriteriaManager
    {
        /// <summary>タブごとの検索条件</summary>
        private readonly Dictionary<int, SearchCriteria> _tabSearchCriteria = new();

        /// <summary>現在のタブインデックス</summary>
        private int _currentTabIndex;

        /// <summary>
        /// 現在の検索条件
        /// タブ切り替え時に自動的に更新される
        /// </summary>
        public SearchCriteria CurrentSearchCriteria { get; private set; } = new();

        /// <summary>
        /// 現在のタブを設定し、検索条件を切り替える
        /// </summary>
        /// <param name="tabIndex">切り替え先のタブインデックス</param>
        public void SetCurrentTab(int tabIndex)
        {
            if (_currentTabIndex != tabIndex)
            {
                DebugLogger.Log($"SearchCriteriaManager Tab Changed: {_currentTabIndex} -> {tabIndex}");
                SaveCurrentTabCriteria();
                _currentTabIndex = tabIndex;
                LoadTabCriteria();
            }
        }

        /// <summary>
        /// 現在のタブの検索条件を保存
        /// </summary>
        private void SaveCurrentTabCriteria()
            => _tabSearchCriteria[_currentTabIndex] = CurrentSearchCriteria.Clone();

        /// <summary>
        /// 現在のタブの検索条件を読み込み
        /// </summary>
        private void LoadTabCriteria()
        {
            if (!_tabSearchCriteria.TryGetValue(_currentTabIndex, out var criteria))
            {
                criteria = new SearchCriteria();
                _tabSearchCriteria[_currentTabIndex] = criteria;
            }

            CurrentSearchCriteria = criteria.Clone();
            CurrentSearchCriteria.ClearTabSpecificCriteria(_currentTabIndex);
        }
    }
}
