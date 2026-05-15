using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PriceWatch.Models;

public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("url")]
    public string Url { get; set; } = string.Empty;

    [BsonElement("targetPrice")]
    public double TargetPrice { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
