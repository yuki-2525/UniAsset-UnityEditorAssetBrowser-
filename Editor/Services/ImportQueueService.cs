// Copyright (c) 2025 sakurayuki

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorAssetBrowser.Models;
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
                return;

            _queue.Add(new ImportQueueItem
            {
                PackagePath = packagePath,
                PackageName = packageName,
                ThumbnailPath = thumbnailPath,
                Category = category
            });
            OnQueueChanged?.Invoke();
        }

        /// <summary>
        /// キューからアイテムを削除
        /// </summary>
        public void Remove(int index)
        {
            if (index >= 0 && index < _queue.Count)
            {
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
            _queue.Clear();
            OnQueueChanged?.Invoke();
        }

        /// <summary>
        /// 一括インポートを開始
        /// </summary>
        public void StartImport()
        {
            if (_isImporting || _queue.Count == 0) return;

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

            Debug.Log($"[UniAsset] Importing ({_currentImportIndex + 1}/{_queue.Count}): {item.PackageName}");

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
                        Debug.LogError($"[UniAsset] Pre-import error for {item.PackageName}: {error}");
                        UnregisterHandlers();
                        _currentImportIndex++;
                        ProcessNextImport();
                    }
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UniAsset] Failed to start import for {item.PackageName}: {ex.Message}");
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
            Debug.LogError($"[UniAsset] Import failed for {packageName}: {errorMessage}");
            UnregisterHandlers();
            _currentImportIndex++;
            EditorApplication.delayCall += ProcessNextImport;
        }

        private void OnPackageImportCancelled(string packageName)
        {
            Debug.LogWarning($"[UniAsset] Import cancelled for {packageName}");
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
            Debug.Log("[UniAsset] Batch import completed.");
            
            // インポート完了時にリストをクリア
            Clear();

            OnImportCompleted?.Invoke();
            
            // インポート完了後にアセットデータベースをリフレッシュ
            AssetDatabase.Refresh();
        }
    }
}
