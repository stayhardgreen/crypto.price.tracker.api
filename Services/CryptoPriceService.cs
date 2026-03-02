public class CryptoPriceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly HttpClient _httpClient;

    public CryptoPriceService(ApplicationDbContext dbContext, HttpClient httpClient)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
    }

    public async Task UpdatePricesAsync()
    {
        var cryptoAssets = _dbContext.CryptoAssets.ToList(); 
        var response = await _httpClient.GetAsync("https://api.coingecko.com/api/v3/simple/price?ids=bitcoin,ethereum&vs_currencies=usd");

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var prices = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json); 

            foreach (var asset in cryptoAssets)
            {
                var today = DateTime.UtcNow;
                var lastPrice = _dbContext.CryptoPriceHistories
                    .Where(p => p.CryptoAssetId == asset.Id)
                    .OrderByDescending(p => p.Date)
                    .FirstOrDefault();

                if (lastPrice != null && lastPrice.Date == today)
                {
                    continue;
                }

                var newPrice = prices[asset.ExternalId];
                if (newPrice > 0)
                {
                    var priceHistory = new CryptoPriceHistory
                    {
                        CryptoAssetId = asset.Id,
                        Price = newPrice,
                        Date = DateTime.UtcNow
                    };

                    _dbContext.CryptoPriceHistories.Add(priceHistory);
                }
            }
        }
    }
}