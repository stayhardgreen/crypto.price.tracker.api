public class CryptoPriceHistory
{
    public int Id { get; set; }
    public int CryptoAssetId { get; set; }
    public CryptoAsset CryptoAsset { get; set; }
    public DateTime Date { get; set; }
    public decimal Price { get; set; }
}