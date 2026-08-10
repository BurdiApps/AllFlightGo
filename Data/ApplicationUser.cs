using Microsoft.AspNetCore.Identity;

namespace AllFlight.Data;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
}