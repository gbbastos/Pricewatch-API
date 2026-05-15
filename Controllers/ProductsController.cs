using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PriceWatch.Models;
using PriceWatch.Services;

namespace PriceWatch.Controllers;

[ApiController]
[Route("products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly MongoDbService _mongo;
    private readonly ScraperService _scraper;

    public ProductsController(MongoDbService mongo, ScraperService scraper)
    {
        _mongo = mongo;
        _scraper = scraper;
    }

    /// <summary>Register a new product to track.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Product), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var product = new Product
        {
            Title = request.Title,
            Url = request.Url,
            TargetPrice = request.TargetPrice,
            CreatedAt = DateTime.UtcNow
        };

        await _mongo.Products.InsertOneAsync(product);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    /// <summary>List all tracked products.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Product>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var products = await _mongo.Products.Find(_ => true).ToListAsync();
        return Ok(products);
    }

    /// <summary>Get a single product by its ID.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var product = await _mongo.Products.Find(p => p.Id == id).FirstOrDefaultAsync();
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Remove a product and its entire price history.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _mongo.Products.DeleteOneAsync(p => p.Id == id);
        if (result.DeletedCount == 0) return NotFound();

        await _mongo.PriceHistories.DeleteManyAsync(h => h.ProductId == id);
        return NoContent();
    }

    /// <summary>
    /// Get the price history for a product.
    /// Use ?alert=true to return only records where the target price was reached.
    /// </summary>
    [HttpGet("{id}/history")]
    [ProducesResponseType(typeof(List<PriceHistory>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(string id, [FromQuery] bool? alert)
    {
        var exists = await _mongo.Products.Find(p => p.Id == id).AnyAsync();
        if (!exists) return NotFound();

        var filter = Builders<PriceHistory>.Filter.Eq(h => h.ProductId, id);

        if (alert.HasValue)
            filter &= Builders<PriceHistory>.Filter.Eq(h => h.AlertTriggered, alert.Value);

        var history = await _mongo.PriceHistories
            .Find(filter)
            .SortByDescending(h => h.FetchedAt)
            .ToListAsync();

        return Ok(history);
    }

    /// <summary>Force an immediate price check for the given product.</summary>
    [HttpPost("{id}/check")]
    [ProducesResponseType(typeof(PriceHistory), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CheckNow(string id)
    {
        var product = await _mongo.Products.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (product is null) return NotFound();

        var price = await _scraper.FetchPriceAsync(product.Url);
        if (price is null)
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Could not fetch the current price. The URL may be invalid or the page structure changed." });

        var record = new PriceHistory
        {
            ProductId = product.Id,
            Price = price.Value,
            FetchedAt = DateTime.UtcNow,
            AlertTriggered = price.Value <= product.TargetPrice
        };

        await _mongo.PriceHistories.InsertOneAsync(record);
        return Ok(record);
    }
}

public record CreateProductRequest(string Title, string Url, double TargetPrice);
