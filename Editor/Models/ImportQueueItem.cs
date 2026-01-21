// Copyright (c) 2025-2026 sakurayuki

#nullable enable

namespace UnityEditorAssetBrowser.Models
{
    /// <summary>
    /// インポートリストのアイテムモデル
    /// </summary>
    [System.Serializable]
    public class ImportQueueItem
    {
        /// <summary>
        /// UnityPackageのパス
        /// </summary>
        public string PackagePath { get; set; } = string.Empty;

        /// <summary>
        /// パッケージ名（表示用）
        /// </summary>
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// サムネイル画像のパス
        /// </summary>
        public string ThumbnailPath { get; set; } = string.Empty;

        /// <summary>
        /// カテゴリ
        /// </summary>
        public string Category { get; set; } = string.Empty;
    }
}
