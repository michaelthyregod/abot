using System;
using System.Net.Http;
using System.Threading.Tasks;
using Abot2.Core;
using Abot2.Crawler;
using Abot2.Poco;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Json;

namespace Abot2.Demo
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithThreadId()
                .WriteTo.Console(outputTemplate: Constants.LogFormatTemplate)
                .CreateLogger();

            Log.Information("Demo starting up!");

            //Which demos run, and the uri each one uses, are set in appsettings.json
            var settings = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var ranAny = false;
            if (IsDemoEnabled(settings, "PageRequester"))
            {
                await DemoPageRequester(GetDemoUri(settings, "PageRequester"));
                ranAny = true;
            }
            if (IsDemoEnabled(settings, "SimpleCrawler"))
            {
                await DemoSimpleCrawler(GetDemoUri(settings, "SimpleCrawler"));
                ranAny = true;
            }
            if (!ranAny)
                Log.Warning("No demo is enabled: set Demos:<name>:Enabled to true in appsettings.json");

            Log.Information("Demo done!");
            Console.ReadKey();
        }

        private static bool IsDemoEnabled(IConfiguration settings, string demo)
        {
            var key = $"Demos:{demo}:Enabled";
            var value = settings[key];
            if (!bool.TryParse(value, out var enabled))
                throw new InvalidOperationException($"Set {key} in appsettings.json to true or false; it is [{value}].");

            return enabled;
        }

        private static Uri GetDemoUri(IConfiguration settings, string demo)
        {
            var key = $"Demos:{demo}:Uri";
            var value = settings[key];
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                throw new InvalidOperationException($"Set {key} in appsettings.json to the absolute uri the {demo} demo should use; it is [{value}].");

            return uri;
        }

        private static async Task DemoSimpleCrawler(Uri uri)
        {
            var config = new CrawlConfiguration
            {
                MaxPagesToCrawl = 25,
                MinCrawlDelayPerDomainMilliSeconds = 3000
            };
            using var crawler = new PoliteWebCrawler(config);

            crawler.PageCrawlCompleted += Crawler_PageCrawlCompleted;

            var crawlResult = await crawler.CrawlAsync(uri);
        }

        private static void Crawler_PageCrawlCompleted(object sender, PageCrawlCompletedArgs e)
        {

        }

        private static async Task DemoPageRequester(Uri uri)
        {
            using var contentExtractor = new WebContentExtractor();
            using var pageRequester = new PageRequester(new CrawlConfiguration(), contentExtractor);

            var result = await pageRequester.MakeRequestAsync(uri);
            Log.Information("{result}", new { url = result.Uri, status = Convert.ToInt32(result.HttpResponseMessage.StatusCode) });

        }
    }
}
