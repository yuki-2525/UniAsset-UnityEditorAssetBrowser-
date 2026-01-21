// Copyright (c) 2025-2026 sakurayuki

#nullable enable

namespace UnityEditorAssetBrowser.Models
{
    /// <summary>
    /// アセットタイプの定数定義クラス
    /// EditorPrefsで使用するアセットタイプの値を統一管理する
    /// </summary>
    public enum AssetTypeConstants
    {
        /// <summary>アバタータイプ</summary>
        Avatar,
        
        /// <summary>アバター関連タイプ</summary>
        AvatarRelated,
        
        /// <summary>ワールドタイプ</summary>
        World,
        
        /// <summary>その他タイプ</summary>
        Other
    }
}
