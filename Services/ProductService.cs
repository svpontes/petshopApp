using Microsoft.EntityFrameworkCore;
using PetShopApp.Data;
using PetShopApp.Models;

namespace PetShopApp.Services;

/// <summary>
/// Provides read-only catalog queries with a fresh context per operation.
/// </summary>
public sealed class ProductService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory)
{
    /// <summary>
    /// Lists all products or products linked to the selected category.
    /// A null category ID includes products without an assigned category.
    /// </summary>
    public async Task<List<Product>> GetCatalogProductsAsync(
        int? categoryId = null)
    {
        await using var db =
            await dbContextFactory.CreateDbContextAsync();

        // Load related data before disposing the database context.
        IQueryable<Product> query = db.Products
            .AsNoTracking()
            .Include(product => product.ProductCategory)
            .Include(product => product.Variants);

        // Filter using the category relationship.
        if (categoryId is int selectedCategoryId)
        {
            query = query.Where(product =>
                product.CategoryId == selectedCategoryId);
        }

        // Keep the result order consistent, including duplicate names.
        return await query
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Lists categories alphabetically for catalog filters.
    /// </summary>
    public async Task<List<Category>> GetCategoriesAsync()
    {
        await using var db =
            await dbContextFactory.CreateDbContextAsync();

        return await db.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Returns a product with its category and variants.
    /// Returns null when the requested product does not exist.
    /// </summary>
    public async Task<Product?> GetProductByIdAsync(int productId)
    {
        await using var db =
            await dbContextFactory.CreateDbContextAsync();

        return await db.Products
            .AsNoTracking()
            .Include(product => product.ProductCategory)
            .Include(product => product.Variants)
            .SingleOrDefaultAsync(product =>
                product.Id == productId);
    }
}