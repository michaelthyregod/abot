using Abot2.Core;
using Abot2.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Abot2.Tests.Unit.Core
{
    //Rate limiters run on a fake clock that only moves when a test advances it, so results never depend on machine load.
    //A RateLimit() call that had to wait would never return, so a call that returns did not wait.
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
            var time = new ManualTimeProvider();
            var unitUnderTest = new InspectableDomainRateLimiter(100, time);

            unitUnderTest.RateLimit(uri);

            //100 ms plus the 20 ms margin DomainRateLimiter adds to its default delay
            var rateLimiter = unitUnderTest.RateLimiterFor(uri);
            Assert.AreEqual(120, rateLimiter.TimeUnitMilliseconds);
            Assert.IsFalse(rateLimiter.WaitToProceed(0), "The next request should wait");

            time.Advance(TimeSpan.FromMilliseconds(119));
            Assert.IsFalse(rateLimiter.WaitToProceed(0), "The delay has not passed yet");

            time.Advance(TimeSpan.FromMilliseconds(1));
            Assert.IsTrue(rateLimiter.WaitToProceed(0), "The delay has passed");
        }

        [TestMethod]
        public void RateLimit_ZeroAsDefault_SameDomain_DoesNotWaitsBetweenRequests()
        {
            var uri = new Uri("http://a.com/");
            var unitUnderTest = new InspectableDomainRateLimiter(0, new ManualTimeProvider());

            unitUnderTest.RateLimit(uri);
            unitUnderTest.RateLimit(uri);
            unitUnderTest.RateLimit(uri);

            Assert.IsNull(unitUnderTest.RateLimiterFor(uri));
        }

        [TestMethod]
        public void RateLimit_DifferentDomain_DoesNotWaitsBetweenRequests()
        {
            var uri1 = new Uri("http://a.com/");
            var uri2 = new Uri("http://b.com/");
            var uri3 = new Uri("http://c.com/");
            var uri4 = new Uri("http://d.com/");

            var unitUnderTest = new InspectableDomainRateLimiter(1000, new ManualTimeProvider());
            unitUnderTest.RateLimit(uri1);
            unitUnderTest.RateLimit(uri2);
            unitUnderTest.RateLimit(uri3);
            unitUnderTest.RateLimit(uri4);

            //Each domain is limited on its own
            Assert.AreNotSame(unitUnderTest.RateLimiterFor(uri1), unitUnderTest.RateLimiterFor(uri2));
            Assert.AreNotSame(unitUnderTest.RateLimiterFor(uri3), unitUnderTest.RateLimiterFor(uri4));
        }

        [TestMethod]
        public void RateLimit_ZeroAsDefault_DifferentDomain_DoesNotWaitsBetweenRequests()
        {
            var uri1 = new Uri("http://a.com/");
            var uri2 = new Uri("http://b.com/");
            var uri3 = new Uri("http://c.com/");
            var uri4 = new Uri("http://d.com/");

            var unitUnderTest = new InspectableDomainRateLimiter(0, new ManualTimeProvider());
            unitUnderTest.RateLimit(uri1);
            unitUnderTest.RateLimit(uri2);
            unitUnderTest.RateLimit(uri3);
            unitUnderTest.RateLimit(uri4);

            Assert.IsNull(unitUnderTest.RateLimiterFor(uri1));
            Assert.IsNull(unitUnderTest.RateLimiterFor(uri4));
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

            var unitUnderTest = new InspectableDomainRateLimiter(100, new ManualTimeProvider());

            unitUnderTest.AddDomain(rootUri, 5);

            Assert.AreEqual(120, unitUnderTest.RateLimiterFor(rootUri).TimeUnitMilliseconds);
            Assert.AreSame(unitUnderTest.RateLimiterFor(rootUri), unitUnderTest.RateLimiterFor(pageUri1), "Pages of a domain share its rate limiter");
        }

        [TestMethod]
        public void AddDomain_ParamGreaterThanDefault_UsesParam()
        {
            var rootUri = new Uri("http://a.com/");
            var pageUri1 = new Uri("http://a.com/a.html");

            var unitUnderTest = new InspectableDomainRateLimiter(5, new ManualTimeProvider());

            unitUnderTest.AddDomain(rootUri, 100);

            Assert.AreEqual(100, unitUnderTest.RateLimiterFor(rootUri).TimeUnitMilliseconds);
            Assert.AreSame(unitUnderTest.RateLimiterFor(rootUri), unitUnderTest.RateLimiterFor(pageUri1), "Pages of a domain share its rate limiter");
        }

        [TestMethod]
        public void AddDomain_AddDuplicateDomain_FirstAddWins()
        {
            var domainRateLimiter = new InspectableDomainRateLimiter(5, new ManualTimeProvider());
            var domain = new Uri("http://a.com");

            domainRateLimiter.AddDomain(domain, 50);
            domainRateLimiter.AddDomain(domain, 150);//This should be ignored

            Assert.AreEqual(50, domainRateLimiter.RateLimiterFor(domain).TimeUnitMilliseconds);
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

            var unitUnderTest = new InspectableDomainRateLimiter(100, new ManualTimeProvider());

            unitUnderTest.AddOrUpdateDomain(rootUri, 5);

            Assert.AreEqual(120, unitUnderTest.RateLimiterFor(rootUri).TimeUnitMilliseconds);
        }

        [TestMethod]
        public void AddOrUpdateDomain_ParamGreaterThanDefault_UsesParam()
        {
            var rootUri = new Uri("http://a.com/");

            var unitUnderTest = new InspectableDomainRateLimiter(5, new ManualTimeProvider());

            unitUnderTest.AddOrUpdateDomain(rootUri, 100);

            Assert.AreEqual(100, unitUnderTest.RateLimiterFor(rootUri).TimeUnitMilliseconds);
        }

        [TestMethod]
        public void AddOrUpdateDomain_AddDuplicateDomain_LastUpdateWins()
        {
            var domainRateLimiter = new InspectableDomainRateLimiter(5, new ManualTimeProvider());
            var domain = new Uri("http://a.com");

            domainRateLimiter.AddOrUpdateDomain(domain, 50);
            domainRateLimiter.AddOrUpdateDomain(domain, 150);//This should override the previous

            Assert.AreEqual(150, domainRateLimiter.RateLimiterFor(domain).TimeUnitMilliseconds);
        }


        [TestMethod]
        public void RemoveDomain_NoLongerRateLimitsThatDomain()
        {
            //Arrange
            var domainRateLimiter = new InspectableDomainRateLimiter(5, new ManualTimeProvider());
            var domain = new Uri("http://a.com");

            domainRateLimiter.AddDomain(domain, 1000);

            //Act
            domainRateLimiter.RemoveDomain(domain);

            //Assert
            Assert.IsNull(domainRateLimiter.RateLimiterFor(domain));

            //Rate limiting the domain again falls back to the default delay (5 ms plus the 20 ms margin), not the removed 1000 ms
            domainRateLimiter.RateLimit(domain);
            Assert.AreEqual(25, domainRateLimiter.RateLimiterFor(domain).TimeUnitMilliseconds);
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

        //Exposes which rate limiter applies to a domain, through the protected lookup derived classes can use
        private class InspectableDomainRateLimiter : DomainRateLimiter
        {
            public InspectableDomainRateLimiter(long minCrawlDelayMillisecs, TimeProvider timeProvider)
                : base(minCrawlDelayMillisecs, timeProvider)
            {
            }

            public RateLimiter RateLimiterFor(Uri uri)
            {
                IRateLimiter rateLimiter;
                return _rateLimiterLookup.TryGetValue(uri.Authority, out rateLimiter) ? (RateLimiter)rateLimiter : null;
            }
        }
    }
}
