using ImageOrganizer.ViewModel;

using Microsoft.Graphics.Canvas;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImageOrganizer
{
    public partial class LruBitmapCache : IDisposable
    {
        #region Fields
        private readonly Dictionary<string, LinkedListNode<ImageFile>> _cacheMap = [];
        private readonly LinkedList<ImageFile> _lruList = new();
        private readonly SemaphoreSlim _loadGate = new(1, 1);
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
        public async Task<bool> LoadImageAsync(ICanvasResourceCreator resourceCreator, ImageFile imageFile)
        {
            await _loadGate.WaitAsync();
            try
            {
                if (_cacheMap.TryGetValue(imageFile.Path, out var node))
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    if (node.Value.Bitmap is not null)
                        return true;
                }

                var cacheResult = await imageFile.Render(resourceCreator, _dpi);
                if (cacheResult == false)
                    return false;

                var newNode = new LinkedListNode<ImageFile>(imageFile);
                _lruList.AddFirst(newNode);
                _cacheMap[imageFile.Path] = newNode;

                if (_cacheMap.Count > _capacity)
                    RemoveLeastRecentlyUsed();

                return true;
            }
            finally
            {
                _loadGate.Release();
            }
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