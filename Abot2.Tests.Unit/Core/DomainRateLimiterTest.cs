using Abot2.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;

namespace Abot2.Tests.Unit.Core
{
    [TestClass]
    public class DomainRateLimiterTest
    {
        [TestMethod]
        public void Constructor_ZeroCrawlDelay_NoException() => new DomainRateLimiter(0);

        [TestMethod]
        public void Constructor_NegativeCrawlDelay_ThrowsException() => Assert.ThrowsExactly<ArgumentException>(() => new DomainRateLimiter(-1));
        

        [TestMethod]
        public void RateLimit_SameDomain_WaitsBetweenRequests()
        {
            var uri = new Uri("http://a.com/");
            var timer = Stopwatch.StartNew();
            var unitUnderTest = new DomainRateLimiter(100);

            unitUnderTest.RateLimit(uri);
            unitUnderTest.RateLimit(uri);
            unitUnderTest.RateLimit(uri);

            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds > 175);
        }

        [TestMethod]
        public void RateLimit_ZeroAsDefault_SameDomain_DoesNotWaitsBetweenRequests()
        {
            var uri = new Uri("http://a.com/");
            var timer = Stopwatch.StartNew();
            var unitUnderTest = new DomainRateLimiter(0);
            unitUnderTest.RateLimit(uri);
            unitUnderTest.RateLimit(uri);
            unitUnderTest.RateLimit(uri);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds < 50);
        }

        [TestMethod]
        public void RateLimit_DifferentDomain_DoesNotWaitsBetweenRequests()
        {
            var uri1 = new Uri("http://a.com/");
            var uri2 = new Uri("http://b.com/");
            var uri3 = new Uri("http://c.com/");
            var uri4 = new Uri("http://d.com/");

            var timer = Stopwatch.StartNew();
            var unitUnderTest = new DomainRateLimiter(1000);
            unitUnderTest.RateLimit(uri1);
            unitUnderTest.RateLimit(uri2);
            unitUnderTest.RateLimit(uri3);
            unitUnderTest.RateLimit(uri4);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds < 100);
        }

        [TestMethod]
        public void RateLimit_ZeroAsDefault_DifferentDomain_DoesNotWaitsBetweenRequests()
        {
            var uri1 = new Uri("http://a.com/");
            var uri2 = new Uri("http://b.com/");
            var uri3 = new Uri("http://c.com/");
            var uri4 = new Uri("http://d.com/");

            var timer = Stopwatch.StartNew();
            var unitUnderTest = new DomainRateLimiter(0);
            unitUnderTest.RateLimit(uri1);
            unitUnderTest.RateLimit(uri2);
            unitUnderTest.RateLimit(uri3);
            unitUnderTest.RateLimit(uri4);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds < 50);
        }

        [TestMethod]
        public void RateLimit_NullUri() => Assert.ThrowsExactly<ArgumentNullException>(() => new DomainRateLimiter(1000).RateLimit(null));


        [TestMethod]
        public void AddDomain_NullUri() => Assert.ThrowsExactly<ArgumentNullException>(() => new DomainRateLimiter(1000).AddDomain(null, 100));

        [TestMethod]
        public void AddDomain_ZeroCrawlDelay() => Assert.ThrowsExactly<ArgumentException>(() => new DomainRateLimiter(1000).AddDomain(new Uri("http://a.com"), 0));

        [TestMethod]
        public void AddDomain_NegativeCrawlDelay() => Assert.ThrowsExactly<ArgumentException>(() => new DomainRateLimiter(1000).AddDomain(new Uri("http://a.com"), -1));

        [TestMethod]
        public void AddDomain_ParamLessThanDefault_UsesDefault()
        {
            var rootUri = new Uri("http://a.com/");
            var pageUri1 = new Uri("http://a.com/a.html");
            var pageUri2 = new Uri("http://a.com/b.html");

            var timer = Stopwatch.StartNew();
            var unitUnderTest = new DomainRateLimiter(100);

            unitUnderTest.AddDomain(rootUri, 5);

            unitUnderTest.RateLimit(rootUri);
            unitUnderTest.RateLimit(pageUri1);
            unitUnderTest.RateLimit(pageUri2);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds > 190);
        }

        [TestMethod]
        public void AddDomain_ParamGreaterThanDefault_UsesParam()
        {
            var rootUri = new Uri("http://a.com/");
            var pageUri1 = new Uri("http://a.com/a.html");
            var pageUri2 = new Uri("http://a.com/b.html");

            var timer = Stopwatch.StartNew();
            var unitUnderTest = new DomainRateLimiter(5);

            unitUnderTest.AddDomain(rootUri, 100);

            unitUnderTest.RateLimit(rootUri);
            unitUnderTest.RateLimit(pageUri1);
            unitUnderTest.RateLimit(pageUri2);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds > 190);
        }

        [TestMethod]
        public void AddDomain_AddDuplicateDomain_FirstAddWins()
        {
            var domainRateLimiter = new DomainRateLimiter(5);
            var domain = new Uri("http://a.com");

            domainRateLimiter.AddDomain(domain, 50);
            domainRateLimiter.AddDomain(domain, 150);//This should be ignored

            var timer = System.Diagnostics.Stopwatch.StartNew();
            domainRateLimiter.RateLimit(domain);
            domainRateLimiter.RateLimit(domain);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds >= 50 && timer.ElapsedMilliseconds < 150, string.Format("Expected it to take more than 50 but less than 150 but only took {0}", timer.ElapsedMilliseconds));
        }


        [TestMethod]
        public void AddOrUpdateDomain_NullUri() => Assert.ThrowsExactly<ArgumentNullException>(() => new DomainRateLimiter(1000).AddOrUpdateDomain(null, 100));

        [TestMethod]
        public void AddOrUpdateDomain_ZeroCrawlDelay() => Assert.ThrowsExactly<ArgumentException>(() => new DomainRateLimiter(1000).AddOrUpdateDomain(new Uri("http://a.com"), 0));

        [TestMethod]
        public void AddOrUpdateDomain_NegativeCrawlDelay() => Assert.ThrowsExactly<ArgumentException>(() => new DomainRateLimiter(1000).AddOrUpdateDomain(new Uri("http://a.com"), -1));

        [TestMethod]
        public void AddOrUpdateDomain_ParamLessThanDefault_UsesDefault()
        {
            var rootUri = new Uri("http://a.com/");
            var pageUri1 = new Uri("http://a.com/a.html");
            var pageUri2 = new Uri("http://a.com/b.html");

            var timer = Stopwatch.StartNew();
            var unitUnderTest = new DomainRateLimiter(100);

            unitUnderTest.AddOrUpdateDomain(rootUri, 5);

            unitUnderTest.RateLimit(rootUri);
            unitUnderTest.RateLimit(pageUri1);
            unitUnderTest.RateLimit(pageUri2);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds > 190);
        }

        [TestMethod]
        public void AddOrUpdateDomain_ParamGreaterThanDefault_UsesParam()
        {
            var rootUri = new Uri("http://a.com/");
            var pageUri1 = new Uri("http://a.com/a.html");
            var pageUri2 = new Uri("http://a.com/b.html");

            var timer = Stopwatch.StartNew();
            var unitUnderTest = new DomainRateLimiter(5);

            unitUnderTest.AddOrUpdateDomain(rootUri, 100);

            unitUnderTest.RateLimit(rootUri);
            unitUnderTest.RateLimit(pageUri1);
            unitUnderTest.RateLimit(pageUri2);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds > 190);
        }

        [TestMethod]
        public void AddOrUpdateDomain_AddDuplicateDomain_LastUpdateWins()
        {
            var domainRateLimiter = new DomainRateLimiter(5);
            var domain = new Uri("http://a.com");
            
            domainRateLimiter.AddOrUpdateDomain(domain, 50);
            domainRateLimiter.AddOrUpdateDomain(domain, 150);//This should override the previous

            var timer = System.Diagnostics.Stopwatch.StartNew();
            domainRateLimiter.RateLimit(domain);
            domainRateLimiter.RateLimit(domain);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds >= 100, "Expected it to take more than 100 millisecs but only took " + timer.ElapsedMilliseconds);
        }


        [TestMethod]
        public void RemoveDomain_NoLongerRateLimitsThatDomain()
        {
            //Arrange
            var domainRateLimiter = new DomainRateLimiter(5);
            var domain = new Uri("http://a.com");

            domainRateLimiter.AddDomain(domain, 1000);

            //Act
            domainRateLimiter.RemoveDomain(domain);

            //Assert
            var timer = System.Diagnostics.Stopwatch.StartNew();
            domainRateLimiter.RateLimit(domain);
            domainRateLimiter.RateLimit(domain);
            timer.Stop();

            Assert.IsTrue(timer.ElapsedMilliseconds < 100, "Expected it to take less than 100 millisecs but only took " + timer.ElapsedMilliseconds);
        }

        [TestMethod]
        public void Dispose_ThenRateLimit_ThrowsObjectDisposedException()
        {
            var unitUnderTest = new DomainRateLimiter(100);
            unitUnderTest.RateLimit(new Uri("http://a.com/"));

            unitUnderTest.Dispose();

            Assert.ThrowsExactly<ObjectDisposedException>(() => unitUnderTest.RateLimit(new Uri("http://a.com/")));
        }

        [TestMethod]
        public void Dispose_AfterUpdatingAndRemovingDomains_DoesNotThrow()
        {
            var uri = new Uri("http://a.com/");
            var unitUnderTest = new DomainRateLimiter(100);
            unitUnderTest.AddDomain(uri, 100);
            unitUnderTest.AddOrUpdateDomain(uri, 200);
            unitUnderTest.RemoveDomain(uri);

            unitUnderTest.Dispose();
            unitUnderTest.Dispose();
        }
    }
}
