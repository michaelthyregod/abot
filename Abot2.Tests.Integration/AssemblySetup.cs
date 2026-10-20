using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;

namespace Abot2.Tests.Integration
{
    [TestClass]
    public class SetupAssemblyInitializer
    {
        /// <summary>Test site that responds immediately</summary>
        public static TestSite Site { get; private set; }

        /// <summary>Test site that delays every response, for tests that need a crawl to still be running at a given time</summary>
        public static TestSite SlowSite { get; private set; }

        [AssemblyInitialize]
        public static async Task AssemblyInit(TestContext context)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithThreadId()
                .WriteTo.Console(outputTemplate: Constants.LogFormatTemplate)
                .CreateLogger();

            Site = await TestSite.StartAsync();
            SlowSite = await TestSite.StartAsync(TimeSpan.FromMilliseconds(1500));
        }

        [AssemblyCleanup]
        public static async Task AssemblyCleanup()
        {
            if (Site != null)
                await Site.DisposeAsync();
            if (SlowSite != null)
                await SlowSite.DisposeAsync();
        }
    }
}
