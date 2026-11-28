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

                var toys = await context.Categories
                    .FirstAsync(c => c.Name == "Toys");

                context.Products.AddRange(
                    new Product
                    {
                        Name = "Feline Feast, Veterinary Diet Wet Cat Food",
                        Description = "This dish is packed with succulent bites of cod, turkey or chicken that's purr-fect for cats of all ages.",
                        AnimalTypeId = cat.Id,
                        CategoryId = food.Id,
                        Category = "Food",
                        Price = 34.99m,
                        ImageUrl = "/images/cat-food.jpg",

                        Variants = new List<ProductVariant>
                        {
                            new ProductVariant
                            {
                                Label = "2 lb",
                                Price = 15.99m
                            },
                            new ProductVariant
                            {
                                Label = "5 lb",
                                Price = 34.99m
                            },
                            new ProductVariant
                            {
                                Label = "10 lb",
                                Price = 59.99m
                            }
                        }
                    },
                    new Product
                    {
                        Name = "Travel Breakaway Cat Collar With Bell, Orange",
                        Description = "A comfortable, durable and stylish collar for your cat! Put an end to your worries about your cat's safety with this innovative thin cat collar.",
                        AnimalTypeId = cat.Id,
                        CategoryId = accessories.Id,
                        Category = "Accessories",
                        Price = 12.99m,
                        ImageUrl = "/images/cat-collar.jpg",

                        Variants = new List<ProductVariant>
                        {
                            new ProductVariant
                            {
                                Label = "Small",
                                Price = 10.99m
                            },
                            new ProductVariant
                            {
                                Label = "Medium",
                                Price = 12.99m
                            },
                            new ProductVariant
                            {
                                Label = "Large",
                                Price = 14.99m
                            }
                        }
                    },
                    new Product
                    {
                        Name = "LED Dog Collar Recharchable, Blue",
                        Description = "This stylish collar provides protection for your dog and offers amazing 500 feet high visibility, which enchances your pet's visibility in the dark or blind spots, preventing accidents or any other potential dangers.",
                        AnimalTypeId = dog.Id,
                        CategoryId = accessories.Id,
                        Category = "Accessories",
                        Price = 11.70m,
                        ImageUrl = "/images/DogCollar.jpg",

                        Variants = new List<ProductVariant>
                        {
                            new ProductVariant
                            {
                                Label = "Small",
                                Price = 11.70m
                            },
                            new ProductVariant
                            {
                                Label = "Medium",
                                Price = 12.99m
                            },
                            new ProductVariant
                            {
                                Label = "Large",
                                Price = 14.99m
                            }
                        }
                    },
                    new Product
                    {
                        Name = "Chicken and Beef Flavor Dog Kibble Adult Dry Dog Food",
                        Description = "This nutritious dish features real chicken or beef as the #1 ingredient to help keep your dog happy and healthy.",
                        AnimalTypeId = dog.Id,
                        CategoryId = food.Id,
                        Category = "Food",
                        Price = 34.99m,
                        ImageUrl = "/images/dog-food.jpg",

                        Variants = new List<ProductVariant>
                        {
                            new ProductVariant
                            {
                                Label = "2 lb",
                                Price = 15.99m
                            },
                            new ProductVariant
                            {
                                Label = "5 lb",
                                Price = 34.99m
                            },
                            new ProductVariant
                            {
                                Label = "10 lb",
                                Price = 59.99m
                            }
                        }
                    },
                    new Product
                    {
                        Name = "Plush Squeaky Dog Toy",
                        Description = "The soft exterior and inner squeaker of this cute companion provide sound and texture that dogs love, helping to encourage the exercise they need to stay healthy.",
                        AnimalTypeId = dog.Id,
                        CategoryId = toys.Id,
                        Category = "Toys",
                        Price = 5.00m,
                        ImageUrl = "/images/dog-toy.jpg",
                    },
                    new Product
                    {
                        Name = "Automatic, Detachable, Interactive Cat Toy, USB Rechargeable Cat Toy for Indoor Cats",
                        Description = "This recharable cat toy will spend all day keeping your cat happy. Surprise your cat with unexpected direction from a soft, mouse wire and add some fun with a ball that's always on the move.",
                        AnimalTypeId = cat.Id,
                        CategoryId = toys.Id,
                        Category = "Toys",
                        Price = 24.99m,
                        ImageUrl = "/images/cat-toy.jpg",
                    }
                );

                await context.SaveChangesAsync();
            }
        }
    }
}