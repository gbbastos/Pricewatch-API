using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PriceWatch.Models;

public class PriceHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("productId")]
    public string ProductId { get; set; } = string.Empty;

    [BsonElement("price")]
    public double Price { get; set; }

    [BsonElement("fetchedAt")]
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>True when the scraped price was at or below the product's target price.</summary>
    [BsonElement("alertTriggered")]
    public bool AlertTriggered { get; set; }
}
