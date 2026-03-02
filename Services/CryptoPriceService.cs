using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoPriceTracker.Api.Services;

/// <summary>
/// Fetches crypto prices from CoinGecko API and persists them to the database.
/// Fetches ALL coins from CoinGecko via /coins/markets (paginated), then upserts into our DB.
///
/// Duplicate/error handling:
/// - CryptoAsset: Use ExternalId (CoinGecko id) as unique key. Lookup before insert; skip insert if exists.
/// - CryptoPriceHistory: One record per (asset, date). ExcludeSet tracks assets that already have today's price.
/// - 429 (rate limit): Retry with exponential backoff (FetchMarketsPageAsync). We paginate with delay (2.5s) between pages
///   to stay under ~30 calls/min. If retries exhausted, return null and stop pagination.
/// - Null/invalid price: Skip coin. Avoids saving bogus data.
/// </summary>
public class CryptoPriceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 2000;
    private const int PageSize = 250;
    private const int MaxPages = 20; // ~5000 coins; adjust if needed. Rate limit ~30/min.
    private const int DelayBetweenPagesMs = 2500; // Stay under ~30 calls/min.

    public CryptoPriceService(ApplicationDbContext dbContext, HttpClient httpClient)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoPriceTracker/1.0");
    }

    public async Task UpdatePricesAsync()
    {
        var today = DateTime.UtcNow.Date;

        // Get asset IDs that already have a price for today to avoid duplicate CryptoPriceHistory.
        var assetIdsWithPriceToday = await _dbContext.CryptoPriceHistories
            .Where(p => p.Date.Date == today)
            .Select(p => p.CryptoAssetId)
            .Distinct()
            .ToListAsync();
        var excludeSet = new HashSet<int>(assetIdsWithPriceToday);

        // Load existing assets by ExternalId for upsert logic.
        var existingByExternalId = await _dbContext.CryptoAssets
            .ToDictionaryAsync(a => a.ExternalId, a => a);

        for (var page = 1; page <= MaxPages; page++)
        {
            var coins = await FetchMarketsPageAsync(page);

            if(coins == null) {
                await Task.Delay(15000);
                page --;
                continue;
            }

            if (coins.Count == 0)
                break;

            foreach (var coin in coins)
            {
                if (string.IsNullOrWhiteSpace(coin.Id) || coin.CurrentPrice is null or <= 0)
                    continue;

                CryptoAsset asset;
                if (existingByExternalId.TryGetValue(coin.Id, out var existing))
                {
                    asset = existing;
                    // Update icon if we have it and it changed (keeps data fresh).
                    if (!string.IsNullOrEmpty(coin.Image) && asset.IconUrl != coin.Image)
                    {
                        asset.IconUrl = coin.Image;
                    }
                }
                else
                {
                    asset = new CryptoAsset
                    {
                        Name = coin.Name ?? coin.Id,
                        Symbol = (coin.Symbol ?? "?").ToUpperInvariant(),
                        ExternalId = coin.Id,
                        IconUrl = coin.Image
                    };
                    _dbContext.CryptoAssets.Add(asset);
                    existingByExternalId[coin.Id] = asset;
                }

                // Only add price history if we don't already have one for today.
                // For existing assets: check excludeSet. For new assets: use navigation so EF resolves FK.
                if (excludeSet.Contains(asset.Id))
                    continue;

                _dbContext.CryptoPriceHistories.Add(new CryptoPriceHistory
                {
                    CryptoAsset = asset,
                    Price = coin.CurrentPrice.Value,
                    Date = today
                });
                if (asset.Id != 0)
                    excludeSet.Add(asset.Id);
            }

            if (coins.Count < PageSize)
            {
                break;
            }

            await Task.Delay(DelayBetweenPagesMs);
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Fetches one page of coins from CoinGecko /coins/markets.
    /// Handles 429 (rate limit) with exponential backoff retry.
    /// On transient failures we return null so caller can stop pagination.
    /// </summary>
    private async Task<List<CoinGeckoMarketItem>?> FetchMarketsPageAsync(int page)
    {
        var url = $"https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&per_page={PageSize}&page={page}";
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            using var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<CoinGeckoMarketItem>>(json);
            }
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < MaxRetries)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta;
                var wait = retryAfter ?? TimeSpan.FromMilliseconds(RetryDelayMs * attempt * attempt); // 2s, 8s, 18s
                await Task.Delay(wait);
                continue;
            }
            return null;
        }
        return null;
    }

    private class CoinGeckoMarketItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("current_price")]
        public decimal? CurrentPrice { get; set; }

        [JsonPropertyName("last_updated")]
        public string? LastUpdated { get; set; }
    }
}
