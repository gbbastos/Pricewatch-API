using PriceWatch.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PriceWatch API",
        Version = "v1",
        Description = "Track product prices from Mercado Livre. Get notified when prices drop to your target."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// MongoDB and scraper are singletons; background service reuses the same instance.
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<ScraperService>();
builder.Services.AddSingleton<PriceTrackerBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PriceTrackerBackgroundService>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PriceWatch API v1"));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
