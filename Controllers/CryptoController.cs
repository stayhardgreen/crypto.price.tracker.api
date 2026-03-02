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
        private const int DefaultPageSize = 24;
        private const int MaxPageSize = 200;

        // Constructor with dependency injection of the service
        public CryptoController(CryptoPriceService service)
        {
            _service = service;
        }

        /// <summary>
        /// Triggers a price update by fetching prices from the CoinGecko API and saving them in the database.
        ///
        /// NOTE: This endpoint is kept for backward compatibility.
        /// Prefer using POST api/crypto/prices-paged to both refresh and fetch in one call.
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
        public async Task<IActionResult> GetLatestPrices(
            [FromServices] ApplicationDbContext db,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = DefaultPageSize,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] string? sortDir = "asc")
        {
            // Requirement: pagination/search/sort must be applied on BOTH backend and frontend,
            // but via this single endpoint. Returning { items, totalCount, ... } lets the UI paginate
            // without downloading the full dataset each time.
            page = page < 1 ? 1 : page;
            pageSize = pageSize switch
            {
                < 1 => DefaultPageSize,
                > MaxPageSize => MaxPageSize,
                _ => pageSize
            };

            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            sortBy = string.IsNullOrWhiteSpace(sortBy) ? "name" : sortBy.Trim().ToLowerInvariant();
            sortDir = string.IsNullOrWhiteSpace(sortDir) ? "asc" : sortDir.Trim().ToLowerInvariant();
            var desc = sortDir == "desc";

            var assetsQuery = db.CryptoAssets
                .AsNoTracking()
                .AsQueryable();

            if (search is not null)
            {
                // SQLite LIKE behavior varies by collation; ToLower() keeps behavior consistent enough for this exercise.
                var query = search.ToLower();
                assetsQuery = assetsQuery.Where(asset =>
                    asset.Name.ToLower().Contains(query) ||
                    asset.Symbol.ToLower().Contains(query) ||
                    asset.ExternalId.ToLower().Contains(query));
            }

            var filtered = assetsQuery
                .Select(asset => new
                {
                    asset.Id,
                    asset.Name,
                    asset.Symbol,
                    asset.ExternalId,
                    asset.IconUrl,
                    LatestPrice = asset.PriceHistory
                        .OrderByDescending(price => price.Date)
                        .FirstOrDefault(),
                    PreviousPrice = asset.PriceHistory
                        .OrderByDescending(price => price.Date)
                        .Skip(1)
                        .FirstOrDefault()
                })
                .Where(x => x.LatestPrice != null)
                .Select(x => new LatestPriceDto {
                    Id = x.Id,
                    Name = x.Name,
                    Symbol = x.Symbol,
                    ExternalId = x.ExternalId,
                    IconUrl = x.IconUrl,
                    Price = x.LatestPrice!.Price,
                    PreviousPrice = x.PreviousPrice != null ? (decimal?)x.PreviousPrice.Price : null,
                    Currency = "USD",
                    LastUpdated = x.LatestPrice.Date,
                    PercentChange = x.PreviousPrice != null && x.PreviousPrice.Price != 0
                        ? (decimal?)((x.LatestPrice.Price - x.PreviousPrice.Price) / x.PreviousPrice.Price * 100)
                        : null
                });

            var totalCount = await filtered.CountAsync();
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            IOrderedQueryable<LatestPriceDto> ordered = sortBy switch
            {
                "price" => desc
                    ? filtered.OrderByDescending(x => (double)x.Price).ThenBy(x => x.Name).ThenBy(x => x.Id)
                    : filtered.OrderBy(x => (double)x.Price).ThenBy(x => x.Name).ThenBy(x => x.Id),
                "updated" or "lastupdated" => desc
                    ? filtered.OrderByDescending(x => x.LastUpdated).ThenBy(x => x.Name).ThenBy(x => x.Id)
                    : filtered.OrderBy(x => x.LastUpdated).ThenBy(x => x.Name).ThenBy(x => x.Id),
                _ => desc
                    ? filtered.OrderByDescending(x => x.Name).ThenBy(x => x.Id)
                    : filtered.OrderBy(x => x.Name).ThenBy(x => x.Id)
            };

            var items = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                items,
                page,
                pageSize,
                totalCount,
                totalPages,
                search,
                sortBy,
                sortDir = desc ? "desc" : "asc"
            });
        }

        private sealed class LatestPriceDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Symbol { get; set; } = "";
            public string ExternalId { get; set; } = "";
            public string? IconUrl { get; set; }
            public decimal Price { get; set; }
            public decimal? PreviousPrice { get; set; }
            public string Currency { get; set; } = "USD";
            public DateTime LastUpdated { get; set; }
            public decimal? PercentChange { get; set; }
        }
    }
}