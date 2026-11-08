namespace PetShopApp.Models
{
    public class ProductVariant
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;

        public string Label { get; set; } = string.Empty;

        public decimal Price { get; set; }
    }
}