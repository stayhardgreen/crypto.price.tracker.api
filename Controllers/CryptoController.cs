using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            try
            {
                await _service.UpdatePricesAsync();

                return Ok(new
                {
                    message = "Prices updated successfully."
                });
            }
            catch
            {
                // For this exercise we return a generic error; in a real app we would log details.
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Failed to update prices."
                });
            }
        }

        /// <summary>
        /// Returns the latest recorded price per crypto asset.
        /// This allows the frontend to display the most recent data saved in the database.
        /// </summary>
        /// <remarks>
        /// Assumption: All prices are fetched in USD from CoinGecko, so we hardcode the currency to "USD".
        /// </remarks>
        /// <returns>A list of assets and their latest recorded price</returns>
        [HttpGet("latest-prices")]
        [HttpGet("lastest-prices")] // Keep a second route to be resilient to potential typo usage.
        public async Task<IActionResult> GetLatestPrices([FromServices] ApplicationDbContext db)
        {
            var latest = await db.CryptoAssets
                .Select(asset => new
                {
                    asset.Id,
                    asset.Name,
                    asset.Symbol,
                    asset.ExternalId,
                    asset.IconUrl,
                    LatestPrice = asset.PriceHistory
                        .OrderByDescending(p => p.Date)
                        .FirstOrDefault()
                })
                .Where(x => x.LatestPrice != null)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Symbol,
                    x.ExternalId,
                    x.IconUrl,
                    Price = x.LatestPrice!.Price,
                    Currency = "USD",
                    LastUpdated = x.LatestPrice.Date
                })
                .ToListAsync();

            return Ok(latest);
        }
    }
}