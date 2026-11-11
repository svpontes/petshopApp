using Microsoft.EntityFrameworkCore;
using PetShopApp.Data;
using PetShopApp.Models;

namespace PetShopApp.Services;

public sealed class ProductService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
{
    public async Task<List<Product>> GetCatalogProductsAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        return await db.Products
            .AsNoTracking()
            .Include(product => product.Variants)
            .OrderBy(product => product.Name)
            .ToListAsync();
    }
}