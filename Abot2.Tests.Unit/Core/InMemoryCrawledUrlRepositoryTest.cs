using System;
using Abot2.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Abot2.Tests.Unit.Core
{
    [TestClass]
    public class InMemoryCrawledUrlRepositoryTest : CrawledUrlRepositoryTest
    {
        public override ICrawledUrlRepository GetInstance()
        {
            return new InMemoryCrawledUrlRepository();
        }

        [TestMethod]
        public void Dispose_ThenContains_ThrowsObjectDisposedException()
        {
            var unitUnderTest = GetInstance();

            unitUnderTest.Dispose();

            Assert.ThrowsExactly<ObjectDisposedException>(() => unitUnderTest.Contains(new Uri("http://a.com/")));
        }
    }
}
