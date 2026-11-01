
using System.ComponentModel.DataAnnotations;
using PetShopApp.Models;

namespace PetShopApp.Services;

public sealed class CreateOrderRequest
{
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public List<CreateOrderLine> Items { get; set; } = new();
}

public sealed class CreateOrderLine
{
    public int ProductId { get; set; }
    public string Size { get; set; } = "";
    public int Quantity { get; set; }
}

public class OrderService
{
    // Validate customer information and order items
    public IReadOnlyList<string> Validate(CreateOrderRequest? request)
    {
        var errors = new List<string>();

        if (request == null)
        {
            errors.Add("Order information is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            errors.Add("Customer name is required.");

        if (string.IsNullOrWhiteSpace(request.CustomerEmail) ||
            !new EmailAddressAttribute().IsValid(request.CustomerEmail))
            errors.Add("A valid email address is required.");

        if (string.IsNullOrWhiteSpace(request.CustomerPhone))
            errors.Add("Customer phone number is required.");

        if (string.IsNullOrWhiteSpace(request.ShippingAddress))
            errors.Add("Shipping address is required.");

        if (request.Items == null || request.Items.Count == 0)
        {
            errors.Add("The order must contain at least one item.");
        }
        else
        {
            foreach (var item in request.Items)
            {
                if (item == null || item.ProductId <= 0)
                    errors.Add("Invalid product.");
                else if (item.Quantity <= 0)
                    errors.Add("Product quantity must be greater than zero.");
            }
        }

        return errors;
    }

    // Calculate total using trusted product prices
    public decimal CalculateTotal(
        CreateOrderRequest request,
        IEnumerable<Product> currentProducts)
    {
        var errors = Validate(request);

        if (errors.Count > 0)
            throw new ArgumentException(
                string.Join(" ", errors), nameof(request));

        ArgumentNullException.ThrowIfNull(currentProducts);

        var productsById = currentProducts.ToDictionary(p => p.Id);

        decimal total = 0m;

        foreach (var item in request.Items)
        {
            if (!productsById.TryGetValue(item.ProductId, out var product))
                throw new InvalidOperationException(
                    $"Product {item.ProductId} does not exist.");

            if (product.Price < 0)
                throw new InvalidOperationException(
                    $"Product {item.ProductId} has an invalid price.");

            total = checked(total + product.Price * item.Quantity);
        }

        return total;
    }
}
