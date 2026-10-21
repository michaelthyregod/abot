using Abot2.Util;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Serilog;

namespace Abot2.Core
{
    /// <summary>
    /// Rate limits or throttles on a per domain basis
    /// </summary>
    public interface IDomainRateLimiter
    {
        /// <summary>
        /// If the domain of the param has been flagged for rate limiting, it will be rate limited according to the configured minimum crawl delay
        /// </summary>
        void RateLimit(Uri uri);

        /// <summary>
        /// Add a domain entry so that domain may be rate limited according the the param minumum crawl delay
        /// </summary>
        void AddDomain(Uri uri, long minCrawlDelayInMillisecs);

        /// <summary>
        /// Add/Update a domain entry so that domain may be rate limited according the the param minumum crawl delay
        /// </summary>
        void AddOrUpdateDomain(Uri uri, long minCrawlDelayInMillisecs);

        /// <summary>
        /// Remove a domain entry so that it will no longer be rate limited
        /// </summary>
        void RemoveDomain(Uri uri);
    }

    /// <summary>
    /// Rate limits per domain. Owns the rate limiters it creates and disposes them when it is disposed.
    /// </summary>
    public class DomainRateLimiter : IDomainRateLimiter, IDisposable
    {
        protected ConcurrentDictionary<string, IRateLimiter> _rateLimiterLookup = new ConcurrentDictionary<string, IRateLimiter>();
        long _defaultMinCrawlDelayInMillisecs;

        //Rate limiters replaced or removed while another thread may still be waiting on them, disposed with this instance
        readonly ConcurrentBag<IRateLimiter> _retiredRateLimiters = new ConcurrentBag<IRateLimiter>();
        int _isDisposed;

        public DomainRateLimiter(long minCrawlDelayMillisecs)
        {
            if (minCrawlDelayMillisecs < 0)
                throw new ArgumentException("minCrawlDelayMillisecs");

            if(minCrawlDelayMillisecs > 0)
                _defaultMinCrawlDelayInMillisecs = minCrawlDelayMillisecs + 20;//IRateLimiter is always a little under so adding a little more time
        }

        public void RateLimit(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            var rateLimiter = GetRateLimiter(uri, _defaultMinCrawlDelayInMillisecs);
            if (rateLimiter == null)
                return;

            var timer = Stopwatch.StartNew();
            rateLimiter.WaitToProceed();
            timer.Stop();

            if(timer.ElapsedMilliseconds > 10)
                Log.Debug("Rate limited [{0}] [{1}] milliseconds", uri.AbsoluteUri, timer.ElapsedMilliseconds);
        }

        public void AddDomain(Uri uri, long minCrawlDelayInMillisecs)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            if (minCrawlDelayInMillisecs < 1)
                throw new ArgumentException("minCrawlDelayInMillisecs");

            GetRateLimiter(uri, Math.Max(minCrawlDelayInMillisecs, _defaultMinCrawlDelayInMillisecs));//just calling this method adds the new domain
        }

        public void AddOrUpdateDomain(Uri uri, long minCrawlDelayInMillisecs)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            if (minCrawlDelayInMillisecs < 1)
                throw new ArgumentException("minCrawlDelayInMillisecs");

            ThrowIfDisposed();

            var delayToUse = Math.Max(minCrawlDelayInMillisecs, _defaultMinCrawlDelayInMillisecs);
            if (delayToUse > 0)
            {
                var rateLimiter = new RateLimiter(1, TimeSpan.FromMilliseconds(delayToUse));

                _rateLimiterLookup.AddOrUpdate(uri.Authority, rateLimiter, (key, oldValue) =>
                {
                    _retiredRateLimiters.Add(oldValue);
                    return rateLimiter;
                });
                Log.Debug("Added/updated domain [{0}] with minCrawlDelayInMillisecs of [{1}] milliseconds", uri.Authority, delayToUse);
            }
        }

        public void RemoveDomain(Uri uri)
        {
            IRateLimiter rateLimiter;
            if (_rateLimiterLookup.TryRemove(uri.Authority, out rateLimiter))
                _retiredRateLimiters.Add(rateLimiter);
        }

        protected virtual IRateLimiter GetRateLimiter(Uri uri, long minCrawlDelayInMillisecs)
        {
            ThrowIfDisposed();

            IRateLimiter rateLimiter;
            _rateLimiterLookup.TryGetValue(uri.Authority, out rateLimiter);

            if (rateLimiter == null && minCrawlDelayInMillisecs > 0)
            {
                var newRateLimiter = new RateLimiter(1, TimeSpan.FromMilliseconds(minCrawlDelayInMillisecs));

                if (_rateLimiterLookup.TryAdd(uri.Authority, newRateLimiter))
                {
                    rateLimiter = newRateLimiter;
                    Log.Debug("Added new domain [{0}] with minCrawlDelayInMillisecs of [{1}] milliseconds", uri.Authority, minCrawlDelayInMillisecs);
                }
                else
                {
                    //Another thread added this domain first, use its rate limiter so the domain shares one limit
                    newRateLimiter.Dispose();
                    _rateLimiterLookup.TryGetValue(uri.Authority, out rateLimiter);
                }
            }

            return rateLimiter;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            if (disposing)
            {
                foreach (var rateLimiter in _rateLimiterLookup.Values)
                    (rateLimiter as IDisposable)?.Dispose();
                _rateLimiterLookup.Clear();

                while (_retiredRateLimiters.TryTake(out var rateLimiter))
                    (rateLimiter as IDisposable)?.Dispose();
            }
        }

        protected void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _isDisposed) != 0)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
