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

        /// <summary>キャッシュの最大サイズ (件数ベース - 非推奨)</summary>
        // private int MAX_CACHE_SIZE = 50;

        /// <summary>キャッシュの最大容量 (バイト) - デフォルト 128MB</summary>
        private const long MAX_CACHE_MEMORY_SIZE = 128L * 1024L * 1024L;

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
        /// <exception cref="FileNotFoundException">指定されたパスにファイルが存在しない場合</exception>
        /// <exception cref="IOException">ファイルの読み込みに失敗した場合</exception>
        public Texture2D? LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return _placeholderTexture;

            if (ImageCache.TryGetValue(path, out var cachedTexture))
            {
                // LRU更新: 最近使用したアイテムをリストの末尾に移動
                UpdateAccessOrder(path);
                return cachedTexture;
            }

            // 既に読み込み中の場合はプレースホルダーを返す
            if (_loadingImages.Contains(path))
            {
                return _placeholderTexture;
            }

            // URLの場合は非同期読み込みを開始してプレースホルダーを返す
            if (path.StartsWith("http://") || path.StartsWith("https://"))
            {
                DebugLogger.Log($"Requested URL texture: {path}");
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
            DebugLogger.Log($"Queueing async load for: {path}");
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
                if (fileInfo.Length > 2 * 1024 * 1024) return false; // 2MB以下のファイルは同期読み込み（ほとんどの画像をカバー）
                
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
            while (_mainThreadQueue.Count > 0 && processCount < 10) // 1フレームで最大10個処理
            {
                var action = _mainThreadQueue.Dequeue();
                action?.Invoke();
                processCount++;
            }
        }

        /// <summary>
        /// 大きい画像の非同期読み込み（Task.Run版）
        /// </summary>
        private void LoadLargeImageAsync(string path, Action<Texture2D?>? onComplete)
        {
            try
            {
                if (!File.Exists(path))
                {
                    _mainThreadQueue.Enqueue(() =>
                    {
                        _loadingImages.Remove(path);
                        onComplete?.Invoke(null);
                    });
                    
                    return;
                }

                var bytes = File.ReadAllBytes(path);
                
                // メインスレッドでテクスチャ作成
                _mainThreadQueue.Enqueue(() => {
                    CreateTextureFromBytesSync(path, bytes, onComplete);
                });
            }
            catch (Exception ex)
            {
                DebugLogger.LogError(string.Format(LocalizationService.Instance.GetString("error_large_image_load_failed"), path, ex.Message));

                _mainThreadQueue.Enqueue(() => {
                    _loadingImages.Remove(path);
                    onComplete?.Invoke(null);
                });
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
            
            _loadingImages.Remove(path);
            onComplete?.Invoke(texture);
            
            // UI更新
            if (EditorWindow.focusedWindow != null) EditorWindow.focusedWindow.Repaint();
        }

        /// <summary>
        /// テクスチャを非同期で読み込む
        /// </summary>
        /// <param name="path">読み込むテクスチャのパス</param>
        /// <param name="onComplete">読み込み完了時のコールバック</param>
        /// <param name="priority">読み込み優先度（高い値ほど優先）</param>
        public void LoadTextureAsync(string path, Action<Texture2D?>? onComplete = null, int priority = 0)
        {
            if (string.IsNullOrEmpty(path))
            {
                onComplete?.Invoke(null);
                return;
            }

            // 既にキャッシュに存在する場合
            if (ImageCache.TryGetValue(path, out var cachedTexture))
            {
                // Detailed debug log might be too spammy even for debug mode, but let's add it for "detailed" tracing if needed.
                // DebugLogger.Log($"Cache hit for: {path}");
                UpdateAccessOrder(path);
                onComplete?.Invoke(cachedTexture);
                return;
            }

            // 既に読み込み中の場合はスキップ
            if (_loadingImages.Contains(path))
            {
                // DebugLogger.Log($"Image loading already in progress, skipping: {path}");
                return;
            }

            _loadingImages.Add(path);

            if (path.StartsWith("http://") || path.StartsWith("https://"))
            {
                LoadUrlImage(path, onComplete);
            }
            else
            {
                // 大きいファイルは直接Task.Runで処理（EditorCoroutineより高速）
                Task.Run(() => LoadLargeImageAsync(path, onComplete));
            }
        }

        /// <summary>
        /// URLから画像を読み込む
        /// </summary>
        private void LoadUrlImage(string url, Action<Texture2D?>? onComplete)
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
                        _loadingImages.Remove(url);
                        onComplete?.Invoke(null);
                    }
                    else
                    {
                        DebugLogger.Log($"Image downloaded: {url}");
                        var texture = DownloadHandlerTexture.GetContent(uwr);
                        if (texture != null)
                        {
                            AddToCache(url, texture);
                        }
                        _loadingImages.Remove(url);
                        onComplete?.Invoke(texture);

                        if (EditorWindow.focusedWindow != null) EditorWindow.focusedWindow.Repaint();
                    }
                }
            });
        }

        /// <summary>
        /// 画像キャッシュをクリア
        /// メモリ使用量を削減するために使用する
        /// </summary>
        public void ClearCache()
        {DebugLogger.Log($"Clearing image cache. Count: {ImageCache.Count}");
            
            // 全テクスチャを適切に解放
            foreach (var texture in ImageCache.Values)
            {
                if (texture != null && texture != _placeholderTexture)
                {
                    try
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogWarning($"テクスチャの破棄に失敗しました: {ex.Message}");
                    }
                }
            }

            ImageCache.Clear();
            _accessOrder.Clear();
            _nodeMap.Clear();
            _currentVisibleImages.Clear();
            _loadingImages.Clear();
            _currentCacheMemoryUsage = 0;
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
            catch (Exception ex)
            {
                DebugLogger.LogWarning($"ImageService dispose中にエラーが発生しました: {ex.Message}");
            }
            finally
            {
                instance = null;
            }
        }

        /// <summary>
        /// 現在表示中のアイテムの画像を再読み込み
        /// </summary>
        /// <param name="items">再読み込みするアイテムのリスト</param>
        public void ReloadCurrentItemsImages(IEnumerable<IDatabaseItem> items)
        {
            foreach (var item in items)
            {
                string imagePath = item.GetImagePath();

                if (!string.IsNullOrEmpty(imagePath))
                {
                    if (imagePath.StartsWith("http://") || imagePath.StartsWith("https://") || File.Exists(imagePath))
                    {
                        LoadTexture(imagePath);
                    }
                }
            }
        }

        /// <summary>
        /// 表示中アイテムの画像を更新し、不要な画像を削除
        /// </summary>
        /// <param name="visibleItems">現在表示中のアイテム</param>
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

                    // まだキャッシュにない場合のみ読み込み
                    if (!ImageCache.ContainsKey(imagePath))
                    {
                        if (imagePath.StartsWith("http://") || imagePath.StartsWith("https://") || File.Exists(imagePath))
                        {
                            LoadTexture(imagePath);
                        }
                    }
                }
            }

            // 不要になった画像をキャッシュから削除（LRUに任せるため、即時削除は行わない）
            /*
            var imagesToRemove = _currentVisibleImages.Except(newVisibleImages).ToList();
            foreach (var imagePath in imagesToRemove)
            {
                RemoveFromCache(imagePath);
            }
            */

            _currentVisibleImages.Clear();
            foreach (var path in newVisibleImages)
            {
                _currentVisibleImages.Add(path);
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
        /// (容量ベースへの移行に伴い廃止)
        /// </summary>
        /// <param name="searchResultCount">検索結果の件数</param>
        public void AdaptCacheSizeToSearchResults(int searchResultCount)
        {
            // 容量ベースの管理に移行したため、件数による調整は行わない
            // 将来的にメモリ制限を動的に変える必要がある場合はここに実装
        }

        /// <summary>
        /// キャッシュにテクスチャを追加
        /// </summary>
        private void AddToCache(string path, Texture2D texture)
        {
            long textureSize = Profiler.GetRuntimeMemorySizeLong(texture);

            // キャッシュサイズ制限チェック (容量ベース)
            while (_currentCacheMemoryUsage + textureSize > MAX_CACHE_MEMORY_SIZE && ImageCache.Count > 0)
            {
                RemoveOldestItem();
            }

            var node = _accessOrder.AddLast(path);
            ImageCache[path] = texture;
            _nodeMap[path] = node;
            _currentCacheMemoryUsage += textureSize;
            
            // DebugLogger.Log($"Added to cache: {Path.GetFileName(path)} ({textureSize / 1024}KB). Total: {_currentCacheMemoryUsage / 1024 / 1024}MB / {MAX_CACHE_MEMORY_SIZE / 1024 / 1024}MB");
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
        /// 指定された数の古いアイテムを削除
        /// </summary>
        private void RemoveOldestItems(int count)
        {
            for (int i = 0; i < count && _accessOrder.Count > 0; i++)
            {
                RemoveOldestItem();
            }
        }

        /// <summary>
        /// キャッシュから画像を削除
        /// </summary>
        private void RemoveFromCache(string path)
        {
            if (ImageCache.TryGetValue(path, out var texture))
            {
                if (texture != null)
                {
                    long textureSize = Profiler.GetRuntimeMemorySizeLong(texture);
                    _currentCacheMemoryUsage -= textureSize;
                    if (_currentCacheMemoryUsage < 0) _currentCacheMemoryUsage = 0;
                    
                    DebugLogger.Log($"Removed from cache: {Path.GetFileName(path)} (Freed: {textureSize / 1024}KB). Total: {_currentCacheMemoryUsage / 1024 / 1024}MB");
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

        /// <summary>
        /// 検索結果数に応じた最適なキャッシュサイズを取得
        /// </summary>
        private int GetOptimalCacheSize(int searchResultCount)
        {
            if (searchResultCount <= 10) return 20;      // 小さい結果: 2ページ分
            if (searchResultCount <= 100) return 50;     // 中程度: 適度なキャッシュ
            return 30;                                   // 大きい結果: 省メモリ
        }
        
        /// <summary>
        /// プレースホルダーテクスチャを初期化
        /// </summary>
        private void InitializePlaceholder()
        {
            if (_placeholderTexture != null) return;

            _placeholderTexture = new Texture2D(100, 100);
            var pixels = new Color32[100 * 100];
            
            // シンプルなチェッカーボードパターンを生成
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
