// Copyright (c) 2025-2026 sakurayuki

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditorAssetBrowser.Helper;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// UnityPackage以外を含む、データベースアイテムのファイル操作を提供します。
    /// </summary>
    public static class AssetFileServices
    {
        public static string[] FindFiles(string path, IEnumerable<string> extensions)
        {
            if (string.IsNullOrEmpty(path)) return Array.Empty<string>();

            return FindFilesInternal(path, NormalizeExtensions(extensions));
        }

        public static string[] FindFilesFromPaths(IEnumerable<string> paths, IEnumerable<string> extensions)
        {
            if (paths == null) return Array.Empty<string>();

            var normalizedExtensions = NormalizeExtensions(extensions);
            return paths
                .Where(path => !string.IsNullOrEmpty(path))
                .SelectMany(path => FindFilesInternal(path, normalizedExtensions))
                .ToArray();
        }

        private static string[] FindFilesInternal(string path, HashSet<string> normalizedExtensions)
        {
            if (string.IsNullOrEmpty(path)) return Array.Empty<string>();

            if (File.Exists(path))
            {
                return normalizedExtensions.Contains(Path.GetExtension(path))
                    ? new[] { path }
                    : Array.Empty<string>();
            }

            if (!Directory.Exists(path)) return Array.Empty<string>();

            try
            {
                return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Where(file => normalizedExtensions.Contains(Path.GetExtension(file)))
                    .ToArray();
            }
            catch (UnauthorizedAccessException ex)
            {
                DebugLogger.LogWarning($"Could not access files under {path}: {ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                DebugLogger.LogWarning($"Path was too long while searching under {path}: {ex.Message}");
            }
            catch (IOException ex)
            {
                DebugLogger.LogWarning($"Could not search files under {path}: {ex.Message}");
            }

            return Array.Empty<string>();
        }

        private static HashSet<string> NormalizeExtensions(IEnumerable<string> extensions)
        {
            return new HashSet<string>(
                (extensions ?? Array.Empty<string>())
                    .Select(AssetFileExtensionService.NormalizeExtension)
                    .Where(x => !string.IsNullOrEmpty(x)),
                StringComparer.OrdinalIgnoreCase);
        }

        public static bool CopyToFolder(string sourcePath, string destinationFolder, out string destinationPath)
        {
            destinationPath = string.Empty;
            if (!File.Exists(sourcePath) || !Directory.Exists(destinationFolder)) return false;

            try
            {
                destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
                if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
                    return true;

                File.Copy(sourcePath, destinationPath, true);
                return true;
            }
            catch (IOException ex)
            {
                DebugLogger.LogError($"Could not copy file {sourcePath}: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                DebugLogger.LogError($"Could not copy file {sourcePath}: {ex.Message}");
            }

            return false;
        }

        public static void OpenInExplorer(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{path}\"",
                        UseShellExecute = true
                    });
                }
                else if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"Could not open Explorer for {path}: {ex.Message}");
            }
        }

        public static void OpenWithDefaultApplication(string path)
        {
            if (!File.Exists(path)) return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"Could not open file {path}: {ex.Message}");
            }
        }
    }
}
