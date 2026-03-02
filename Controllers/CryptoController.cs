using Microsoft.AspNetCore.Mvc;
using CryptoPriceTracker.Api.Services;
using CryptoPriceTracker.Api.Data;

namespace CryptoPriceTracker.Api.Controllers
{
    [ApiController]
    [Route("api/crypto")]
    public class CryptoController : ControllerBase
    {
        private readonly CryptoPriceService _service;

        // Constructor with dependency injection of the service
        public CryptoController(CryptoPriceService service)
        {
            _service = service;
        }

        /// <summary>
        /// TODO: Implement logic to call the UpdatePricesAsync method from the service
        /// This endpoint should trigger a price update by fetching prices from the CoinGecko API
        /// and saving them in the database through the service logic.
        /// </summary>
        /// <returns>200 OK with a confirmation message once done</returns>
        [HttpPost("update-prices")]
        public async Task<IActionResult> UpdatePrices()
        {
            // Uncomment and call the service here
            // await _service.UpdatePricesAsync();

            return Ok("Prices updated."); // Optional: Replace with a real result message
        }

        /// <summary>
        /// TODO: Implement an endpoint to return the latest prices per crypto asset.
        /// This will allow the frontend to display the most recent data saved in the database.
        /// </summary>
        /// <returns>A list of assets and their latest recorded price</returns>
        // [HttpGet("latest-prices")]
        // public async Task<IActionResult> GetLatestPrices([FromServices] ApplicationDbContext db)
        // {
        //     // Sample logic (you can customize this query as needed):
        //     var latest = await db.CryptoAssets
        //         .Select(asset => new {
        //             asset.Name,
        //             asset.Symbol,
        //             asset.ExternalId,
        //             Price = asset.PriceHistory
        //                 .OrderByDescending(p => p.Date)
        //                 .FirstOrDefault().Price
        //         })
        //         .ToListAsync();

        //     return Ok(latest);
        // }
    }
}