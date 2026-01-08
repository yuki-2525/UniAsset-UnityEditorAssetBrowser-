// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorAssetBrowser.Models;
using UnityEditorAssetBrowser.Helper;
using UnityEngine;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// インポートリストを管理し、一括インポートを実行するサービス
    /// </summary>
    public class ImportQueueService
    {
        private static ImportQueueService? _instance;
        public static ImportQueueService Instance => _instance ??= new ImportQueueService();

        private readonly List<ImportQueueItem> _queue = new();
        public IReadOnlyList<ImportQueueItem> Queue => _queue;

        public event Action? OnQueueChanged;
        public event Action? OnImportStarted;
        public event Action? OnImportCompleted;
        public event Action<int, int>? OnImportProgress; // current, total

        private bool _isImporting;
        public bool IsImporting => _isImporting;

        private int _currentImportIndex;

        private ImportQueueService() { }

        /// <summary>
        /// キューにアイテムを追加
        /// </summary>
        public void Add(string packagePath, string packageName, string thumbnailPath, string category)
        {
            if (_queue.Any(x => x.PackagePath == packagePath))
            {
                DebugLogger.Log($"Queue already contains: {packageName}");
                return;
            }

            _queue.Add(new ImportQueueItem
            {
                PackagePath = packagePath,
                PackageName = packageName,
                ThumbnailPath = thumbnailPath,
                Category = category
            });
            OnQueueChanged?.Invoke();
            DebugLogger.Log($"Added to import queue: {packageName}");
        }

        /// <summary>
        /// キューからアイテムを削除
        /// </summary>
        public void Remove(int index)
        {
            if (index >= 0 && index < _queue.Count)
            {
                DebugLogger.Log($"Removing from import queue: {_queue[index].PackageName}");
                _queue.RemoveAt(index);
                OnQueueChanged?.Invoke();
            }
        }

        /// <summary>
        /// キューのアイテムを移動
        /// </summary>
        public void Move(int oldIndex, int newIndex)
        {
            if (oldIndex >= 0 && oldIndex < _queue.Count && newIndex >= 0 && newIndex < _queue.Count)
            {
                var item = _queue[oldIndex];
                _queue.RemoveAt(oldIndex);
                _queue.Insert(newIndex, item);
                OnQueueChanged?.Invoke();
            }
        }

        /// <summary>
        /// キューをクリア
        /// </summary>
        public void Clear()
        {
            DebugLogger.Log("Clearing import queue.");
            _queue.Clear();
            OnQueueChanged?.Invoke();
        }

        /// <summary>
        /// 一括インポートを開始
        /// </summary>
        public void StartImport()
        {
            if (_isImporting || _queue.Count == 0) return;

            DebugLogger.Log($"Starting batch import. Items: {_queue.Count}");

            _isImporting = true;
            _currentImportIndex = 0;
            OnImportStarted?.Invoke();

            ProcessNextImport();
        }

        private void ProcessNextImport()
        {
            if (_currentImportIndex >= _queue.Count)
            {
                FinishImport();
                return;
            }

            var item = _queue[_currentImportIndex];
            OnImportProgress?.Invoke(_currentImportIndex + 1, _queue.Count);

            DebugLogger.Log($"Importing ({_currentImportIndex + 1}/{_queue.Count}): {item.PackageName}");

            // イベントハンドラ登録
            AssetDatabase.importPackageCompleted += OnPackageImportCompleted;
            AssetDatabase.importPackageFailed += OnPackageImportFailed;
            AssetDatabase.importPackageCancelled += OnPackageImportCancelled;

            try
            {
                // UnityPackageServicesを使用してインポート（サムネイル生成などを含む）
                // ダイアログは強制的に非表示
                UnityPackageServices.ImportPackageAndSetThumbnails(
                    item.PackagePath,
                    item.ThumbnailPath,
                    item.Category,
                    null, // forceImportToCategoryFolder
                    false, // showDialog
                    (error) => // onPreImportError
                    {
                        DebugLogger.LogError($"Pre-import error for {item.PackageName}: {error}");
                        UnregisterHandlers();
                        _currentImportIndex++;
                        ProcessNextImport();
                    }
                );
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"Failed to start import for {item.PackageName}: {ex.Message}");
                UnregisterHandlers();
                // エラーでも次へ進む
                _currentImportIndex++;
                ProcessNextImport();
            }
        }

        private void OnPackageImportCompleted(string packageName)
        {
            UnregisterHandlers();
            _currentImportIndex++;
            // 少し遅延させて次を実行（Unityの処理安定のため）
            EditorApplication.delayCall += ProcessNextImport;
        }

        private void OnPackageImportFailed(string packageName, string errorMessage)
        {
            DebugLogger.LogError($"Import failed for {packageName}: {errorMessage}");
            UnregisterHandlers();
            _currentImportIndex++;
            EditorApplication.delayCall += ProcessNextImport;
        }

        private void OnPackageImportCancelled(string packageName)
        {
            DebugLogger.LogWarning($"Import cancelled for {packageName}");
            UnregisterHandlers();
            _currentImportIndex++;
            EditorApplication.delayCall += ProcessNextImport;
        }

        private void UnregisterHandlers()
        {
            AssetDatabase.importPackageCompleted -= OnPackageImportCompleted;
            AssetDatabase.importPackageFailed -= OnPackageImportFailed;
            AssetDatabase.importPackageCancelled -= OnPackageImportCancelled;
        }

        private void FinishImport()
        {
            _isImporting = false;
            DebugLogger.Log("Batch import completed.");
            
            // インポート完了時にリストをクリア
            Clear();

            OnImportCompleted?.Invoke();
            
            // インポート完了後にアセットデータベースをリフレッシュ
            AssetDatabase.Refresh();
        }
    }
}
