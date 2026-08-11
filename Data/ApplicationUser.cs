using Microsoft.AspNetCore.Identity;

namespace AllFlight.Data;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }

    // True for staff accounts that are allowed into the /admin dashboard.
    // A normal user leaves this false, so they never get the "Admin" role claim.
    public bool IsAdmin { get; set; }
}