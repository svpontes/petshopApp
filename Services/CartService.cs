using System;
using System.Collections.Generic;
using System.Linq;
using PetShopApp.Models;

namespace PetShopApp.Services
{
    /// <summary>
    /// Service responsible for managing the shopping cart state, including adding, 
    /// removing, and updating product quantities.
    /// </summary>
    public class CartService
    {
        // Internal list to store items added to the cart
        private readonly List<CartItem> _cartItems = new();

        // Event triggered whenever the cart content changes to update the UI
        public event Action? OnChange;

        // Exposes the list of items in the cart
        public List<CartItem> CartItems => _cartItems;

        // Calculates the total number of individual items in the cart
        public int TotalItems => _cartItems.Sum(i => i.Quantity);

        // Calculates the total price of all items currently in the cart
        public decimal TotalPrice => _cartItems.Sum(i => i.Product.Price * i.Quantity);

        /// <summary>
        /// Adds a product to the cart or increments the quantity if the product and size already exist.
        /// </summary>
        /// <param name="product">The product to add.</param>
        /// <param name="size">Selected size for the product.</param>
        /// <param name="quantity">Quantity to be added.</param>
        public void AddToCart(Product product, string size, int quantity)
        {
            if (product == null || quantity <= 0)
                return;

            // Check if the exact product with the same size is already in the cart
            var existingItem = _cartItems.FirstOrDefault(i =>
                i.Product.Id == product.Id && i.Size == size);

            if (existingItem != null)
            {
                // Increment quantity for existing item
                existingItem.Quantity += quantity;
            }
            else
            {
                // Add new item to the collection
                _cartItems.Add(new CartItem
                {
                    Product = product,
                    Size = size,
                    Quantity = quantity
                });
            }

            NotifyStateChanged();
        }

        /// <summary>
        /// Adjusts the quantity of a specific cart item. Removes the item if quantity drops to zero.
        /// </summary>
        /// <param name="productId">ID of the product to update.</param>
        /// <param name="size">Specific size of the product.</param>
        /// <param name="change">Amount to add or subtract (e.g., +1 or -1).</param>
        public void UpdateQuantity(int productId, string size, int change)
        {
            var item = _cartItems.FirstOrDefault(i =>
                i.Product.Id == productId && i.Size == size);

            if (item == null)
                return;

            item.Quantity += change;

            // Remove item if user decreases quantity to 0 or less
            if (item.Quantity <= 0)
            {
                _cartItems.Remove(item);
            }

            NotifyStateChanged();
        }

        /// <summary>
        /// Removes a specific product and size combination from the cart.
        /// </summary>
        /// <param name="productId">ID of the product to remove.</param>
        /// <param name="size">Specific size of the product.</param>
        public void RemoveFromCart(int productId, string size)
        {
            var item = _cartItems.FirstOrDefault(i =>
                i.Product.Id == productId && i.Size == size);

            if (item == null)
                return;

            _cartItems.Remove(item);
            NotifyStateChanged();
        }

        /// <summary>
        /// Completely empties the shopping cart.
        /// </summary>
        public void ClearCart()
        {
            _cartItems.Clear();
            NotifyStateChanged();
        }

        /// <summary>
        /// Invokes the OnChange event to signal UI components (like the Cart Sidebar) to re-render.
        /// </summary>
        private void NotifyStateChanged()
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>
    /// Represents a single entry in the shopping cart.
    /// </summary>
    public class CartItem
    {
        public Product Product { get; set; } = new();
        public string Size { get; set; } = "M";
        public int Quantity { get; set; }
    }
}