//Rebecca's mock data

using Microsoft.EntityFrameworkCore;
using PetShopApp.Models;

namespace PetShopApp.Data
{
    public static class ProductSeeder
    {
        public static async Task SeedAsync(
            ApplicationDbContext context)
        {
            await context.Database.MigrateAsync();

            if (!await context.AnimalTypes.AnyAsync())
            {
                context.AnimalTypes.AddRange(
                    new AnimalType { Name = "Dog" },
                    new AnimalType { Name = "Cat" },
                    new AnimalType { Name = "Horse" },
                    new AnimalType { Name = "Rabbit" },
                    new AnimalType { Name = "Bird" },
                    new AnimalType { Name = "Sheep" },
                    new AnimalType { Name = "Pig" }
                );

                await context.SaveChangesAsync();
            }

            if (!await context.Categories.AnyAsync())
            {
                context.Categories.AddRange(
                    new Category { Name = "Food" },
                    new Category { Name = "Accessories" },
                    new Category { Name = "Toys" }
                );

                await context.SaveChangesAsync();
            }

            if (!await context.Products.AnyAsync())
            {
                var dog = await context.AnimalTypes
                    .FirstAsync(a => a.Name == "Dog");

                var cat = await context.AnimalTypes
                    .FirstAsync(a => a.Name == "Cat");

                var food = await context.Categories
                    .FirstAsync(c => c.Name == "Food");

                var accessories = await context.Categories
                    .FirstAsync(c => c.Name == "Accessories");

                context.Products.AddRange(
                    new Product
                    {
                        Name = "Cat Food",
                        Description = "Food for adult cats lol.",
                        AnimalTypeId = cat.Id,
                        CategoryId = food.Id,
                        Category = "Food",
                        Price = 34.99m,
                        ImageUrl = "/images/cat-food.jpg"
                    },
                    new Product
                    {
                        Name = "Cat Collar",
                        Description = "COOL collar for the CAT.",
                        AnimalTypeId = cat.Id,
                        CategoryId = accessories.Id,
                        Category = "Accessories",
                        Price = 12.99m,
                        ImageUrl = "/images/cat-collar.jpg"
                    }
                );

                await context.SaveChangesAsync();
            }
        }
    }
}