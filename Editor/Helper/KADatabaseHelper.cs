// Copyright (c) 2025-2026 sakurayuki
// This code is borrowed from AETools(https://github.com/puk06/AE-Tools)
// AETools is licensed under the MIT License. https://github.com/puk06/AE-Tools/blob/master/LICENSE.txt

#nullable enable

using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditorAssetBrowser.Models;
using UnityEngine;

namespace UnityEditorAssetBrowser.Helper
{
    /// <summary>
    /// KonoAssetデータベースの読み込みと保存を支援するヘルパークラス
    /// アバター、ウェアラブル、ワールドオブジェクトのデータベースを管理する
    /// </summary>
    public class KADatabaseHelper
    {
        /// <summary>
        /// データベース読み込み結果を保持するクラス
        /// 各データベースの読み込み状態を管理する
        /// </summary>
        public class DatabaseLoadResult
        {
            /// <summary>
            /// アバターデータベース
            /// </summary>
            public KonoAssetAvatarsDatabase? AvatarsDatabase;

            /// <summary>
            /// ウェアラブルデータベース
            /// </summary>
            public KonoAssetWearablesDatabase? WearablesDatabase;

            /// <summary>
            /// ワールドオブジェクトデータベース
            /// </summary>
            public KonoAssetWorldObjectsDatabase? WorldObjectsDatabase;

            /// <summary>
            /// その他アセットデータベース
            /// </summary>
            public KonoAssetOtherAssetsDatabase? OtherAssetsDatabase;
        }

        /// <summary>
        /// KonoAssetのデータベースを読み込む
        /// 指定されたパスから各データベースファイルを読み込み、結果を返す
        /// </summary>
        /// <param name="metadataPath">メタデータが格納されているディレクトリのパス</param>
        /// <returns>読み込んだデータベースの結果</returns>
        public static DatabaseLoadResult LoadKADatabaseFiles(string metadataPath)
        {
            var result = new DatabaseLoadResult();

            DebugLogger.Log($"Starting to load KA database from: {metadataPath}");

            try
            {
                // avatars.jsonの読み込み
                var avatarsPath = Path.Combine(metadataPath, "avatars.json");
                if (File.Exists(avatarsPath))
                {
                    DebugLogger.Log($"Reading avatars.json from: {avatarsPath}");
                    var json = File.ReadAllText(avatarsPath);
                    result.AvatarsDatabase = JsonConvert.DeserializeObject<KonoAssetAvatarsDatabase>(json);
                    DebugLogger.Log($"Loaded {result.AvatarsDatabase?.Data.Length ?? 0} avatars.");
                }
                else
                {
                    DebugLogger.Log("avatars.json not found.");
                }

                // avatarWearables.jsonの読み込み
                var wearablesPath = Path.Combine(metadataPath, "avatarWearables.json");
                if (File.Exists(wearablesPath))
                {
                    DebugLogger.Log($"Reading avatarWearables.json from: {wearablesPath}");
                    var json = File.ReadAllText(wearablesPath);
                    result.WearablesDatabase = JsonConvert.DeserializeObject<KonoAssetWearablesDatabase>(json);
                    DebugLogger.Log($"Loaded {result.WearablesDatabase?.Data.Length ?? 0} wearables.");
                }
                else
                {
                    DebugLogger.Log("avatarWearables.json not found.");
                }

                // worldObjects.jsonの読み込み
                var worldObjectsPath = Path.Combine(metadataPath, "worldObjects.json");
                if (File.Exists(worldObjectsPath))
                {
                    DebugLogger.Log($"Reading worldObjects.json from: {worldObjectsPath}");
                    var json = File.ReadAllText(worldObjectsPath);
                    result.WorldObjectsDatabase = JsonConvert.DeserializeObject<KonoAssetWorldObjectsDatabase>(json);
                    DebugLogger.Log($"Loaded {result.WorldObjectsDatabase?.Data.Length ?? 0} world objects.");
                }
                else
                {
                    DebugLogger.Log("worldObjects.json not found.");
                }

                // otherAssets.jsonの読み込み
                var otherAssetsPath = Path.Combine(metadataPath, "otherAssets.json");
                if (File.Exists(otherAssetsPath))
                {
                    DebugLogger.Log($"Reading otherAssets.json from: {otherAssetsPath}");
                    var json = File.ReadAllText(otherAssetsPath);
                    result.OtherAssetsDatabase = JsonConvert.DeserializeObject<KonoAssetOtherAssetsDatabase>(json);
                    DebugLogger.Log($"Loaded {result.OtherAssetsDatabase?.Data.Length ?? 0} other assets.");
                }
                else
                {
                    DebugLogger.Log("otherAssets.json not found.");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"Failed to load KA database: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// KAデータベースを保存する
        /// 指定されたパスにデータベースをJSON形式で保存する
        /// </summary>
        /// <param name="path">保存先のディレクトリパス</param>
        /// <param name="database">保存するデータベース</param>
        public static void SaveKADatabase(
            string path
            // , KonoAssetDatabase database
        )
        {
            return; // 勝手に書き換えられたら困るため、一応
            
            // try
            // {
            //     var metadataPath = Path.Combine(path, "metadata");
            //     if (!Directory.Exists(metadataPath)) Directory.CreateDirectory(metadataPath);

            //     var jsonPath = Path.Combine(metadataPath, "database.json");
            //     var json = JsonConvert.SerializeObject(database, JsonSettings.Settings);
            //     File.WriteAllText(jsonPath, json);
            // }
            // catch (Exception ex)
            // {
            //     Debug.LogError($"Error saving KA database: {ex.Message}");
            // }
        }
    }
}
