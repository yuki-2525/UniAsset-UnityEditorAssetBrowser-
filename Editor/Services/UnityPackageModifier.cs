// Copyright (c) 2025-2026 sakurayuki
// This code is borrowed from Avatar-Explorer(https://github.com/puk06/Avatar-Explorer)
// Avatar-Explorer is licensed under the MIT License. https://github.com/puk06/Avatar-Explorer/blob/master/LICENSE.txt
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditorAssetBrowser.Helper;

namespace UnityEditorAssetBrowser.Services
{
    public static class UnityPackageModifier
    {
        public static async Task<string> CreateModifiedPackageAsync(string sourcePath, string category)
        {
            DebugLogger.Log($"Creating modified package. Source: {sourcePath}, Category: {category}");
            string tempDir = Path.Combine(Path.GetTempPath(), "UnityEditorAssetBrowser");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            // UnityのAssetDatabase.ImportPackageは、パスに日本語が含まれていると「Couldn't decompress package」エラーで失敗することがあるため、
            // 一時ファイル名はASCII文字のみで構成するようにする。
            string destPath = Path.Combine(tempDir, $"temp_pkg_{Guid.NewGuid()}.unitypackage");

            await Task.Run(() =>
            {
                using (var sourceStream = File.OpenRead(sourcePath))
                using (var destStream = File.Create(destPath))
                using (var sourceGzip = new GZipStream(sourceStream, CompressionMode.Decompress))
                using (var destGzip = new GZipStream(destStream, CompressionMode.Compress))
                {
                    ModifyTarStream(sourceGzip, destGzip, category);
                }
            });

            return destPath;
        }

        /// <summary>
        /// 一時フォルダ内の古いパッケージファイルを削除する
        /// </summary>
        public static void Cleanup()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "UnityEditorAssetBrowser");
            if (!Directory.Exists(tempDir)) return;

            try
            {
                var files = Directory.GetFiles(tempDir, "*.unitypackage");
                foreach (var file in files)
                {
                    string fname = Path.GetFileName(file);
                    if (fname.Contains("_modified_") || fname.StartsWith("temp_pkg_"))
                    {
                        try
                        {
                            // 単純に削除を試みる。使用中の場合は例外が発生してスキップされる。
                            File.Delete(file);
                        }
                        catch
                        {
                            // 無視
                        }
                    }
                }
            }
            catch
            {
                // 無視
            }
        }

        private static void ModifyTarStream(Stream input, Stream output, string category)
        {
            byte[] headerBuffer = new byte[512];
            while (true)
            {
                int read = ReadFull(input, headerBuffer, 512);
                if (read < 512) break; // ストリームの終了

                // ヌルブロック（アーカイブの終了）を確認
                if (IsZeroBlock(headerBuffer))
                {
                    output.Write(headerBuffer, 0, 512);
                    // 2つ目のヌルブロックを読み込む
                    read = ReadFull(input, headerBuffer, 512);
                    output.Write(headerBuffer, 0, 512);
                    break;
                }

                string name = GetString(headerBuffer, 0, 100);
                long size = GetOctal(headerBuffer, 124, 12);
                char typeFlag = (char)headerBuffer[156];

                bool isPathname = name.EndsWith("pathname") || name.EndsWith("/pathname");

                if (isPathname && typeFlag == '0') // '0' は通常ファイル
                {
                    byte[] content = new byte[size];
                    ReadFull(input, content, (int)size);

                    // コンテンツを変更
                    string path = Encoding.UTF8.GetString(content);
                    // BOMが存在する場合は削除（Unityは通常pathnameにBOMを使用しないが念のため）
                    // 実際にはpathnameは通常単なる文字列
                    
                    // 必要に応じてWindows/Unixの改行コードを処理するが、通常は単なるパス
                    // 'pathname'ファイル内のパスは通常スラッシュを使用し、"Assets"で始まる
                    
                    // パス文字列をクリーンアップ（ヌル文字や改行があれば削除）
                    path = path.TrimEnd('\0', '\r', '\n');

                    if (path.StartsWith("Assets"))
                    {
                        // カテゴリを挿入
                        // Assets/Folder -> Assets/Category/Folder
                        // Assets/File.ext -> Assets/Category/File.ext
                        
                        // "Assets/" の後に挿入したい
                        // パスが正確に "Assets" の場合、変更したくない可能性がある
                        // しかし通常、アイテムはAssetsの下にある
                        
                        if (path.Length > 7 && (path[6] == '/' || path[6] == '\\'))
                        {
                            path = path.Insert(7, $"{category}/");
                        }
                        else if (path == "Assets")
                        {
                            // ルートのAssetsフォルダを変更すべきか？ いいえ。
                        }
                    }

                    byte[] newContent = Encoding.UTF8.GetBytes(path);

                    // ヘッダーを更新
                    UpdateHeaderSize(headerBuffer, newContent.Length);
                    UpdateHeaderChecksum(headerBuffer);

                    output.Write(headerBuffer, 0, 512);
                    WritePadded(output, newContent);
                    
                    // 入力内の元のコンテンツのパディングをスキップ
                    long padding = (512 - (size % 512)) % 512;
                    if (padding > 0)
                    {
                        // GZipStreamはSeekをサポートしていない可能性があるため、読み込んで破棄する
                        byte[] discardBuffer = new byte[padding];
                        ReadFull(input, discardBuffer, (int)padding);
                    }
                }
                else
                {
                    output.Write(headerBuffer, 0, 512);
                    CopyContent(input, output, size);
                }
            }
        }

        private static void CopyContent(Stream input, Stream output, long size)
        {
            long bytesToRead = size;
            long padding = (512 - (size % 512)) % 512;
            long total = bytesToRead + padding;

            byte[] buffer = new byte[4096];
            while (total > 0)
            {
                int toRead = (int)Math.Min(total, buffer.Length);
                int read = input.Read(buffer, 0, toRead);
                if (read == 0) break;
                output.Write(buffer, 0, read);
                total -= read;
            }
        }

        private static void WritePadded(Stream output, byte[] content)
        {
            output.Write(content, 0, content.Length);
            long padding = (512 - (content.Length % 512)) % 512;
            if (padding > 0)
            {
                output.Write(new byte[padding], 0, (int)padding);
            }
        }

        private static int ReadFull(Stream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, totalRead, count - totalRead);
                if (read == 0) break;
                totalRead += read;
            }
            return totalRead;
        }

        private static bool IsZeroBlock(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != 0) return false;
            }
            return true;
        }

        private static string GetString(byte[] buffer, int offset, int length)
        {
            int i = 0;
            while (i < length && buffer[offset + i] != 0) i++;
            return Encoding.ASCII.GetString(buffer, offset, i);
        }

        private static long GetOctal(byte[] buffer, int offset, int length)
        {
            string str = GetString(buffer, offset, length).Trim();
            if (string.IsNullOrEmpty(str)) return 0;
            try
            {
                return Convert.ToInt64(str, 8);
            }
            catch
            {
                return 0;
            }
        }

        private static void UpdateHeaderSize(byte[] buffer, long size)
        {
            // サイズは124バイト目から12バイト。8進数、ヌル終端またはスペース終端。
            // 標準的なtarは11桁 + ヌル/スペースを使用する。
            string octal = Convert.ToString(size, 8).PadLeft(11, '0');
            byte[] bytes = Encoding.ASCII.GetBytes(octal);
            Array.Copy(bytes, 0, buffer, 124, 11);
            buffer[124 + 11] = 0; // ヌル終端
        }

        private static void UpdateHeaderChecksum(byte[] buffer)
        {
            // チェックサムは148バイト目から8バイト。
            // 計算時、チェックサムフィールドはスペース（ASCII 32）で埋められているものとして扱われる。
            for (int i = 0; i < 8; i++) buffer[148 + i] = 32;

            long sum = 0;
            for (int i = 0; i < 512; i++) sum += buffer[i];

            // チェックサムを書き込む: 6桁の8進数 + ヌル + スペース
            string octal = Convert.ToString(sum, 8).PadLeft(6, '0');
            byte[] bytes = Encoding.ASCII.GetBytes(octal);
            Array.Copy(bytes, 0, buffer, 148, 6);
            buffer[148 + 6] = 0;
            buffer[148 + 7] = 32; // スペース？ それとも単にヌル？
            // 標準的なtarはしばしば6桁 + ヌル + スペースを使用する。
            // または7桁 + ヌル。
            // 一般的な6桁 + ヌル + スペースを採用する。
        }
    }
}
