using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;

namespace PetShopApp.Models;

/// <summary>
/// Extends Identity while retaining the existing integer account IDs and profile data.
/// </summary>
public class User : IdentityUser<int>
{
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    // Retain the legacy role field during migration; Identity role memberships will be authoritative.
    public string Role { get; set; } = "Customer";

    // Keep existing callers compatible with Identity's nullable email declaration.
    [AllowNull]
    public override string Email
    {
        get => base.Email ?? string.Empty;
        set => base.Email = value;
    }

    // Preserve the legacy API while Identity owns the mapped password hash.
    [NotMapped]
    public string Password
    {
        get => PasswordHash ?? string.Empty;
        set => PasswordHash = value;
    }

    // Preserve the legacy profile API while Identity owns the mapped phone number.
    [NotMapped]
    public string? Phone
    {
        get => PhoneNumber;
        set => PhoneNumber = value;
    }

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public List<Order> OrderHistory { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
