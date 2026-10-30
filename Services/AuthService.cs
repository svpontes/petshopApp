using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetShopApp.Data;
using PetShopApp.Models;

namespace PetShopApp.Services
{
    /// <summary>
    /// Service responsible for handling user authentication, session state, and role authorization.
    /// </summary>
    public class AuthService
    {
        // Scope factory is used to create a temporary database context scope 
        // within this service, ensuring thread safety in Blazor Server.
        private readonly IServiceScopeFactory _scopeFactory;

        // Holds the currently authenticated user's data. Null if no user is logged in.
        public User? CurrentUser { get; set; }

        // Event triggered whenever the authentication state changes (Login/Logout),
        // allowing UI components to refresh automatically.
        public event Action? OnChange;

        // Helper property to check if a session is active.
        public bool IsLoggedIn => CurrentUser != null;

        // Helper property to check if the current user has administrative privileges.
        public bool IsAdmin => CurrentUser?.Role == "Admin";

        public AuthService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Validates user credentials against the database and initializes the session.
        /// </summary>
        /// <param name="email">User's email address.</param>
        /// <param name="password">User's plain-text password.</param>
        /// <returns>True if authentication succeeds; otherwise, false.</returns>
        public async Task<bool> Login(string email, string password)
        {
            // Create a temporary scope to resolve the ApplicationDbContext
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Retrieve the user from the database, including their OrderHistory 
            // to ensure it is available on the Profile page.
            var user = await context.Users
                .Include(u => u.OrderHistory)
                .FirstOrDefaultAsync(u => u.Email == email && u.Password == password);

            if (user is null)
                return false;

            // Update session state
            CurrentUser = user;
            NotifyStateChanged();
            return true;
        }

        /// <summary>
        /// Clears the current session and resets authentication state.
        /// </summary>
        public void Logout()
        {
            CurrentUser = null;
            NotifyStateChanged();
        }

        /// <summary>
        /// Invokes the OnChange event to notify subscribed components of a state update.
        /// </summary>
        private void NotifyStateChanged()
        {
            OnChange?.Invoke();
        }
    }
}