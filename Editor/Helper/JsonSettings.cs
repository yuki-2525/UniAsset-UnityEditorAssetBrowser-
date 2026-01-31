// Copyright (c) 2025-2026 sakurayuki
// This code is borrowed from AETools(https://github.com/puk06/AE-Tools)
// AETools is licensed under the MIT License. https://github.com/puk06/AE-Tools/blob/master/LICENSE.txt

#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnityEditorAssetBrowser.Helper
{
    /// <summary>
    /// JSONシリアライズ/デシリアライズの設定を提供するクラス
    /// アプリケーション全体で共通のJSON設定を管理する
    /// </summary>
    public static class JsonSettings
    {
        /// <summary>
        /// JSONシリアライズ/デシリアライズの設定
        /// 以下の設定を含む：
        /// - インデント付きのフォーマット
        /// - null値の無視
        /// - 循環参照の無視
        /// - オブジェクトの置換
        /// - 日付のカスタムパース
        /// </summary>
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            // 読みやすいようにインデントを付ける
            Formatting = Formatting.Indented,
            // null値はシリアライズしない
            NullValueHandling = NullValueHandling.Ignore,
            // 循環参照は無視する
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            // 既存のオブジェクトを置換する
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            // 日付の自動パースを無効化
            DateParseHandling = DateParseHandling.None,
            // カスタム日付コンバーターを使用
            Converters = new List<JsonConverter> { new CustomDateTimeConverter() },
        };
    }
}
