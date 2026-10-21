using System.Threading.Tasks;
using Abot2.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;

namespace Abot2.Tests.Unit.Util
{
    //Asserts upper time limits that other tests running at the same time could push it over
    [TestClass]
    [DoNotParallelize]
    public class RateLimiterTest
    {
        RateLimiter _unitUnderTest;

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Constructor_InvalidOccurrances()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RateLimiter(0, TimeSpan.FromSeconds(10)));
        }

        [TestMethod]
        public void Constructor_NegativeTimeSpan()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RateLimiter(1, TimeSpan.FromSeconds(-10)));
        }

        [TestMethod]
        public void Constructor_TimeSpanToBig()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RateLimiter(1, TimeSpan.FromDays(1000)));
        }

        [TestMethod]
        public void WaitToProceed_ZeroWait_DoesntWait()
        {
            _unitUnderTest = new RateLimiter(1, TimeSpan.FromSeconds(0));

            var timer = Stopwatch.StartNew();

            _unitUnderTest.WaitToProceed();
            _unitUnderTest.WaitToProceed();
            _unitUnderTest.WaitToProceed();

            timer.Stop();

            Assert.IsLessThan(1, timer.Elapsed.TotalSeconds);
        }

        [TestMethod]
        public void WaitToProceed_ValidWaitTime_Waits()
        {
            _unitUnderTest = new RateLimiter(1, TimeSpan.FromMilliseconds(700));

            var timer = Stopwatch.StartNew();

            _unitUnderTest.WaitToProceed();//will not wait for first call
            _unitUnderTest.WaitToProceed();
            _unitUnderTest.WaitToProceed();

            timer.Stop();

            Assert.IsGreaterThan(1, timer.Elapsed.TotalSeconds);
        }

        [TestMethod]
        public void WaitToProceed_ForCoverage()
        {
            _unitUnderTest = new RateLimiter(1, TimeSpan.FromSeconds(1));

            var timer = Stopwatch.StartNew();

            _unitUnderTest.WaitToProceed(TimeSpan.FromSeconds(10));
            _unitUnderTest.Dispose();

            timer.Stop();

            Assert.IsLessThan(1, timer.Elapsed.TotalSeconds);
        }

        [TestMethod]
        public void WaitToProceed_NegativeMilli()
        {
            _unitUnderTest = new RateLimiter(1, TimeSpan.FromSeconds(1));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _unitUnderTest.WaitToProceed(-10));
        }

        [TestMethod]
        public void Dispose_ThenWaitToProceed_ThrowsObjectDisposedException()
        {
            _unitUnderTest = new RateLimiter(1, TimeSpan.FromSeconds(1));

            _unitUnderTest.Dispose();

            Assert.ThrowsExactly<ObjectDisposedException>(() => _unitUnderTest.WaitToProceed());
        }

        [TestMethod]
        public async Task Dispose_CalledTwice_DoesNotThrow()
        {
            _unitUnderTest = new RateLimiter(1, TimeSpan.FromMilliseconds(10));
            _unitUnderTest.WaitToProceed();

            _unitUnderTest.Dispose();
            _unitUnderTest.Dispose();

            //Give a timer callback that was already due the chance to run against the disposed instance
            await Task.Delay(50, TestContext.CancellationToken);
        }
    }
}
