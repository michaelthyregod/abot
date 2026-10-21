# Upgrading from Abot 2.x to 3.0

Abot 3.0 modernises the library: it targets .NET 10 alongside .NET Standard 2.0, updates its dependencies to current, patched versions, and makes disposal and concurrency behave the way .NET developers expect. Most applications that only use `PoliteWebCrawler` with its default components upgrade without code changes. Applications that pass their own components into the crawler, or that derive from Abot's classes, need to read the sections below.

3.0 is currently published as a pre-release:

```shell
dotnet add package Abot --version 3.0.0-preview.1
```

## At a glance

| Change | Who is affected | What to do |
|---|---|---|
| [Abot no longer disposes objects you pass in](#1-abot-no-longer-disposes-objects-you-pass-in) | Anyone passing their own components or `HttpClient` to a constructor | Dispose what you create, after the crawler |
| [`Dispose()` is no longer virtual](#2-dispose-is-no-longer-virtual-override-disposebool) | Classes that derive from Abot types and override `Dispose()` | Override `Dispose(bool)` instead |
| [`ObjectDisposedException` after `Dispose`](#3-using-an-object-after-dispose-throws-objectdisposedexception) | Code that uses an Abot object after disposing it | Don't use objects after disposing them |
| [Dependency upgrades](#4-supported-platforms-and-dependencies) | Apps using AngleSharp or Serilog APIs directly, or older Serilog sinks | Check the AngleSharp and Serilog versions you use |
| [`HttpServicePointConnectionLimit` now applies on .NET 10](#51-httpservicepointconnectionlimit-now-works-on-net-10) | Apps on .NET 10+ that relied on unlimited connections per host | Raise the limit or set it to `0` |
| [Other behaviour changes](#5-behaviour-changes) | Mostly subclasses of `WebCrawler` | Review the list |

## 1. Abot no longer disposes objects you pass in

In 2.x, disposing a crawler disposed everything it held, including components you passed into its constructor. That made it impossible to share an object between crawlers, for example one `HttpClient` for the whole application, and it disposed objects the crawler did not own.

In 3.0 the rule is the standard .NET one: **whoever creates an object disposes it.**

- Objects Abot creates itself, because you passed `null` or used a constructor without that parameter, are disposed by Abot.
- Objects you pass in are yours. Abot never disposes them, so you must dispose them, after you dispose the crawler.

These constructor parameters were disposed by Abot in 2.x and must now be disposed by you:

| Constructor | Parameters you now dispose yourself |
|---|---|
| `WebCrawler` / `PoliteWebCrawler` | `threadManager`, `scheduler`, `pageRequester`, `memoryManager` |
| `PageRequester` | `httpClient` |
| `Scheduler` | `crawledUrlRepo`, `pagesToCrawlRepo` |
| `MemoryManager` | `memoryMonitor` |

Abot never disposed the other objects you can pass in, in 2.x either: `PageRequester`'s `contentExtractor`, `TaskThreadManager`'s `cancellationTokenSource`, `CachedMemoryMonitor`'s `memoryMonitor`, and `PoliteWebCrawler`'s `domainRateLimiter` and `robotsDotTextFinder`. Dispose those too if they are disposable.

### Example: default components (no change needed)

If you don't pass any components in, Abot creates and disposes everything itself:

```csharp
using var crawler = new PoliteWebCrawler(new CrawlConfiguration { MaxPagesToCrawl = 10 });
crawler.PageCrawlCompleted += (sender, e) => Console.WriteLine(e.CrawledPage.Uri);

var result = await crawler.CrawlAsync(new Uri("https://example.com/"));
```

### Example: custom components

**2.x**: disposing the crawler also disposed the scheduler and page requester:

```csharp
var config = new CrawlConfiguration();
var crawler = new PoliteWebCrawler(config, null, null,
    new Scheduler(), new PageRequester(config, new WebContentExtractor()), null, null, null, null);

await crawler.CrawlAsync(new Uri("https://example.com/"));
crawler.Dispose();   // also disposed the Scheduler and PageRequester
```

**3.0**: you created them, so you dispose them. With `using var` they are disposed in reverse order of declaration, so the crawler goes first:

```csharp
var config = new CrawlConfiguration();

using var contentExtractor = new WebContentExtractor();
using var pageRequester = new PageRequester(config, contentExtractor);
using var scheduler = new Scheduler();
using var crawler = new PoliteWebCrawler(config, null, null, scheduler, pageRequester, null, null, null, null);

await crawler.CrawlAsync(new Uri("https://example.com/"));
// crawler, scheduler, pageRequester and contentExtractor are disposed here, in that order
```

### Example: sharing one `HttpClient` across crawls

Because Abot no longer disposes an `HttpClient` you pass in, you can now reuse one for many crawls, which is how `HttpClient` is meant to be used:

```csharp
// Created once, for example as a singleton, and disposed when the application shuts down
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MyCrawler/1.0");

async Task<CrawlResult> CrawlAsync(Uri site)
{
    var config = new CrawlConfiguration { MaxPagesToCrawl = 100 };

    using var contentExtractor = new WebContentExtractor();
    using var pageRequester = new PageRequester(config, contentExtractor, httpClient);
    using var crawler = new PoliteWebCrawler(config, null, null, null, pageRequester, null, null, null, null);

    return await crawler.CrawlAsync(site);   // httpClient is still usable afterwards
}
```

When you pass in an `HttpClient`, `PageRequester` uses it as it is. The HTTP settings in `CrawlConfiguration` (user agent, timeout, redirects, cookies, decompression, certificate validation, login and connection limit) are only applied to the client Abot builds itself, so configure your own client accordingly.

### Example: thread manager with your own cancellation token source

```csharp
using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
using var threadManager = new TaskThreadManager(10, cancellationTokenSource);
using var crawler = new PoliteWebCrawler(new CrawlConfiguration(), null, threadManager, null, null, null, null, null, null);

var result = await crawler.CrawlAsync(new Uri("https://example.com/"));
// The thread manager cancels the token source on AbortAll()/Dispose(), but, as in 2.x, does not dispose it
```

As in 2.x, a thread manager can only be used for one crawl: when a crawl ends, the crawler calls `AbortAll()` on a thread manager you passed in (and disposes one it created itself). Create a new thread manager, or let the crawler create one, for each crawl.

### Example: memory monitoring

`MemoryManager` no longer disposes the monitor you give it. A `CachedMemoryMonitor` runs a timer, so it must be disposed:

```csharp
var config = new CrawlConfiguration { MaxMemoryUsageInMb = 1024 };

using var gcMonitor = new GcMemoryMonitor();
using var cachedMonitor = new CachedMemoryMonitor(gcMonitor, cacheExpirationInSeconds: 5);
using var memoryManager = new MemoryManager(cachedMonitor);
using var crawler = new PoliteWebCrawler(config, null, null, null, null, null, memoryManager, null, null);
```

If you don't need a custom monitor, leave `memoryManager` as `null` and Abot creates and disposes one for you whenever a memory limit is configured.

## 2. `Dispose()` is no longer virtual: override `Dispose(bool)`

Every disposable class in Abot now follows the standard .NET [dispose pattern](https://learn.microsoft.com/dotnet/standard/garbage-collection/implementing-dispose): a public, non-virtual `Dispose()` and a `protected virtual void Dispose(bool disposing)` for derived classes. `Dispose()` can be called more than once, and from several threads at the same time, without failing.

This affects `WebCrawler`, `PoliteWebCrawler`, `ThreadManager`, `TaskThreadManager`, `ManualThreadManager`, `PageRequester`, `Scheduler`, `WebContentExtractor`, `MemoryManager`, `GcMemoryMonitor`, `CachedMemoryMonitor`, `RateLimiter`, `DomainRateLimiter` and the `InMemory`, `Compact` and `BloomFilter` crawled-URL repositories and `FifoPagesToCrawlRepository`.

If your class overrides `Dispose()`, it no longer compiles (`CS0506: cannot override inherited member because it is not marked virtual`).

**2.x**:

```csharp
public class MyCrawler : PoliteWebCrawler
{
    private readonly MyResultStore _store = new MyResultStore();

    public MyCrawler(CrawlConfiguration config) : base(config) { }

    public override void Dispose()
    {
        _store.Dispose();
        base.Dispose();
    }
}
```

**3.0**: override `Dispose(bool)`, release your own resources only when `disposing` is `true`, guard against being called twice, and always call the base class:

```csharp
public class MyCrawler : PoliteWebCrawler
{
    private readonly MyResultStore _store = new MyResultStore();
    private bool _disposed;

    public MyCrawler(CrawlConfiguration config) : base(config) { }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;

        // The base class stops the crawl threads first, so call it before
        // disposing anything those threads might still be using
        base.Dispose(disposing);

        if (disposing)
            _store.Dispose();
    }
}
```

The same applies to custom thread managers:

```csharp
public class MyThreadManager : ThreadManager
{
    private readonly SemaphoreSlim _gate = new SemaphoreSlim(1);
    private bool _disposed;

    public MyThreadManager(int maxThreads) : base(maxThreads) { }

    protected override void RunActionOnDedicatedThread(Action action)
    {
        new Thread(() => RunAction(action)).Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;

        base.Dispose(disposing);   // aborts running work and releases the base class's resources

        if (disposing)
            _gate.Dispose();
    }
}
```

Classes that implement Abot's **interfaces** directly (`IPageRequester`, `IScheduler`, `IThreadManager` and so on) are not affected: the interfaces still just extend `IDisposable`.

### `ThreadManager._isDisplosed` is now `_isDisposed`

The misspelled protected field in `ThreadManager` is renamed to `_isDisposed`, and it is now `volatile`. Rename any references in derived classes.

## 3. Using an object after `Dispose` throws `ObjectDisposedException`

In 2.x, using an Abot object after disposing it usually failed with a `NullReferenceException`, or appeared to work while using released resources. In 3.0 it throws `ObjectDisposedException`, as .NET types do:

| Type | Members that throw after `Dispose` |
|---|---|
| `WebCrawler`, `PoliteWebCrawler` | `CrawlAsync` |
| `PageRequester` | `MakeRequestAsync` |
| `Scheduler` | `Count`, `Add`, `GetNext`, `Clear`, `AddKnownUri`, `IsUriKnown` |
| `ThreadManager` and derived classes | `DoWork` (was `InvalidOperationException`; after `AbortAll()` it is still `InvalidOperationException`) |
| `DomainRateLimiter` | `RateLimit`, `AddDomain`, `AddOrUpdateDomain` |
| `RateLimiter` | `WaitToProceed` (unchanged, but the message is now the type name) |
| `FifoPagesToCrawlRepository` | `Add`, `GetNext`, `Clear`, `Count` |
| `InMemoryCrawledUrlRepository`, `CompactCrawledUrlRepository` | `Contains`, `AddIfNew` |

If you catch `InvalidOperationException` from `ThreadManager.DoWork` to detect a disposed thread manager, catch `ObjectDisposedException` instead.

## 4. Supported platforms and dependencies

### Target frameworks

The package now contains two builds:

| Your application runs on | Build used |
|---|---|
| .NET 10 or later | `net10.0` |
| .NET Framework 4.6.2 or later, .NET Core 2.0 to .NET 9, and other .NET Standard 2.0 platforms | `netstandard2.0` |

The minimum .NET Framework version is now **4.6.2**, because that is the oldest version Abot's updated dependencies support.

On .NET 10 the package no longer pulls in `Microsoft.CSharp`, `System.Net.Http` or `System.Security.Cryptography.X509Certificates`, because they are part of .NET. On the `netstandard2.0` build, `System.Net.Http` (4.3.4) and `System.Security.Cryptography.X509Certificates` (4.3.2) are now explicit dependencies, which replaces the vulnerable 4.1.0 versions that NRobotsCore pulled in before (GHSA-7jgj-8wvc-jh57 and GHSA-7mfr-774f-w5r9).

### Dependency upgrades

| Package | 2.x | 3.0 |
|---|---|---|
| AngleSharp | 0.14.0 | 1.8.5 |
| Serilog | 2.10.0 | 4.4.0 |
| Serilog.Sinks.Console | 3.1.1 | 6.1.1 |
| Serilog.Enrichers.Thread | 3.1.0 | 4.0.0 |

**AngleSharp**: 0.14.0 has a known vulnerability (GHSA-pgww-w46g-26qg). If your code works with `CrawledPage.AngleSharpHtmlDocument` beyond basic querying, check it against the [AngleSharp changelog](https://github.com/AngleSharp/AngleSharp/blob/devel/CHANGELOG.md) for the 1.x releases. If your application references AngleSharp itself, it must now reference 1.8.5 or later.

**Serilog**: Abot still logs through Serilog's static `Log.Logger`, so you configure logging exactly as before:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: Constants.LogFormatTemplate)
    .CreateLogger();
```

Your application now gets Serilog 4.4.0 or later. If you use other Serilog sinks or enrichers, update them to versions that support Serilog 4.

## 5. Behaviour changes

### 5.1 `HttpServicePointConnectionLimit` now works on .NET 10

`CrawlConfiguration.HttpServicePointConnectionLimit` (default 200) limits the number of concurrent connections per host. In 2.x it set `ServicePointManager.DefaultConnectionLimit`, which has no effect on `HttpClient` on .NET Core and later, so on modern .NET the limit was silently ignored.

On the `net10.0` build it now sets `HttpClientHandler.MaxConnectionsPerServer`, so a crawl on .NET 10 or later really is limited to 200 connections per host by default. On the `netstandard2.0` build nothing changes: the limit works on .NET Framework, and is still ignored on .NET Core 2.0 to .NET 9.

To keep the 2.x behaviour on .NET 10 (no limit set by Abot), set it to `0`:

```csharp
var config = new CrawlConfiguration
{
    HttpServicePointConnectionLimit = 0   // 0 = Abot does not set a limit
};
```

### 5.2 `MaxPagesToCrawl` is now exact

In 2.x, multi-threaded crawls could crawl a few pages more than `MaxPagesToCrawl` (for example 25 to 28 pages with a limit of 25). 3.0 never exceeds the limit. If you compensated for the overshoot, remove the workaround.

### 5.3 Errors during a crawl no longer crash the process

In 2.x, an exception thrown while getting or processing a page could escape the crawler's worker threads and terminate the whole process. This included cancelling a crawl at the wrong moment, a custom scheduler whose `GetNext()` throws, and a `ProcessPage` or `AddPageToContext` override that throws. In 3.0 these are handled the same way the crawler handles its own errors:

- The exception is stored in `CrawlResult.ErrorException`, and `CrawlResult.ErrorOccurred` is `true`.
- The error is logged.
- The crawl is hard-stopped.

Cancellation is still reported as before: `ErrorOccurred` is `true` and `ErrorException` is an `OperationCanceledException`.

```csharp
var result = await crawler.CrawlAsync(new Uri("https://example.com/"));

if (result.ErrorOccurred)
    Console.WriteLine($"Crawl stopped early: {result.ErrorException.Message}");
```

### 5.4 `AddPageToContext` is called before `ProcessPage`

`WebCrawler` now calls `AddPageToContext` while it dequeues the next page, under the same lock that guards `MaxPagesToCrawl`, and then calls `ProcessPage`. In 2.x, `ProcessPage` called `AddPageToContext` itself. If you override `ProcessPage` without calling the base implementation, `AddPageToContext` is now still called for every page; don't call it again from your override.

### 5.5 Very deeply nested HTML is flattened in `AngleSharpHtmlDocument`

AngleSharp 1.x processes the document tree recursively, so a malformed or hostile page with thousands of unclosed tags could cause a `StackOverflowException`, which .NET cannot catch and which kills the process. `CrawledPage.AngleSharpHtmlDocument` now caps nesting at `CrawledPage.MaxHtmlDocumentDepth` (512 levels, the same limit Chrome uses). Deeper elements are moved up into siblings in document order, so no text, elements or links are lost. Pages within the limit are not changed. `CrawledPage.Content.Text` always contains the original HTML.

### 5.6 `DomainRateLimiter` is disposable and shares limits correctly

`DomainRateLimiter` now implements `IDisposable` and releases the timer and semaphore of every per-domain limiter it created. If you create one yourself, dispose it. When two threads added the same domain at the same time, 2.x could give one of them a separate limiter, so that domain was not rate limited correctly. 3.0 always uses one limiter per domain.

### 5.7 Fixes you get automatically

These need no action, but may change what you observe:

- `PageRequester` creates exactly one `HttpClient`, even when the first requests run at the same time. 2.x could create several and leak all but one.
- A link found on several pages that are crawled at the same time is evaluated once. In 2.x, `ShouldCrawlPage` (and your `ShouldScheduleLinkDecisionMaker`) could be called for it more than once, and `PageCrawlDisallowed` could fire more than once for the same link.
- `PoliteWebCrawler` disposes the rate limiter and the robots.txt `PageRequester` it creates.
- `WebCrawler` disposes the crawl timeout timer, and `TaskThreadManager` disposes the cancellation token source it creates.
- A `RateLimiter` timer callback that runs after `Dispose` no longer crashes the process.
- `RateLimiter` times waits with `Stopwatch`. On .NET Framework, 2.x used `Environment.TickCount`, which only advances every ~15 ms, so waits could end up to ~15 ms early.
- `CompactCrawledUrlRepository`, the default crawled-URL store, now identifies URLs by a 64-bit hash that is the same in every process. 2.x used `Uri.GetHashCode()`: only 32 bits, so different URLs could share a hash (about 100 pairs per million URLs) and the second page was skipped as already crawled. On .NET Core and later that hash is also seeded randomly per process, so which page was skipped changed from run to run. Which URLs count as duplicates is unchanged.
- `BloomFilter<string>` (used by `BloomFilterCrawledUrlRepository`) no longer uses `string.GetHashCode()`, so its results are the same on every run.

## 6. Package changes

- The package includes XML documentation (`Abot2.xml`), so IntelliSense shows descriptions of Abot's types and members.
- The package icon is embedded in the package. The old icon URL no longer worked.

## Upgrade checklist

1. Update the package reference to 3.0 and build.
2. Fix any `CS0506` errors by changing `Dispose()` overrides to `Dispose(bool)` overrides ([section 2](#2-dispose-is-no-longer-virtual-override-disposebool)).
3. Rename references to `ThreadManager._isDisplosed` to `_isDisposed`.
4. For every object you pass into an Abot constructor, make sure you dispose it after the crawler ([section 1](#1-abot-no-longer-disposes-objects-you-pass-in)).
5. If you pass in your own `HttpClient`, configure its timeout, user agent and handler settings yourself.
6. On .NET 10 or later, decide whether the default limit of 200 connections per host suits you ([section 5.1](#51-httpservicepointconnectionlimit-now-works-on-net-10)).
7. Update Serilog sinks and enrichers to versions that support Serilog 4, and check direct use of AngleSharp APIs against AngleSharp 1.x.
8. Replace `catch (InvalidOperationException)` around `ThreadManager.DoWork` with `catch (ObjectDisposedException)` if you used it to detect a disposed thread manager.
