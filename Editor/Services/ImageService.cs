// Copyright (c) 2025-2026 sakurayuki
// This code is borrowed from AssetLibraryManager (https://github.com/MAIOTAchannel/AssetLibraryManager)
// Used with permission from MAIOTAchannel

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Helper;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// 画像操作を支援するサービスクラス
    /// テクスチャの読み込み、キャッシュ管理、アイテム画像パスの取得機能を提供する
    /// </summary>
    public class ImageServices
    {
        private static ImageServices? instance;

        /// <summary>排他制御用のロックオブジェクト</summary>
        private readonly object _lockObject = new object();
        
        /// <summary>キュー操作用のロックオブジェクト</summary>
        private readonly object _queueLock = new object();

        /// <summary>キャッシュの最大サイズ (件数ベース - 非推奨)</summary>
        // private int MAX_CACHE_SIZE = 50;

        /// <summary>キャッシュの最大容量 (バイト) - デフォルト 512MB</summary>
        private const long MAX_CACHE_MEMORY_SIZE = 512L * 1024L * 1024L;

        /// <summary>現在のキャッシュ使用量 (バイト)</summary>
        private long _currentCacheMemoryUsage = 0;

        /// <summary>
        /// 画像のキャッシュ
        /// キーは画像パス、値は読み込まれたテクスチャ
        /// </summary>
        public Dictionary<string, Texture2D> ImageCache { get; } = new Dictionary<string, Texture2D>();

        /// <summary>LRU管理用のアクセス順序</summary>
        private readonly LinkedList<string> _accessOrder = new LinkedList<string>();

        /// <summary>現在表示中の画像パス</summary>
        private readonly HashSet<string> _currentVisibleImages = new HashSet<string>();

        /// <summary>画像パスとLinkedListNodeのマッピング</summary>
        private readonly Dictionary<string, LinkedListNode<string>> _nodeMap = new Dictionary<string, LinkedListNode<string>>();

        /// <summary>現在読み込み中の画像パス</summary>
        private readonly HashSet<string> _loadingImages = new HashSet<string>();

        /// <summary>プレースホルダーテクスチャ</summary>
        private Texture2D? _placeholderTexture;

        /// <summary>メインスレッド処理キュー</summary>
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

        /// <summary>
        /// シングルトンインスタンスを取得
        /// インスタンスが存在しない場合は新規作成する
        /// </summary>
        public static ImageServices Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ImageServices();
                    instance.InitializePlaceholder();
                    EditorApplication.update += instance.ProcessMainThreadQueue;
                }
                return instance;
            }
        }

        /// <summary>
        /// テクスチャを読み込む
        /// キャッシュに存在する場合はキャッシュから返す
        /// </summary>
        /// <param name="path">読み込むテクスチャのパス</param>
        /// <returns>読み込まれたテクスチャ（読み込みに失敗した場合はnull）</returns>
        public Texture2D? LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return _placeholderTexture;

            lock (_lockObject)
            {
                if (ImageCache.TryGetValue(path, out var cachedTexture))
                {
                    // LRU更新
                    UpdateAccessOrder(path);
                    // 頻繁に出力されるためコメントアウト
                    // DebugLogger.Log($"Cache hit: {path}");
                    return cachedTexture;
                }

                // 既に読み込み中の場合はプレースホルダーを返す
                if (_loadingImages.Contains(path))
                {
                    DebugLogger.Log($"Cache miss (loading): {path}");
                    return _placeholderTexture;
                }
                
                DebugLogger.Log($"Cache miss (new load): {path}");
            }

            // URLの場合は非同期読み込みを開始してプレースホルダーを返す
            if (path.StartsWith("http://") || path.StartsWith("https://"))
            {
                LoadTextureAsync(path, priority: 1);
                return _placeholderTexture;
            }

            // 即座に同期読み込みを試行（小さいファイル用）
            if (TryLoadSmallImageSync(path, out var texture))
            {
                DebugLogger.Log($"Loaded small image sync: {path}");
                return texture;
            }

            // 大きいファイルは非同期読み込み
            DebugLogger.Log($"Start loading large/async: {path}");
            LoadTextureAsync(path, priority: 1);
            return _placeholderTexture;
        }

        /// <summary>
        /// 小さい画像の同期読み込みを試行
        /// </summary>
        private bool TryLoadSmallImageSync(string path, out Texture2D? texture)
        {
            texture = null;
            
            try
            {
                if (!File.Exists(path)) return false;

                var fileInfo = new FileInfo(path);
                if (fileInfo.Length > 2 * 1024 * 1024) return false; // 2MB以下のファイルは同期読み込み
                
                var bytes = File.ReadAllBytes(path);
                texture = new Texture2D(2, 2);
                
                if (ImageConversion.LoadImage(texture, bytes))
                {
                    AddToCache(path, texture);
                    return true;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    texture = null;
                    return false;
                }
            }
            catch
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    texture = null;
                }
                return false;
            }
        }

        /// <summary>
        /// メインスレッドキューの処理
        /// </summary>
        private void ProcessMainThreadQueue()
        {
            var processCount = 0;
            while (processCount < 10) // 1フレームで最大10個処理
            {
                Action? action = null;
                lock (_queueLock)
                {
                    if (_mainThreadQueue.Count > 0)
                    {
                        action = _mainThreadQueue.Dequeue();
                    }
                }

                if (action == null) break;

                action.Invoke();
                processCount++;
            }
        }

        /// <summary>
        /// 大きい画像の非同期読み込み（Task.Run版）
        /// </summary>
        private void LoadLargeImageAsync(string path, Action<Texture2D?>? onComplete)
        {
            DebugLogger.Log($"Async load started (Task): {path}");
            try
            {
                if (!File.Exists(path))
                {
                    lock (_queueLock)
                    {
                        _mainThreadQueue.Enqueue(() =>
                        {
                            lock (_lockObject) { _loadingImages.Remove(path); }
                            onComplete?.Invoke(null);
                        });
                    }
                    return;
                }

                var bytes = File.ReadAllBytes(path);
                
                // メインスレッドでテクスチャ作成
                lock (_queueLock)
                {
                    _mainThreadQueue.Enqueue(() => {
                        CreateTextureFromBytesSync(path, bytes, onComplete);
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError(string.Format(LocalizationService.Instance.GetString("error_large_image_load_failed"), path, ex.Message));

                lock (_queueLock)
                {
                    _mainThreadQueue.Enqueue(() => {
                        lock (_lockObject) { _loadingImages.Remove(path); }
                        onComplete?.Invoke(null);
                    });
                }
            }
        }

        /// <summary>
        /// バイト配列からテクスチャを作成（同期版）
        /// </summary>
        private void CreateTextureFromBytesSync(string path, byte[] bytes, Action<Texture2D?>? onComplete)
        {
            Texture2D? texture = null;

            try
            {
                texture = new Texture2D(2, 2);
                if (ImageConversion.LoadImage(texture, bytes))
                {
                    AddToCache(path, texture);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    texture = null;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError(string.Format(LocalizationService.Instance.GetString("error_texture_creation_failed"), path, ex.Message));
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    texture = null;
                }
            }
            
            lock (_lockObject) { _loadingImages.Remove(path); }
            onComplete?.Invoke(texture);
            
            // UI更新
            if (EditorWindow.focusedWindow != null) EditorWindow.focusedWindow.Repaint();
        }

        /// <summary>
        /// テクスチャを非同期で読み込む
        /// </summary>
        public void LoadTextureAsync(string path, Action<Texture2D?>? onComplete = null, int priority = 0)
        {
            if (string.IsNullOrEmpty(path))
            {
                onComplete?.Invoke(null);
                return;
            }

            lock (_lockObject)
            {
                // 既にキャッシュに存在する場合
                if (ImageCache.TryGetValue(path, out var cachedTexture))
                {
                    UpdateAccessOrder(path);
                    onComplete?.Invoke(cachedTexture);
                    return;
                }

                // 既に読み込み中の場合はスキップ
                if (_loadingImages.Contains(path))
                {
                    return;
                }

                _loadingImages.Add(path);
            }

            if (path.StartsWith("http://") || path.StartsWith("https://"))
            {
                LoadUrlImage(path, onComplete);
            }
            else
            {
                // 大きいファイルは直接Task.Runで処理
                Task.Run(() => LoadLargeImageAsync(path, onComplete));
            }
        }

        /// <summary>
        /// URLから画像を読み込む
        /// </summary>
        private void LoadUrlImage(string url, Action<Texture2D?>? onComplete)
        {
            DebugLogger.Log($"Async load started (URL): {url}");
            lock (_queueLock)
            {
                _mainThreadQueue.Enqueue(async () =>
                {
                    using (var uwr = UnityWebRequestTexture.GetTexture(url))
                    {
                        var op = uwr.SendWebRequest();
                        while (!op.isDone) await Task.Yield();

                        if (uwr.result != UnityWebRequest.Result.Success)
                        {
                            DebugLogger.LogWarning($"Failed to download image: {url}\n{uwr.error}");
                            lock(_lockObject) { _loadingImages.Remove(url); }
                            onComplete?.Invoke(null);
                        }
                        else
                        {
                            var texture = DownloadHandlerTexture.GetContent(uwr);
                            if (texture != null)
                            {
                                AddToCache(url, texture);
                            }
                            lock(_lockObject) { _loadingImages.Remove(url); }
                            onComplete?.Invoke(texture);

                            if (EditorWindow.focusedWindow != null) EditorWindow.focusedWindow.Repaint();
                        }
                    }
                });
            }
        }

        /// <summary>
        /// 画像キャッシュをクリア
        /// </summary>
        public void ClearCache()
        {
            DebugLogger.Log($"Clearing image cache. Count: {ImageCache.Count}");
            
            lock (_lockObject)
            {
                foreach (var texture in ImageCache.Values)
                {
                    if (texture != null && texture != _placeholderTexture)
                    {
                        try
                        {
                            UnityEngine.Object.DestroyImmediate(texture);
                        }
                        catch {}
                    }
                }

                ImageCache.Clear();
                _accessOrder.Clear();
                _nodeMap.Clear();
                _currentVisibleImages.Clear();
                _loadingImages.Clear();
                _currentCacheMemoryUsage = 0;
            }
        }

        /// <summary>
        /// ImageServiceインスタンスを破棄し、イベントハンドラーをクリーンアップ
        /// </summary>
        public void Dispose()
        {
            try
            {
                EditorApplication.update -= ProcessMainThreadQueue;
                ClearCache();
                
                if (_placeholderTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(_placeholderTexture);
                    _placeholderTexture = null;
                }
            }
            catch {}
            finally
            {
                instance = null;
            }
        }

        /// <summary>
        /// 現在表示中のアイテムの画像を再読み込み
        /// </summary>
        public void ReloadCurrentItemsImages(IEnumerable<IDatabaseItem> items)
        {
            foreach (var item in items)
            {
                string imagePath = item.GetImagePath();
                if (!string.IsNullOrEmpty(imagePath))
                {
                    if (imagePath.StartsWith("http") || File.Exists(imagePath))
                    {
                        LoadTexture(imagePath);
                    }
                }
            }
        }

        /// <summary>
        /// 表示中アイテムの画像を更新し、不要な画像を削除
        /// </summary>
        public void UpdateVisibleImages(IEnumerable<IDatabaseItem> visibleItems)
        {
            var newVisibleImages = new HashSet<string>();

            // 新しく表示されるアイテムの画像パス収集
            foreach (var item in visibleItems)
            {
                string imagePath = item.GetImagePath();

                if (!string.IsNullOrEmpty(imagePath))
                {
                    newVisibleImages.Add(imagePath);

                    bool needLoad = false;
                    lock (_lockObject)
                    {
                        // まだキャッシュにない場合のみ読み込み
                        if (!ImageCache.ContainsKey(imagePath))
                        {
                            needLoad = true;
                        }
                    }

                    if (needLoad)
                    {
                         if (imagePath.StartsWith("http") || File.Exists(imagePath))
                         {
                             // LoadTexture内部でロックを取得するのでここでは取得しない
                             LoadTexture(imagePath);
                         }
                    }
                }
            }
            
            lock (_lockObject)
            {
                _currentVisibleImages.Clear();
                foreach (var path in newVisibleImages)
                {
                    _currentVisibleImages.Add(path);
                }
            }

            // 表示中画像の読み込み完了後にEditorWindowを再描画
            if (newVisibleImages.Any())
            {
                EditorApplication.delayCall += () =>
                {
                    if (EditorWindow.focusedWindow != null) EditorWindow.focusedWindow.Repaint();
                };
            }
        }

        /// <summary>
        /// 検索結果に応じてキャッシュサイズを適応的に調整
        /// </summary>
        public void AdaptCacheSizeToSearchResults(int searchResultCount)
        {
        }

        /// <summary>
        /// キャッシュにテクスチャを追加
        /// </summary>
        private void AddToCache(string path, Texture2D texture)
        {
            long textureSize = Profiler.GetRuntimeMemorySizeLong(texture);

            lock (_lockObject)
            {
                // キャッシュサイズ制限チェック (容量ベース)
                while (_currentCacheMemoryUsage + textureSize > MAX_CACHE_MEMORY_SIZE && ImageCache.Count > 0)
                {
                    RemoveOldestItem();
                }

                var node = _accessOrder.AddLast(path);
                ImageCache[path] = texture;
                
                _nodeMap[path] = node;
                _currentCacheMemoryUsage += textureSize;
                DebugLogger.Log($"Added to cache: {path} (Size: {textureSize / 1024} KB, Total: {_currentCacheMemoryUsage / 1024 / 1024} MB)");
            }
        }

        /// <summary>
        /// LRUアクセス順序を更新
        /// </summary>
        private void UpdateAccessOrder(string path)
        {
            if (_nodeMap.TryGetValue(path, out var node))
            {
                _accessOrder.Remove(node);
                node = _accessOrder.AddLast(path);
                _nodeMap[path] = node;
            }
        }

        /// <summary>
        /// 最も古いアイテムをキャッシュから削除
        /// </summary>
        private void RemoveOldestItem()
        {
            if (_accessOrder.First == null) return;

            var oldestPath = _accessOrder.First.Value;
            RemoveFromCache(oldestPath);
        }
        
        /// <summary>
        /// キャッシュから画像を削除
        /// </summary>
        private void RemoveFromCache(string path)
        {
            if (ImageCache.TryGetValue(path, out var texture))
            {
                DebugLogger.Log($"Removing from cache: {path}");
                if (texture != null)
                {
                    long textureSize = Profiler.GetRuntimeMemorySizeLong(texture);
                    _currentCacheMemoryUsage -= textureSize;
                    if (_currentCacheMemoryUsage < 0) _currentCacheMemoryUsage = 0;
                    
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                ImageCache.Remove(path);
            }

            if (_nodeMap.TryGetValue(path, out var node))
            {
                _accessOrder.Remove(node);
                _nodeMap.Remove(path);
            }
        }

        private void InitializePlaceholder()
        {
            if (_placeholderTexture != null) return;

            _placeholderTexture = new Texture2D(100, 100);
            var pixels = new Color32[100 * 100];
            
            for (int y = 0; y < 100; y++)
            {
                for (int x = 0; x < 100; x++)
                {
                    var isWhite = (x / 10 + y / 10) % 2 == 0;
                    pixels[y * 100 + x] = isWhite
                        ? new Color32(240, 240, 240, 255)
                        : new Color32(200, 200, 200, 255);
                }
            }
            
            _placeholderTexture.SetPixels32(pixels);
            _placeholderTexture.Apply();
        }
    }
}
