using System.Security.Claims;
using AllFlight.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AllFlight.Services;

// Adds a "Admin" role claim to users flagged IsAdmin, so [Authorize(Roles="Admin")]
// and <AuthorizeView Roles="Admin"> gate the admin dashboard. Runs for both the
// Google sign-in (CreateUserPrincipalAsync) and the admin password login.
public class AdminUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser>
{
    public AdminUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, optionsAccessor)
    {
    }

    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);

        if (user.IsAdmin && principal.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        }

        return principal;
    }
}
