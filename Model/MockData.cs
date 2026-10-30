namespace PetShopApp.Models
{
    public static class MockData
    {
        public static List<Product> GetMockProducts()
        {
            return new List<Product>
            {
                new Product 
                {
                    Id = 1,
                    Name = "Metallica Vintage Art",
                    Description = "Arte licenciada e exclusiva estilo rock vintage.",
                    Price = 45.90m,
                    AvailableSizes = new List<string> { "S", "M", "L", "XL" },
                    ImageUrl = "/images/metallica-arte.png",
                    // IMAGENS ADICIONAIS (Exemplos de nomes de arquivos)
                    SecondaryImageUrls = new List<string> {
                        "/images/metallica-arte-back.png",
                        "/images/metallica-arte-detail1.png",
                        "/images/metallica-arte-detail2.png"
                    }
                },
                new Product 
                {
                    Id = 2,
                    Name = "Retro Sunset Vibe",
                    Description = "Pôr do sol retrô com estética synthwave dos anos 80.",
                    Price = 29.99m,
                    AvailableSizes = new List<string> { "M", "L", "XXL" },
                    ImageUrl = "/images/sunset.png",
                    SecondaryImageUrls = new List<string> {
                        "/images/sunset-back.png",
                        "/images/sunset-detail1.png",
                        "/images/sunset-detail2.png"
                    }
                },
                new Product 
                {
                    Id = 3,
                    Name = "The Great Streetwave",
                    Description = "A famosa onda de Kanagawa remixada para o streetwear.",
                    Price = 34.50m,
                    AvailableSizes = new List<string> { "S", "M", "L" },
                    ImageUrl = "/images/wave.png",
                    SecondaryImageUrls = new List<string> {
                        "/images/wave-back.png",
                        "/images/wave-detail1.png",
                        "/images/wave-detail2.png"
                    }
                },
                new Product 
                {
                    Id = 4,
                    Name = "Classic Skull & Bones",
                    Description = "O design atemporal de crânio e ossos cruzados.",
                    Price = 32.00m,
                    AvailableSizes = new List<string> { "S", "M", "L", "XL", "XXL" },
                    ImageUrl = "/images/classic-skull.png",
                    SecondaryImageUrls = new List<string> {
                        "/images/classic-skull-back.png",
                        "/images/classic-skull-detail1.png",
                        "/images/classic-skull-detail2.png"
                    }
                },
                new Product 
                {
                    Id = 5,
                    Name = "Cyberpunk Night City",
                    Description = "Ilustração futurista inspirada na cultura cyberpunk.",
                    Price = 39.99m,
                    AvailableSizes = new List<string> { "M", "L", "XL" },
                    ImageUrl = "/images/cyberpunk.png",
                    SecondaryImageUrls = new List<string> {
                        "/images/cyberpunk-back.png",
                        "/images/cyberpunk-detail1.png",
                        "/images/cyberpunk-detail2.png"
                    }
                },
                new Product 
                {
                    Id = 6,
                    Name = "Freedom Eagle Graphic",
                    Description = "Arte arrojada de águia para um visual impactante.",
                    Price = 36.00m,
                    AvailableSizes = new List<string> { "S", "M", "L", "XL" },
                    ImageUrl = "/images/eagle.png",
                    SecondaryImageUrls = new List<string> {
                        "/images/eagle-back.png",
                        "/images/eagle-detail1.png",
                        "/images/eagle-detail2.png"
                    }
                }
            };
        }
    }
}