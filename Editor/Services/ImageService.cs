// Copyright (c) 2025 sakurayuki
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
using UnityEngine;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// 画像操作を支援するサービスクラス
    /// テクスチャの読み込み、キャッシュ管理、アイテム画像パスの取得機能を提供する
    /// </summary>
    public class ImageServices
    {
        private static ImageServices? instance;

        /// <summary>キャッシュの最大サイズ</summary>
        private int MAX_CACHE_SIZE = 50;

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

            // 即座に同期読み込みを試行（小さいファイル用）
            if (TryLoadSmallImageSync(path, out var texture))
            {
                return texture;
            }

            // 大きいファイルは非同期読み込み
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
                Debug.LogError(string.Format(LocalizationService.Instance.GetString("error_large_image_load_failed"), path, ex.Message));

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
                Debug.LogError(string.Format(LocalizationService.Instance.GetString("error_texture_creation_failed"), path, ex.Message));
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
                UpdateAccessOrder(path);
                onComplete?.Invoke(cachedTexture);
                return;
            }

            // 既に読み込み中の場合はスキップ
            if (_loadingImages.Contains(path))
            {
                return;
            }

            // 大きいファイルは直接Task.Runで処理（EditorCoroutineより高速）
            _loadingImages.Add(path);
            Task.Run(() => LoadLargeImageAsync(path, onComplete));
        }

        /// <summary>
        /// 画像キャッシュをクリア
        /// メモリ使用量を削減するために使用する
        /// </summary>
        public void ClearCache()
        {
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
                        Debug.LogWarning($"テクスチャの破棄に失敗しました: {ex.Message}");
                    }
                }
            }

            ImageCache.Clear();
            _accessOrder.Clear();
            _nodeMap.Clear();
            _currentVisibleImages.Clear();
            _loadingImages.Clear();
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
                Debug.LogWarning($"ImageService dispose中にエラーが発生しました: {ex.Message}");
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

                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    LoadTexture(imagePath);
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
                    if (!ImageCache.ContainsKey(imagePath) && File.Exists(imagePath))
                    {
                        LoadTexture(imagePath);
                    }
                }
            }

            // 不要になった画像をキャッシュから削除
            var imagesToRemove = _currentVisibleImages.Except(newVisibleImages).ToList();
            foreach (var imagePath in imagesToRemove)
            {
                RemoveFromCache(imagePath);
            }

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
        /// </summary>
        /// <param name="searchResultCount">検索結果の件数</param>
        public void AdaptCacheSizeToSearchResults(int searchResultCount)
        {
            var newMaxSize = GetOptimalCacheSize(searchResultCount);
            if (newMaxSize < ImageCache.Count)
            {
                // キャッシュサイズを削減
                RemoveOldestItems(ImageCache.Count - newMaxSize);
            }

            MAX_CACHE_SIZE = newMaxSize;
        }

        /// <summary>
        /// キャッシュにテクスチャを追加
        /// </summary>
        private void AddToCache(string path, Texture2D texture)
        {
            // キャッシュサイズ制限チェック
            while (ImageCache.Count >= MAX_CACHE_SIZE)
            {
                RemoveOldestItem();
            }

            var node = _accessOrder.AddLast(path);
            ImageCache[path] = texture;
            _nodeMap[path] = node;
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
                UnityEngine.Object.DestroyImmediate(texture);
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
