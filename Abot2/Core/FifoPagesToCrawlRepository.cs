using Abot2.Poco;
using System;
using System.Collections.Concurrent;

namespace Abot2.Core
{
    public interface IPagesToCrawlRepository : IDisposable
    {
        void Add(PageToCrawl page);
        PageToCrawl GetNext();
        void Clear();
        int Count();

    }

    public class FifoPagesToCrawlRepository : IPagesToCrawlRepository
    {
        private ConcurrentQueue<PageToCrawl> _urlQueue = new ConcurrentQueue<PageToCrawl>();

        public void Add(PageToCrawl page)
        {
            GetQueue().Enqueue(page);
        }

        public PageToCrawl GetNext()
        {
            PageToCrawl pageToCrawl;
            GetQueue().TryDequeue(out pageToCrawl);

            return pageToCrawl;
        }

        public void Clear()
        {
            GetQueue();
            _urlQueue = new ConcurrentQueue<PageToCrawl>();
        }

        public int Count()
        {
            return GetQueue().Count;
        }

        public virtual void Dispose()
        {
            _urlQueue = null;
        }

        private ConcurrentQueue<PageToCrawl> GetQueue()
        {
            return _urlQueue ?? throw new ObjectDisposedException(GetType().FullName);
        }
    }

}
