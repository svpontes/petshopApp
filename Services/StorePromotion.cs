namespace PetShopApp.Services;

public static class StorePromotion
{
    public const int DiscountPercent = 10;

    public static decimal GetSaleUnitPrice(decimal regularUnitPrice) =>
        decimal.Round(
            regularUnitPrice * (100 - DiscountPercent) / 100m,
            2,
            MidpointRounding.AwayFromZero);
}