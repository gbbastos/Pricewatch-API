using MongoDB.Driver;
using PriceWatch.Models;

namespace PriceWatch.Services;

public class PriceTrackerBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PriceTrackerBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public PriceTrackerBackgroundService(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<PriceTrackerBackgroundService> logger)
    {
        _services = services;
        _logger = logger;

        var hours = configuration.GetValue("Scraper:IntervalHours", 6);
        _interval = TimeSpan.FromHours(hours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PriceTracker started. Interval: {Interval}h", _interval.TotalHours);

        // Short delay so the app is fully up before the first scrape run.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllProductsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Log and continue — a transient DB or network error must not crash the host.
                _logger.LogError(ex, "Unexpected error during scheduled price check. Will retry in {Interval}h", _interval.TotalHours);
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    /// <summary>Scrapes and persists prices for every tracked product. Can be called on-demand.</summary>
    public async Task CheckAllProductsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();
        var mongo = scope.ServiceProvider.GetRequiredService<MongoDbService>();
        var scraper = scope.ServiceProvider.GetRequiredService<ScraperService>();

        var products = await mongo.Products.Find(_ => true).ToListAsync(cancellationToken);
        _logger.LogInformation("Checking prices for {Count} product(s)", products.Count);

        foreach (var product in products)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var price = await scraper.FetchPriceAsync(product.Url);

            if (price is null)
            {
                _logger.LogWarning("Skipping product {Id} ({Title}) — price not found", product.Id, product.Title);
                continue;
            }

            var record = new PriceHistory
            {
                ProductId = product.Id,
                Price = price.Value,
                FetchedAt = DateTime.UtcNow,
                AlertTriggered = price.Value <= product.TargetPrice
            };

            await mongo.PriceHistories.InsertOneAsync(record, cancellationToken: cancellationToken);

            if (record.AlertTriggered)
            {
                _logger.LogInformation(
                    "ALERT: '{Title}' is at R$ {Price} — at or below target R$ {Target}",
                    product.Title, price.Value, product.TargetPrice);
            }
        }
    }
}
