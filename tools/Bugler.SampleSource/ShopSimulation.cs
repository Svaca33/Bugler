using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Bugler.SampleSource;

internal enum Outcome
{
    Ok,
    Warning,
    Error,
}

internal sealed record OperationResult(string Name, Outcome Outcome, string Detail, string TraceId);

/// <summary>
/// Simulates a small e-shop: HTTP requests with nested db/cache/payment spans and
/// logs emitted inside the active span so Bugler receives them trace-correlated.
/// Latencies are real (Task.Delay) so span durations look plausible in the waterfall.
/// </summary>
internal sealed class ShopSimulation(
    ILoggerFactory loggerFactory, int declineRate, TimeSpan? rareErrorInterval)
{
    public const string ActivitySourceName = "Sample.Eshop";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    private static readonly string[] Products =
        ["mechanical keyboard", "usb-c dock", "27\" monitor", "4k webcam", "desk mat", "trackball", "headset", "laptop stand"];

    private static readonly string[] Customers = ["alice", "bob", "carol", "dave", "erin", "frank"];

    private static readonly string[] SearchTerms = ["keyboard", "usb hub", "monitor 27", "webcam", "ergonomic", "stand"];

    private static readonly string[] DeclineReasons = ["insufficient funds", "card expired", "suspected fraud"];

    private readonly ILogger catalogLog = loggerFactory.CreateLogger("Eshop.Catalog");
    private readonly ILogger checkoutLog = loggerFactory.CreateLogger("Eshop.Checkout");
    private readonly ILogger jobsLog = loggerFactory.CreateLogger("Eshop.Jobs");
    private readonly Random random = new();
    private int nextOrderId = Random.Shared.Next(1000, 5000);

    // The first sync failure comes one full interval after start, never sooner.
    private DateTime nextRareErrorAt = DateTime.UtcNow + (rareErrorInterval ?? TimeSpan.Zero);

    public async Task<OperationResult> RunOperationAsync(CancellationToken ct)
    {
        // A different, rarer kind of trouble than the payment declines: spaced at least the
        // configured interval apart, so an episode it opens can outlive a quiet window.
        if (rareErrorInterval is { } interval && DateTime.UtcNow >= nextRareErrorAt)
        {
            nextRareErrorAt = DateTime.UtcNow + interval + TimeSpan.FromSeconds(random.Next(0, 90));
            return await SyncInventoryAsync(ct);
        }

        var roll = random.Next(100);
        return roll switch
        {
            < 30 => await BrowseProductsAsync(ct),
            < 55 => await ViewProductAsync(ct),
            < 70 => await SearchAsync(ct),
            < 94 => await CheckoutAsync(ct),
            _ => await PurgeCartsAsync(ct),
        };
    }

    private async Task<OperationResult> BrowseProductsAsync(CancellationToken ct)
    {
        using var request = StartRequest("GET /products", "GET", "/products");
        var page = random.Next(1, 9);
        await QueryAsync("SELECT products page", "SELECT * FROM products ORDER BY name LIMIT 24 OFFSET @offset", 5, 25, ct);
        var count = random.Next(12, 25);
        catalogLog.LogInformation("Listed {ProductCount} products on page {Page}", count, page);
        request?.SetTag("http.response.status_code", 200);
        return new("GET /products", Outcome.Ok, $"page {page}", TraceIdOf(request));
    }

    private async Task<OperationResult> ViewProductAsync(CancellationToken ct)
    {
        var product = Pick(Products);
        var productId = random.Next(1, 500);
        using var request = StartRequest("GET /products/{id}", "GET", "/products/{id}");
        request?.SetTag("app.product.id", productId);

        var cacheHit = random.Next(100) < 60;
        using (var cache = Source.StartActivity("cache.get product"))
        {
            cache?.SetTag("cache.hit", cacheHit);
            await Task.Delay(random.Next(1, 4), ct);
        }

        if (!cacheHit)
        {
            await QueryAsync("SELECT product", "SELECT * FROM products WHERE id = @id", 4, 18, ct);
        }

        if (random.Next(100) < 6)
        {
            catalogLog.LogWarning("Product {ProductId} not found", productId);
            request?.SetTag("http.response.status_code", 404);
            return new("GET /products/{id}", Outcome.Warning, $"#{productId} not found", TraceIdOf(request));
        }

        catalogLog.LogInformation("Served product {ProductId} ({ProductName}), cache hit: {CacheHit}", productId, product, cacheHit);
        request?.SetTag("http.response.status_code", 200);
        return new("GET /products/{id}", Outcome.Ok, $"#{productId} {product}", TraceIdOf(request));
    }

    private async Task<OperationResult> SearchAsync(CancellationToken ct)
    {
        var term = Pick(SearchTerms);
        using var request = StartRequest("GET /search", "GET", "/search");
        request?.SetTag("app.search.term", term);

        var slow = random.Next(100) < 8;
        var elapsed = slow ? random.Next(300, 900) : random.Next(10, 60);
        await QueryAsync("SELECT products search", "SELECT * FROM products WHERE name ILIKE @pattern", elapsed, elapsed + 1, ct);
        var hits = random.Next(0, 30);

        if (slow)
        {
            catalogLog.LogWarning("Search for {SearchTerm} took {ElapsedMs} ms", term, elapsed);
        }
        else
        {
            catalogLog.LogInformation("Search for {SearchTerm} returned {HitCount} hits", term, hits);
        }

        request?.SetTag("http.response.status_code", 200);
        return new("GET /search", slow ? Outcome.Warning : Outcome.Ok, $"'{term}', {hits} hits", TraceIdOf(request));
    }

    private async Task<OperationResult> CheckoutAsync(CancellationToken ct)
    {
        var orderId = nextOrderId++;
        var customer = Pick(Customers);
        var itemCount = random.Next(1, 5);
        using var request = StartRequest("POST /checkout", "POST", "/checkout");
        request?.SetTag("app.order.id", orderId);

        using (Source.StartActivity("reserve-inventory"))
        {
            await QueryAsync("UPDATE inventory", "UPDATE inventory SET reserved = reserved + @qty WHERE product_id = @id", 4, 15, ct);
            if (random.Next(100) < 5)
            {
                checkoutLog.LogWarning("Order {OrderId} rejected: item out of stock", orderId);
                request?.SetTag("http.response.status_code", 409);
                return new("POST /checkout", Outcome.Warning, $"order {orderId} out of stock", TraceIdOf(request));
            }
        }

        using (var charge = Source.StartActivity("charge-card", ActivityKind.Client))
        {
            charge?.SetTag("peer.service", "payment-gateway");
            charge?.SetTag("http.request.method", "POST");
            charge?.SetTag("url.full", "https://payments.example.com/charge");

            var slow = random.Next(100) < 10;
            var elapsed = slow ? random.Next(1100, 2100) : random.Next(40, 160);
            await Task.Delay(elapsed, ct);
            if (slow)
            {
                checkoutLog.LogWarning("Payment gateway responded in {ElapsedMs} ms for order {OrderId}", elapsed, orderId);
            }

            if (random.Next(100) < declineRate)
            {
                var reason = Pick(DeclineReasons);
                charge?.SetStatus(ActivityStatusCode.Error, "card declined");
                charge?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                {
                    ["exception.type"] = "PaymentDeclinedException",
                    ["exception.message"] = reason,
                }));
                checkoutLog.LogError("Payment declined for order {OrderId}: {DeclineReason}", orderId, reason);
                request?.SetStatus(ActivityStatusCode.Error, "payment declined");
                request?.SetTag("http.response.status_code", 402);
                return new("POST /checkout", Outcome.Error, $"order {orderId} declined: {reason}", TraceIdOf(request));
            }
        }

        await QueryAsync("INSERT order", "INSERT INTO orders (customer, items) VALUES (@customer, @items)", 5, 20, ct);
        checkoutLog.LogInformation("Order {OrderId} placed by {Customer} with {ItemCount} items", orderId, customer, itemCount);
        request?.SetTag("http.response.status_code", 201);
        return new("POST /checkout", Outcome.Ok, $"order {orderId} by {customer}", TraceIdOf(request));
    }

    private async Task<OperationResult> PurgeCartsAsync(CancellationToken ct)
    {
        using var job = Source.StartActivity("purge-abandoned-carts");
        await QueryAsync("DELETE carts", "DELETE FROM carts WHERE updated_at < now() - interval '2 days'", 10, 40, ct);
        var purged = random.Next(0, 14);
        jobsLog.LogInformation("Purged {CartCount} abandoned carts", purged);
        return new("purge-abandoned-carts", Outcome.Ok, $"{purged} carts", TraceIdOf(job));
    }

    private async Task<OperationResult> SyncInventoryAsync(CancellationToken ct)
    {
        using var job = Source.StartActivity("sync-inventory");
        await QueryAsync("SELECT stock deltas", "SELECT * FROM stock_deltas WHERE synced_at IS NULL", 8, 30, ct);

        var elapsed = random.Next(1500, 2600);
        using (var call = Source.StartActivity("warehouse.fetch", ActivityKind.Client))
        {
            call?.SetTag("peer.service", "warehouse-api");
            call?.SetTag("http.request.method", "GET");
            call?.SetTag("url.full", "https://warehouse.example.com/stock");
            await Task.Delay(elapsed, ct);
            call?.SetStatus(ActivityStatusCode.Error, "timeout");
            call?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                ["exception.type"] = "TaskCanceledException",
                ["exception.message"] = $"The request was canceled due to the configured timeout of {elapsed} ms.",
            }));
        }

        jobsLog.LogError("Inventory sync failed: warehouse API timed out after {TimeoutMs} ms", elapsed);
        job?.SetStatus(ActivityStatusCode.Error, "warehouse timeout");
        return new("sync-inventory", Outcome.Error, $"warehouse timeout {elapsed} ms", TraceIdOf(job));
    }

    private static Activity? StartRequest(string name, string method, string route)
    {
        var request = Source.StartActivity(name, ActivityKind.Server);
        request?.SetTag("http.request.method", method);
        request?.SetTag("http.route", route);
        return request;
    }

    private async Task QueryAsync(string spanName, string query, int minMs, int maxMs, CancellationToken ct)
    {
        using var span = Source.StartActivity(spanName, ActivityKind.Client);
        span?.SetTag("db.system.name", "postgresql");
        span?.SetTag("db.query.text", query);
        await Task.Delay(random.Next(minMs, maxMs), ct);
    }

    private string Pick(string[] values) => values[random.Next(values.Length)];

    private static string TraceIdOf(Activity? activity) => activity?.TraceId.ToString() ?? "none";
}
