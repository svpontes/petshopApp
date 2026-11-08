
using Microsoft.EntityFrameworkCore;
using PetShopApp.Models;

namespace PetShopApp.Data
{
    public static class ProductSeeder
    {
        public static async Task SeedAsync(
            ApplicationDbContext dbContext)
        {
            if (await dbContext.Products.AnyAsync())
                return;

            var products = new List<Product>
            {
                new Product
                {
                    Name = "Cat Food",
                    Description = "Food for adult cats.",
                    Category = "Food",
                    Price = 50.00m,
                    ImageUrl = "/images/cat-food.jpg",
                    SecondaryImageUrlsRaw = "",
                },

                new Product
                {
                    Name = "Comfort Pet Bed",
                    Description = "A soft and comfortable bed for your pet.",
                    Category = "Accessories",
                    Price = 35.00m,
                    ImageUrl = "/images/pet-bed.jpg",
                    SecondaryImageUrlsRaw = "",
                },

                new Product
                {
                    Name = "Classic Pet Collar",
                    Description = "An adjustable collar for everyday use.",
                    Category = "Accessories",
                    Price = 15.00m,
                    ImageUrl = "/images/pet-collar.jpg",
                    SecondaryImageUrlsRaw = "",
                }
            };

            await dbContext.Products.AddRangeAsync(products);

            await dbContext.SaveChangesAsync();
        }
    }
}