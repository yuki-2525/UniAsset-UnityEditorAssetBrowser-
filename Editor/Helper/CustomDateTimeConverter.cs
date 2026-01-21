// Copyright (c) 2025-2026 sakurayuki
// This code is borrowed from AETools(https://github.com/puk06/AE-Tools)
// AETools is licensed under the MIT License. https://github.com/puk06/AE-Tools/blob/master/LICENSE.txt

#nullable enable

using System;
using Newtonsoft.Json;
using UnityEngine;

namespace UnityEditorAssetBrowser.Helper
{
    /// <summary>
    /// カスタム日付変換クラス
    /// JSONシリアライズ/デシリアライズ時の日付形式の変換を処理する
    /// </summary>
    public class CustomDateTimeConverter : JsonConverter<DateTime>
    {
        /// <summary>
        /// JSONからDateTimeを読み込む
        /// </summary>
        /// <param name="reader">JSONリーダー</param>
        /// <param name="objectType">変換対象の型</param>
        /// <param name="existingValue">既存の値</param>
        /// <param name="hasExistingValue">既存の値があるかどうか</param>
        /// <param name="serializer">JSONシリアライザー</param>
        /// <returns>変換されたDateTime</returns>
        /// <exception cref="JsonSerializationException">日付の解析に失敗した場合にスローされる</exception>
        public override DateTime ReadJson(
            JsonReader reader,
            Type objectType,
            DateTime existingValue,
            bool hasExistingValue,
            JsonSerializer serializer
        )
        {
            if (reader.TokenType == JsonToken.Null)
                return DateTime.MinValue;

            if (reader.TokenType == JsonToken.String)
            {
                string dateString = reader.Value?.ToString() ?? string.Empty;
                return GetDate(dateString);
            }

            throw new JsonSerializationException(
                $"Unexpected token type {reader.TokenType} when parsing DateTime"
            );
        }

        /// <summary>
        /// DateTimeをJSONに書き込む
        /// </summary>
        /// <param name="writer">JSONライター</param>
        /// <param name="value">書き込むDateTime</param>
        /// <param name="serializer">JSONシリアライザー</param>
        public override void WriteJson(JsonWriter writer, DateTime value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        /// <summary>
        /// 日付文字列をDateTimeに変換
        /// 様々な形式の日付文字列を解析し、UTCのDateTimeに変換する
        /// </summary>
        /// <param name="date">変換対象の日付文字列</param>
        /// <returns>変換されたDateTime（変換に失敗した場合はUnixエポック）</returns>
        /// <exception cref="FormatException">日付文字列の形式が不正な場合にスローされる</exception>
        public static DateTime GetDate(string date)
        {
            try
            {
                // 数字のみの文字列かチェック
                bool allDigits = true;
                foreach (char c in date)
                {
                    if (!char.IsDigit(c))
                    {
                        allDigits = false;
                        break;
                    }
                }

                // Unixタイムスタンプの場合
                if (allDigits)
                {
                    // ミリ秒単位のUnixタイムスタンプをDateTimeに変換
                    return DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(date)).DateTime;
                }

                // 通常の日付文字列の場合
                if (DateTime.TryParse(date, out DateTime result))
                    return result;

                DebugLogger.LogWarning($"Failed to parse date: {date}");
                return DateTimeOffset.FromUnixTimeMilliseconds(0).DateTime;
            }
            catch (Exception ex)
            {
                DebugLogger.LogWarning($"Failed to parse date: {date}, Error: {ex.Message}");
                return DateTimeOffset.FromUnixTimeMilliseconds(0).DateTime;
            }
        }
    }
}
