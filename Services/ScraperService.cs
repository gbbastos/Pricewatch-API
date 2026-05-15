using Microsoft.Playwright;
using System.Globalization;

namespace PriceWatch.Services;

public class ScraperService
{
    private readonly ILogger<ScraperService> _logger;

    // Ordered from most specific to most generic.
    // Covers: detail pages (/MLB...), catalog/comparison pages (/p/MLB... and /up/MLB...).
    private static readonly string[] PriceSelectors =
    [
        "meta[itemprop='price']",                                    // structured data — most reliable
        ".ui-pdp-price__part .andes-money-amount__fraction",         // PDP (product detail page)
        ".poly-price__current .andes-money-amount__fraction",        // catalog /p/ pages
        ".andes-money-amount--cents-superscript .andes-money-amount__fraction",
        ".andes-money-amount__fraction",                             // generic fallback
    ];

    // Injected before page load to hide Playwright's automation fingerprint.
    private const string StealthScript = """
        Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
        Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3] });
        Object.defineProperty(navigator, 'languages', { get: () => ['pt-BR', 'pt', 'en-US'] });
        window.chrome = { runtime: {} };
        """;

    public ScraperService(ILogger<ScraperService> logger)
    {
        _logger = logger;
    }

    public async Task<double?> FetchPriceAsync(string url)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args =
            [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-blink-features=AutomationControlled",
                "--disable-dev-shm-usage",
                "--disable-infobars",
                "--window-size=1920,1080"
            ]
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                        "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            Locale = "pt-BR",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Accept-Language"] = "pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7",
                ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
            }
        });

        // Hide automation before any script runs on the page.
        await context.AddInitScriptAsync(StealthScript);

        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 45_000
            });

            // Brief human-like pause after load.
            await Task.Delay(Random.Shared.Next(800, 1500));

            foreach (var selector in PriceSelectors)
            {
                var raw = selector.StartsWith("meta")
                    ? await TryReadAttributeAsync(page, selector, "content")
                    : await TryReadTextAsync(page, selector);

                if (string.IsNullOrWhiteSpace(raw)) continue;

                var price = ParseBrazilianPrice(raw);
                if (price is null) continue;

                _logger.LogInformation("Price scraped ({Selector}) from {Url}: R$ {Price}", selector, url, price);
                return price;
            }

            var snippet = await page.EvaluateAsync<string>("document.body.innerText.substring(0, 300)");
            _logger.LogWarning("No price selector matched on {Url}. Page snippet: {Snippet}", url, snippet);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape {Url}", url);
            return null;
        }
    }

    private static double? ParseBrazilianPrice(string raw)
    {
        // ML prices: "1.299" (integer part, dot as thousands sep) or "222.30" (already decimal)
        var cleaned = raw.Trim();

        // If it's already a valid decimal (e.g. from meta tag "222.30"), parse directly.
        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var direct) && direct > 0)
            return direct;

        // Brazilian format: "1.299" or "1.299,99"
        cleaned = cleaned.Replace(".", "").Replace(",", ".");
        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) && price > 0
            ? price
            : null;
    }

    private static async Task<string?> TryReadTextAsync(IPage page, string selector)
    {
        try
        {
            var el = await page.QuerySelectorAsync(selector);
            return el is null ? null : await el.InnerTextAsync();
        }
        catch { return null; }
    }

    private static async Task<string?> TryReadAttributeAsync(IPage page, string selector, string attribute)
    {
        try
        {
            var el = await page.QuerySelectorAsync(selector);
            return el is null ? null : await el.GetAttributeAsync(attribute);
        }
        catch { return null; }
    }
}
