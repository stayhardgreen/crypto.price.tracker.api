using System;
using System.Collections.Generic;
using CryptoPriceTracker.Api.Models;
using CryptoPriceTracker.Api.Validators;
using Xunit;

namespace CryptoPriceTracker.Api.Tests;

public class PriceValidatorTests
{
    private readonly PriceValidator _validator = new();

    [Fact]
    public void ShouldSavePrice_ReturnsFalse_ForNonPositivePrice()
    {
        var history = new List<CryptoPriceHistory>();

        Assert.False(_validator.ShouldSavePrice(0m, DateTime.UtcNow, history));
        Assert.False(_validator.ShouldSavePrice(-10m, DateTime.UtcNow, history));
    }

    [Fact]
    public void ShouldSavePrice_ReturnsFalse_WhenSamePriceAlreadyExistsForDate()
    {
        var today = DateTime.UtcNow.Date;
        var history = new List<CryptoPriceHistory>
        {
            new()
            {
                Date = today,
                Price = 100m
            }
        };

        var result = _validator.ShouldSavePrice(100m, today, history);

        Assert.False(result);
    }

    [Fact]
    public void ShouldSavePrice_ReturnsTrue_WhenPriceIsNewForSameDate()
    {
        var today = DateTime.UtcNow.Date;
        var history = new List<CryptoPriceHistory>
        {
            new()
            {
                Date = today,
                Price = 100m
            }
        };

        var result = _validator.ShouldSavePrice(105m, today, history);

        Assert.True(result);
    }

    [Fact]
    public void ShouldSavePrice_IgnoresSamePriceOnDifferentDate()
    {
        var today = DateTime.UtcNow.Date;
        var history = new List<CryptoPriceHistory>
        {
            new()
            {
                Date = today.AddDays(-1),
                Price = 100m
            }
        };

        var result = _validator.ShouldSavePrice(100m, today, history);

        Assert.True(result);
    }
}

