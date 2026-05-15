using MongoDB.Driver;
using PriceWatch.Models;

namespace PriceWatch.Services;

public class MongoDbService
{
    private readonly IMongoDatabase _database;

    public MongoDbService(IConfiguration configuration)
    {
        var connectionString = configuration["MongoDB:ConnectionString"]
            ?? throw new InvalidOperationException("MongoDB:ConnectionString is not configured.");

        var databaseName = configuration["MongoDB:DatabaseName"]
            ?? throw new InvalidOperationException("MongoDB:DatabaseName is not configured.");

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<Product> Products =>
        _database.GetCollection<Product>("products");

    public IMongoCollection<PriceHistory> PriceHistories =>
        _database.GetCollection<PriceHistory>("price_histories");
}
