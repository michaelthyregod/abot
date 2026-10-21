using System;
using System.Collections.Concurrent;

namespace Abot2.Core
{
    /// <summary>
    /// Implementation that stores a numeric hash of the url instead of the url itself to use for lookups. This should save space when the crawled url list gets very long. 
    /// </summary>
    public class CompactCrawledUrlRepository : ICrawledUrlRepository
    {
        private ConcurrentDictionary<long, byte> _mUrlRepository = new ConcurrentDictionary<long, byte>();

        /// <inheritDoc />
        public bool Contains(Uri uri)
        {
            return GetRepository().ContainsKey(uri.GetHashCode());
        }

        /// <inheritDoc />
        public bool AddIfNew(Uri uri)
        {
            return GetRepository().TryAdd(uri.GetHashCode(), 0);
        }

        /// <inheritDoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                _mUrlRepository = null;
        }

        private ConcurrentDictionary<long, byte> GetRepository()
        {
            return _mUrlRepository ?? throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
