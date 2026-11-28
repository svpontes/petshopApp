using Microsoft.EntityFrameworkCore;
using PetShopApp.Data;
using PetShopApp.Models;

namespace PetShopApp.Services;

public sealed class CustomerOrderHistoryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly AuthService _authService;

    public CustomerOrderHistoryService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        AuthService authService)
    {
        _dbFactory = dbFactory;
        _authService = authService;
    }

    public async Task<List<Order>> GetMyOrdersAsync()
    {
        // Enforce account authorization inside the service as well as the UI.
        if (!await _authService.IsAuthorizedAsync())
            throw new UnauthorizedAccessException("Please sign in again.");
            var customer = _authService.CurrentUser;

        if (customer is null)
            throw new InvalidOperationException("Sign in to view your orders.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var orders = await db.Orders
            .AsNoTracking()
            .Where(order => order.UserId == customer.Id)
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.Id)
            .ToListAsync();

        if (!ReferenceEquals(_authService.CurrentUser, customer))
            throw new InvalidOperationException("Your session has changed.");

        return orders;
    }
}