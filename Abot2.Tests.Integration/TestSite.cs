using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Abot2.Tests.Integration
{
    /// <summary>
    /// Small in-process website the integration tests crawl, so no external server or proxy is needed.
    /// Mirrors the structure of the original PageGenerator test app:
    ///   /                              links to 4 generator pages and 2 "external" pages
    ///   /PageGenerator/Generate?...    links to 5x200, 1x403, 2x404, 3x500 and 4x503 pages
    ///   /HttpResponse/StatusXXX/PageN  returns status XXX with no links
    /// The "external" pages are served by this same server under the other loopback name (localhost vs 127.0.0.1),
    /// which the crawler treats as a different domain, so all traffic stays on the machine.
    /// </summary>
    internal sealed class TestSite : IAsyncDisposable
    {
        public const int GeneratorPageCount = 4;
        public const int ExternalPageCount = 2;

        // Status code and number of links per generator page, in the same proportions as the old app
        static readonly (int Status, int Count)[] LinksPerGeneratorPage =
        {
            (200, 5), (403, 1), (404, 2), (500, 3), (503, 4)
        };

        const string GeneratorQuery = "Status200Count=5&Status403Count=1&Status404Count=2&Status500Count=3&Status503Count=4";

        readonly WebApplication _app;
        readonly TimeSpan _responseDelay;

        TestSite(WebApplication app, int port, TimeSpan responseDelay)
        {
            _app = app;
            _responseDelay = responseDelay;
            BaseUri = new Uri($"http://127.0.0.1:{port}/");
            ExternalBaseUri = new Uri($"http://localhost:{port}/");
        }

        /// <summary>Root of the site, the uri to crawl</summary>
        public Uri BaseUri { get; }

        /// <summary>Same server under the other loopback name, seen by the crawler as an external domain</summary>
        public Uri ExternalBaseUri { get; }

        /// <param name="responseDelay">Delay added before every response, used by tests that need a crawl to take a while</param>
        /// <param name="cancellationToken">Cancels starting the site</param>
        public static async Task<TestSite> StartAsync(TimeSpan responseDelay = default, CancellationToken cancellationToken = default)
        {
            var port = GetFreeTcpPort();

            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            // Listens on both 127.0.0.1 and ::1 so "localhost" connects immediately whichever address it resolves to
            builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(port));

            var app = builder.Build();
            var site = new TestSite(app, port, responseDelay);
            site.MapRoutes();

            await app.StartAsync(cancellationToken);
            return site;
        }

        /// <summary>Every page a full crawl with external page crawling enabled should find, with its http status</summary>
        public List<PageResult> GetAllPages()
        {
            var pages = new List<PageResult> { new PageResult { Url = BaseUri.AbsoluteUri, HttpStatusCode = 200 } };

            for (var page = 1; page <= GeneratorPageCount; page++)
                pages.Add(new PageResult { Url = new Uri(BaseUri, GeneratorPath(page)).AbsoluteUri, HttpStatusCode = 200 });

            foreach (var (status, count) in LinksPerGeneratorPage)
            {
                for (var page = 1; page <= count * GeneratorPageCount; page++)
                    pages.Add(new PageResult { Url = new Uri(BaseUri, StatusPath(status, page)).AbsoluteUri, HttpStatusCode = status });
            }

            for (var page = 1; page <= ExternalPageCount; page++)
                pages.Add(new PageResult { Url = new Uri(ExternalBaseUri, ExternalPath(page)).AbsoluteUri, HttpStatusCode = 200 });

            return pages;
        }

        public ValueTask DisposeAsync()
        {
            return _app.DisposeAsync();
        }

        void MapRoutes()
        {
            _app.Use(async (context, next) =>
            {
                if (_responseDelay > TimeSpan.Zero)
                    await Task.Delay(_responseDelay, context.RequestAborted);
                await next(context);
            });

            _app.MapGet("/", () =>
            {
                var links = new List<string>();
                for (var page = 1; page <= GeneratorPageCount; page++)
                    links.Add(GeneratorPath(page));
                for (var page = 1; page <= ExternalPageCount; page++)
                    links.Add(new Uri(ExternalBaseUri, ExternalPath(page)).AbsoluteUri);

                return Html("Home", links);
            });

            // Page numbers are assigned per generator page, so links are the same whatever order pages are requested in
            _app.MapGet("/PageGenerator/Generate", (int page) =>
            {
                var links = new List<string>();
                foreach (var (status, count) in LinksPerGeneratorPage)
                {
                    for (var i = 1; i <= count; i++)
                        links.Add(StatusPath(status, (page - 1) * count + i));
                }

                return Html("Generated page " + page, links);
            });

            _app.MapGet("/HttpResponse/Status{status:int}/Page{page:int}", (int status, int page) =>
                Results.Content(HtmlDocument($"Status {status} page {page}", new List<string>()), "text/html", Encoding.UTF8, status));

            _app.MapGet("/External/Page{page:int}", (int page) =>
                Html("External page " + page, new List<string>()));
        }

        static string GeneratorPath(int page) => $"/PageGenerator/Generate?{GeneratorQuery}&Page={page}";

        static string StatusPath(int status, int page) => $"/HttpResponse/Status{status}/Page{page}";

        static string ExternalPath(int page) => $"/External/Page{page}";

        static IResult Html(string title, List<string> links) =>
            Results.Content(HtmlDocument(title, links), "text/html", Encoding.UTF8);

        static string HtmlDocument(string title, List<string> links)
        {
            var html = new StringBuilder();
            html.Append("<!DOCTYPE html><html><head><title>").Append(title).Append("</title></head><body><ul>");
            foreach (var link in links)
                html.Append("<li><a href=\"").Append(WebUtility.HtmlEncode(link)).Append("\">").Append(WebUtility.HtmlEncode(link)).Append("</a></li>");
            html.Append("</ul></body></html>");
            return html.ToString();
        }

        static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
