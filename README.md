# PriceWatch API

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![MongoDB](https://img.shields.io/badge/MongoDB-47A248?style=for-the-badge&logo=mongodb&logoColor=white)
![Playwright](https://img.shields.io/badge/Playwright-45ba4b?style=for-the-badge&logo=playwright&logoColor=white)

A RESTful price-tracking API built with **C# / ASP.NET Core 8**, **MongoDB**, and **Playwright**.  
Register any Mercado Livre product URL, set a target price, and the service monitors it automatically every 6 hours. Every price check is stored in the history — and flagged whenever the price drops to your target.

---

## Features

- **Automatic scraping** — Playwright-powered headless browser runs in the background on a configurable interval
- **Alert system** — every `PriceHistory` record is marked `alertTriggered: true` when the price reaches or drops below the target
- **Manual check** — force an on-demand price fetch for any product via `POST /products/{id}/check`
- **Filtered history** — query only alert records with `GET /products/{id}/history?alert=true`
- **Swagger UI** — interactive docs available at `/swagger` in development

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 / ASP.NET Core Web API |
| Database | MongoDB (MongoDB.Driver) |
| Scraping | Microsoft Playwright for .NET |
| Background jobs | `IHostedService` / `BackgroundService` |
| Docs | Swashbuckle (Swagger / OpenAPI) |

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [MongoDB Atlas](https://www.mongodb.com/atlas) free cluster (or local MongoDB)
- Node.js ≥ 18 (required by the Playwright browser installer)

### 1 — Clone

```bash
git clone https://github.com/<your-username>/pricewatch-api.git
cd pricewatch-api
```

### 2 — Configure

```bash
cp appsettings.example.json appsettings.json
```

Edit `appsettings.json` and fill in your MongoDB connection string:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb+srv://<user>:<password>@cluster.mongodb.net/?retryWrites=true&w=majority",
    "DatabaseName": "pricewatch"
  },
  "Scraper": {
    "IntervalHours": 6
  }
}
```

> `appsettings.json` is gitignored — your credentials never leave your machine.

### 3 — Install Playwright browsers

```bash
dotnet build
pwsh bin/Debug/net8.0/playwright.ps1 install chromium
# or on Linux/macOS:
# ./bin/Debug/net8.0/playwright.sh install chromium
```

### 4 — Run

```bash
dotnet run
```

The API starts at `https://localhost:5001`. Open `https://localhost:5001/swagger` for the interactive docs.

---

## API Reference

### Products

#### `POST /products` — Register a product

**Request body**
```json
{
  "title": "PlayStation 5",
  "url": "https://www.mercadolivre.com.br/...",
  "targetPrice": 3500.00
}
```

**Response `201 Created`**
```json
{
  "id": "664f1a2b3c4d5e6f7a8b9c0d",
  "title": "PlayStation 5",
  "url": "https://www.mercadolivre.com.br/...",
  "targetPrice": 3500.00,
  "createdAt": "2024-06-01T12:00:00Z"
}
```

---

#### `GET /products` — List all products

**Response `200 OK`**
```json
[
  {
    "id": "664f1a2b3c4d5e6f7a8b9c0d",
    "title": "PlayStation 5",
    "url": "https://www.mercadolivre.com.br/...",
    "targetPrice": 3500.00,
    "createdAt": "2024-06-01T12:00:00Z"
  }
]
```

---

#### `GET /products/{id}` — Get product by ID

**Response `200 OK`** — same shape as above.  
**Response `404 Not Found`** — product does not exist.

---

#### `DELETE /products/{id}` — Remove product and its history

**Response `204 No Content`**  
**Response `404 Not Found`**

---

#### `GET /products/{id}/history` — Full price history

**Query params**

| Param | Type | Description |
|---|---|---|
| `alert` | `bool` | If `true`, returns only records where the target price was reached |

**Response `200 OK`**
```json
[
  {
    "id": "664f1b3c4d5e6f7a8b9c0e1f",
    "productId": "664f1a2b3c4d5e6f7a8b9c0d",
    "price": 3299.99,
    "fetchedAt": "2024-06-01T18:00:00Z",
    "alertTriggered": true
  }
]
```

**Filter example — only alerts:**
```
GET /products/664f1a2b.../history?alert=true
```

---

#### `POST /products/{id}/check` — Force immediate price check

Scrapes the current price right now and saves it to the history.

**Response `200 OK`** — the `PriceHistory` record just created.  
**Response `502 Bad Gateway`** — page was unreachable or price element not found.

```json
{
  "id": "664f1c4d5e6f7a8b9c0f1e2a",
  "productId": "664f1a2b3c4d5e6f7a8b9c0d",
  "price": 3299.99,
  "fetchedAt": "2024-06-01T20:30:00Z",
  "alertTriggered": true
}
```

---

## Project Structure

```
pricewatch-api/
├── Controllers/
│   └── ProductsController.cs     # REST endpoints
├── Models/
│   ├── Product.cs                # MongoDB document — tracked product
│   └── PriceHistory.cs           # MongoDB document — price snapshot
├── Services/
│   ├── MongoDbService.cs         # Database connection & collections
│   ├── ScraperService.cs         # Playwright price scraper
│   └── PriceTrackerBackgroundService.cs  # Scheduled background checker
├── appsettings.example.json      # Config template (committed)
├── appsettings.json              # Your local config (gitignored)
├── Program.cs                    # App bootstrap & DI setup
├── PriceWatch.csproj
└── README.md
```
