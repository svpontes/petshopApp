using Microsoft.AspNetCore.Identity;
using PetShopApp.Models;

namespace PetShopApp.Services;

/// <summary>
/// Keeps Identity password validation consistent with the visible registration checklist.
/// </summary>
public sealed class RegistrationPasswordValidator : IPasswordValidator<User>
{
    public Task<IdentityResult> ValidateAsync(
        UserManager<User> manager, User user, string? password)
    {
        // Whitespace must not satisfy the special-character requirement.
        var hasVisibleSpecialCharacter = password?.Any(character =>
            !char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character)) == true;

        var result = hasVisibleSpecialCharacter
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordRequiresVisibleSpecialCharacter",
                Description = "Passwords must contain a special character other than whitespace."
            });

        return Task.FromResult(result);
    }
}
