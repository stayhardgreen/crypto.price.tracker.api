using Microsoft.EntityFrameworkCore;
using CryptoPriceTracker.Api.Models;

namespace CryptoPriceTracker.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<CryptoAsset> CryptoAssets { get; set; }
        public DbSet<CryptoPriceHistory> CryptoPriceHistories { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CryptoAsset>().HasData(
                new CryptoAsset { Id = 1, Name = "Bitcoin", Symbol = "BTC", ExternalId = "bitcoin" },
                new CryptoAsset { Id = 2, Name = "Ethereum", Symbol = "ETH", ExternalId = "ethereum" }
            );
        }
    }
}