using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abot2.Crawler;
using Abot2.Poco;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;

namespace Abot2.Tests.Integration
{
    [TestClass]
    public class CrawlTest : CrawlTestBase
    {
        public CrawlTest()
            :base(SetupAssemblyInitializer.Site.BaseUri, 25)
        {

        }

        [TestMethod]
        public async Task Crawl_VerifyCrawlResultIsAsExpected()
        {
            var config = new CrawlConfiguration()
            {
                IsExternalPageCrawlingEnabled = true
            };
            await base.CrawlAndAssert(new PoliteWebCrawler(config));
        }

        [TestMethod]
        public async Task Crawl_MaxPagesTo5_OnlyCrawls5Pages()
        {
            var configuration = new CrawlConfiguration
            {
                IsExternalPageCrawlingEnabled = true,
                MaxPagesToCrawl = 5
            };

            var pagesCrawledCount = 0;

            var crawler = new PoliteWebCrawler(configuration, null, null, null, null, null, null, null, null);
            crawler.PageCrawlCompleted += (a, b) => Interlocked.Increment(ref pagesCrawledCount);

            await crawler.CrawlAsync(SetupAssemblyInitializer.Site.BaseUri);

            Assert.AreEqual(5, pagesCrawledCount);
        }

        [TestMethod]
        public async Task Crawl_MaxPagesTo25_OnlyCrawls25Pages()
        {
            var configuration = new CrawlConfiguration();
            configuration.MaxPagesToCrawl = 25;

            var pagesCrawledCount = 0;

            var crawler = new PoliteWebCrawler(configuration, null, null, null, null, null, null, null, null);
            crawler.PageCrawlCompleted += (a, b) => Interlocked.Increment(ref pagesCrawledCount);

            await crawler.CrawlAsync(SetupAssemblyInitializer.Site.BaseUri);

            Assert.AreEqual(25, pagesCrawledCount);
        }

        [TestMethod]
        public async Task Crawl_MaxPagesTo5_WithCrawlDelay_OnlyCrawls5Pages()
        {
            var configuration = new CrawlConfiguration();
            configuration.MinCrawlDelayPerDomainMilliSeconds = 1000; //adding delay since it increases the chance of issues with abot crawling more than MaxPagesToCrawl.
            configuration.MaxPagesToCrawl = 5;

            var pagesCrawledCount = 0;

            var crawler = new PoliteWebCrawler(configuration, null, null, null, null, null, null, null, null);
            crawler.PageCrawlCompleted += (a, b) => Interlocked.Increment(ref pagesCrawledCount);

            await crawler.CrawlAsync(SetupAssemblyInitializer.Site.BaseUri);

            Assert.AreEqual(5, pagesCrawledCount);
        }

        [TestMethod]
        public async Task Crawl_CrawlTimeoutIs1Sec_TimesOut()
        {
            var configuration = new CrawlConfiguration();
            configuration.CrawlTimeoutSeconds = 2;

            var pagesCrawledCount = 0;

            var crawler = new PoliteWebCrawler(configuration, null, null, null, null, null, null, null, null);
            crawler.PageCrawlCompleted += (a, b) => Interlocked.Increment(ref pagesCrawledCount);

            var result = await crawler.CrawlAsync(SetupAssemblyInitializer.SlowSite.BaseUri);

            Assert.IsFalse(result.ErrorOccurred);
            Assert.IsTrue(result.Elapsed.TotalSeconds < 8, "Took more than 8 seconds");
            Assert.IsTrue(pagesCrawledCount < 2, "Crawled more than 2 pages");
        }

        [TestMethod]
        public async Task Crawl_Synchronous_CancellationTokenCancelled_StopsCrawl()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var timer = new System.Timers.Timer(800);
            timer.Elapsed += (o, e) =>
            {
                cancellationTokenSource.Cancel();
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();

            var crawler = new PoliteWebCrawler();
            var result = await crawler.CrawlAsync(SetupAssemblyInitializer.SlowSite.BaseUri, cancellationTokenSource);

            Assert.IsTrue(result.ErrorOccurred);
            Assert.IsTrue(result.ErrorException is OperationCanceledException);
        }

        [TestMethod]
        public void Crawl_Asynchronous_CancellationTokenCancelled_StopsCrawl()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var timer = new System.Timers.Timer(800);
            timer.Elapsed += (o, e) =>
            {
                cancellationTokenSource.Cancel();
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();

            var crawler = new PoliteWebCrawler();
            var task = Task.Factory.StartNew<CrawlResult>( () => crawler.CrawlAsync(SetupAssemblyInitializer.SlowSite.BaseUri, cancellationTokenSource).Result);
            var result = task.Result;

            Assert.IsTrue(result.ErrorOccurred);
            Assert.IsTrue(result.ErrorException is OperationCanceledException);
        }

        [TestMethod]
        public async Task Crawl_IsRateLimited()
        {
            var configuration = new CrawlConfiguration();
            configuration.MaxPagesToCrawl = 3;
            configuration.MinCrawlDelayPerDomainMilliSeconds = 1000; // 1 second * 2 pages = 2 (or more) seconds

            var pagesCrawledCount = 0;

            var crawler = new PoliteWebCrawler(configuration);
            crawler.PageCrawlCompleted += (a, b) => Interlocked.Increment(ref pagesCrawledCount);

            var uriToCrawl = SetupAssemblyInitializer.Site.BaseUri;
            var start = DateTime.Now;
            await crawler.CrawlAsync(uriToCrawl);
            var elapsed = DateTime.Now - start;

            Assert.IsTrue(elapsed.TotalMilliseconds > 2000);
            Assert.AreEqual(3, pagesCrawledCount);
        }

        [TestMethod]
        public async Task Crawl_RetryEnabled_VerifyCrawlResultIsAsExpected()
        {
            CrawlConfiguration configuration = new CrawlConfiguration
            {
                MaxRetryCount = 3,
                MinRetryDelayInMilliseconds = 2000,
                IsExternalPageCrawlingEnabled = true
            };

            await base.CrawlAndAssert(new PoliteWebCrawler(configuration));
        }

        protected override List<PageResult> GetExpectedCrawlResult()
        {
            return SetupAssemblyInitializer.Site.GetAllPages();
        }
    }
}
