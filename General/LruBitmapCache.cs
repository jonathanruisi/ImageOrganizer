using ImageOrganizer.ViewModel;

using Microsoft.Graphics.Canvas;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ImageOrganizer
{
    public partial class LruBitmapCache : IDisposable
    {
        #region Fields
        private readonly Dictionary<string, LinkedListNode<ImageFile>> _cacheMap = [];
        private readonly LinkedList<ImageFile> _lruList = new();
        private int _capacity;
        private float _dpi;
        private bool disposed;
        #endregion

        #region Properties
        public int Capacity
        {
            get => _capacity;
            set
            {
                _capacity = value;
                while (_cacheMap.Count > _capacity)
                    RemoveLeastRecentlyUsed();
            }
        }

        public float Dpi
        {
            get => _dpi;
            set
            {
                var clearCache = _dpi != value;
                _dpi = value;

                if (clearCache)
                    ClearCache();
            }
        }

        public int Count => _cacheMap.Count;
        #endregion

        #region Constructor
        public LruBitmapCache(int capacity, int dpi = 96)
        {
            Capacity = capacity;
            Dpi = dpi;
        }
        #endregion

        #region Public Methods
        public async Task<CanvasBitmap?> GetOrLoadImageAsync(ICanvasResourceCreator resourceCreator, ImageFile imageFile)
        {
            if (_cacheMap.TryGetValue(imageFile.Path, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                if (node.Value.Bitmap is not null)
                {
                    //Debug.WriteLine($"RETRIEVED {imageFile.Name}");
                    return node.Value.Bitmap;
                }
            }

            var cacheResult = await imageFile.Cache(resourceCreator, _dpi);
            //Debug.WriteLine($"{(cacheResult ? "CACHED" : "FAILED TO CACHE")} {imageFile.Name}");
            if (cacheResult == false)
                return null;

            var newNode = new LinkedListNode<ImageFile>(imageFile);
            _lruList.AddFirst(newNode);
            _cacheMap[imageFile.Path] = newNode;

            if (_cacheMap.Count > _capacity)
                RemoveLeastRecentlyUsed();

            return imageFile.Bitmap;
        }

        public void RemoveImage(string path)
        {
            if (_cacheMap.TryGetValue(path, out var node))
            {
                _lruList.Remove(node);
                _cacheMap.Remove(path);
                node.Value.ReleaseCache();
                //Debug.WriteLine($"REMOVED {path}");
            }
        }

        public void ClearCache()
        {
            foreach (var node in _lruList)
                node.ReleaseCache();
            _cacheMap.Clear();
            _lruList.Clear();
        }
        #endregion

        #region Private Methods
        private void RemoveLeastRecentlyUsed()
        {
            var node = _lruList.Last;
            if (node != null)
            {
                //Debug.WriteLine($"REMOVED {node.Value.Name}");
                _cacheMap.Remove(node.Value.Path);
                node.Value.ReleaseCache();
                _lruList.RemoveLast();
            }
        }
        #endregion

        #region Interface Implementation (IDisposable)
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    ClearCache();
                }

                disposed = true;
            }
        }
        #endregion
    }
}