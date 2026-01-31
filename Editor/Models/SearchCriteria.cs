// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System;
using System.Collections.Generic;

namespace UnityEditorAssetBrowser.Models
{
    /// <summary>
    /// 検索条件を管理するクラス
    /// 基本検索、詳細検索、ソート、フィルター、ページネーションの機能を提供する
    /// </summary>
    public class SearchCriteria
    {
        // 基本検索
        private string _searchQuery = "";

        // 詳細検索
        private bool _showAdvancedSearch = false;
        private string _titleSearch = "";
        private string _authorSearch = "";
        private string _categorySearch = "";
        private string _supportedAvatarsSearch = "";
        private string _tagsSearch = "";
        private string _memoSearch = "";

        // ソート方法
        private SortMethod _sortMethod = SortMethod.TitleAsc;

        // フィルター
        private bool _showAvatars = true;
        private bool _showWearables = true;
        private bool _showWorldObjects = true;
        private bool _showOtherAssets = true;

        // ページネーション
        private int _currentPage = 1;
        private int _itemsPerPage = 20;

        /// <summary>
        /// 基本検索クエリ
        /// スペース区切りで複数キーワードを指定可能
        /// </summary>
        public string SearchQuery
        {
            get => _searchQuery;
            set => _searchQuery = value ?? "";
        }

        /// <summary>
        /// 詳細検索パネルの表示状態
        /// </summary>
        public bool ShowAdvancedSearch
        {
            get => _showAdvancedSearch;
            set => _showAdvancedSearch = value;
        }

        /// <summary>
        /// タイトル検索クエリ
        /// スペース区切りで複数キーワードを指定可能
        /// </summary>
        public string TitleSearch
        {
            get => _titleSearch;
            set => _titleSearch = value ?? "";
        }

        /// <summary>
        /// 作者名検索クエリ
        /// スペース区切りで複数キーワードを指定可能
        /// </summary>
        public string AuthorSearch
        {
            get => _authorSearch;
            set => _authorSearch = value ?? "";
        }

        /// <summary>
        /// カテゴリ検索クエリ
        /// スペース区切りで複数キーワードを指定可能
        /// </summary>
        public string CategorySearch
        {
            get => _categorySearch;
            set => _categorySearch = value ?? "";
        }

        /// <summary>
        /// 対応アバター検索クエリ
        /// スペース区切りで複数キーワードを指定可能
        /// </summary>
        public string SupportedAvatarsSearch
        {
            get => _supportedAvatarsSearch;
            set => _supportedAvatarsSearch = value ?? "";
        }

        /// <summary>
        /// タグ検索クエリ
        /// スペース区切りで複数キーワードを指定可能
        /// </summary>
        public string TagsSearch
        {
            get => _tagsSearch;
            set => _tagsSearch = value ?? "";
        }

        /// <summary>
        /// メモ検索クエリ
        /// スペース区切りで複数キーワードを指定可能
        /// </summary>
        public string MemoSearch
        {
            get => _memoSearch;
            set => _memoSearch = value ?? "";
        }

        /// <summary>
        /// アイテムのソート方法
        /// </summary>
        public SortMethod SortMethod
        {
            get => _sortMethod;
            set => _sortMethod = value;
        }

        /// <summary>
        /// アバターアイテムの表示/非表示
        /// </summary>
        public bool ShowAvatars
        {
            get => _showAvatars;
            set => _showAvatars = value;
        }

        /// <summary>
        /// ウェアラブルアイテムの表示/非表示
        /// </summary>
        public bool ShowWearables
        {
            get => _showWearables;
            set => _showWearables = value;
        }

        /// <summary>
        /// ワールドオブジェクトアイテムの表示/非表示
        /// </summary>
        public bool ShowWorldObjects
        {
            get => _showWorldObjects;
            set => _showWorldObjects = value;
        }

        /// <summary>
        /// その他アセットアイテムの表示/非表示
        /// </summary>
        public bool ShowOtherAssets
        {
            get => _showOtherAssets;
            set => _showOtherAssets = value;
        }

        /// <summary>
        /// 現在のページ番号（1から開始）
        /// </summary>
        public int CurrentPage
        {
            get => _currentPage;
            set => _currentPage = Math.Max(1, value);
        }

        /// <summary>
        /// 1ページあたりのアイテム表示数
        /// 最小値は1に制限される
        /// </summary>
        public int ItemsPerPage
        {
            get => _itemsPerPage;
            set => _itemsPerPage = Math.Max(1, value);
        }

        private static readonly char[] EmptyChars = new[] { ' ', '　' };

        /// <summary>
        /// 検索クエリをキーワード配列に分割
        /// </summary>
        /// <param name="query">分割する検索クエリ</param>
        /// <returns>分割されたキーワード配列</returns>
        private string[] SplitKeywords(string query)
            => query.Split(EmptyChars, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// 基本検索のキーワードを取得
        /// </summary>
        /// <returns>キーワード配列</returns>
        public string[] GetKeywords()
            => SplitKeywords(SearchQuery);

        /// <summary>
        /// タイトル検索のキーワードを取得
        /// </summary>
        /// <returns>キーワード配列</returns>
        public string[] GetTitleKeywords()
            => SplitKeywords(TitleSearch);

        /// <summary>
        /// 作者名検索のキーワードを取得
        /// </summary>
        /// <returns>キーワード配列</returns>
        public string[] GetAuthorKeywords()
            => SplitKeywords(AuthorSearch);

        /// <summary>
        /// カテゴリ検索のキーワードを取得
        /// </summary>
        /// <returns>キーワード配列</returns>
        public string[] GetCategoryKeywords()
            => SplitKeywords(CategorySearch);

        /// <summary>
        /// 対応アバター検索のキーワードを取得
        /// </summary>
        /// <returns>キーワード配列</returns>
        public string[] GetSupportedAvatarsKeywords()
            => SplitKeywords(SupportedAvatarsSearch);

        /// <summary>
        /// タグ検索のキーワードを取得
        /// </summary>
        /// <returns>キーワード配列</returns>
        public string[] GetTagsKeywords()
            => SplitKeywords(TagsSearch);

        /// <summary>
        /// メモ検索のキーワードを取得
        /// </summary>
        /// <returns>キーワード配列</returns>
        public string[] GetMemoKeywords()
            => SplitKeywords(MemoSearch);

        /// <summary>
        /// フィルター条件を取得
        /// </summary>
        /// <returns>フィルター条件の辞書</returns>
        public Dictionary<string, bool> GetFilters()
            => new Dictionary<string, bool>
            {
                { "Avatars", ShowAvatars },
                { "Wearables", ShowWearables },
                { "WorldObjects", ShowWorldObjects },
                { "OtherAssets", ShowOtherAssets },
            };

        /// <summary>
        /// 検索条件をリセット
        /// すべての検索条件を初期値に戻す
        /// </summary>
        public void Reset()
        {
            SearchQuery = "";
            TitleSearch = "";
            AuthorSearch = "";
            CategorySearch = "";
            SupportedAvatarsSearch = "";
            TagsSearch = "";
            MemoSearch = "";
            SortMethod = SortMethod.TitleAsc;
            ShowAvatars = true;
            ShowWearables = true;
            ShowWorldObjects = true;
            ShowOtherAssets = true;
            CurrentPage = 1;
        }

        /// <summary>
        /// タブ固有の検索条件をクリア
        /// </summary>
        /// <param name="tabIndex">タブのインデックス（0: アバター, 2: ワールド）</param>
        public void ClearTabSpecificCriteria(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0: // アバタータブ
                    CategorySearch = "";
                    SupportedAvatarsSearch = "";
                    break;
                case 2: // ワールドタブ
                    SupportedAvatarsSearch = "";
                    break;
            }
        }

        /// <summary>
        /// 検索条件のディープコピーを作成
        /// </summary>
        /// <returns>コピーされた検索条件</returns>
        public SearchCriteria Clone()
            => new SearchCriteria
            {
                SearchQuery = SearchQuery,
                ShowAdvancedSearch = ShowAdvancedSearch,
                TitleSearch = TitleSearch,
                AuthorSearch = AuthorSearch,
                CategorySearch = CategorySearch,
                SupportedAvatarsSearch = SupportedAvatarsSearch,
                TagsSearch = TagsSearch,
                MemoSearch = MemoSearch,
                SortMethod = SortMethod,
                ShowAvatars = ShowAvatars,
                ShowWearables = ShowWearables,
                ShowWorldObjects = ShowWorldObjects,
                ShowOtherAssets = ShowOtherAssets,
                CurrentPage = CurrentPage,
                ItemsPerPage = ItemsPerPage,
            };
    }

    /// <summary>
    /// アイテムのソート方法を定義する列挙型
    /// </summary>
    public enum SortMethod
    {
        /// <summary>
        /// タイトル昇順
        /// </summary>
        TitleAsc,

        /// <summary>
        /// タイトル降順
        /// </summary>
        TitleDesc,

        /// <summary>
        /// 作者名昇順
        /// </summary>
        AuthorAsc,

        /// <summary>
        /// 作者名降順
        /// </summary>
        AuthorDesc,

        /// <summary>
        /// 作成日昇順
        /// </summary>
        DateAsc,

        /// <summary>
        /// 作成日降順
        /// </summary>
        DateDesc,
    }
}
