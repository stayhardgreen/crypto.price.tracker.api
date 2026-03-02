public class PriceValidator
{
    public bool ShouldSavePrice(decimal newPrice, DateTime date, List<CryptoPriceHistory> history)
    {
        if (newPrice <= 0) return false;
        return !history.Any(h => h.Date.Date == date.Date && h.Price == newPrice);
    }
}