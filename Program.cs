using System.Security.Claims;
using AllFlight.Components;
using AllFlight.Data;
using AllFlight.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<DuffelService>();
builder.Services.AddScoped<FlightService>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=allflight.db"));

builder.Services.AddCascadingAuthenticationState();
// "AdminOnly" gates every /admin page: the user needs the "Admin" role claim,
// which AdminUserClaimsPrincipalFactory only hands out to accounts with IsAdmin = true.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        // Check the app (cookie) sign-in, and if it fails, the cookie's
        // OnRedirectToLogin sends /admin visitors to /admin/login (not Google).
        policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme);
        policy.RequireRole("Admin");
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SelectedFlightService>();

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(IdentityConstants.ApplicationScheme, options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/login";

    // Normal pages send unauthenticated visitors to the Google "/login". But the
    // admin area has its own password login, so if someone hits an "/admin" URL
    // without the right access, send them to "/admin/login" instead.
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/admin"))
            {
                context.Response.Redirect("/admin/login?returnUrl=" +
                    Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/admin"))
            {
                context.Response.Redirect("/admin/login?returnUrl=" +
                    Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        }
    };
})
.AddCookie(IdentityConstants.ExternalScheme);

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// Swap in our factory so signed-in users flagged IsAdmin get a "Admin" role claim.
// This runs for BOTH the Google login and the admin password login.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AdminUserClaimsPrincipalFactory>();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SaveTokens = true;
        options.CallbackPath = "/signin-google";
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var signInManager = context.HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();

                var loginProvider = GoogleDefaults.AuthenticationScheme;
                var identity = context.Identity;
                if (identity is null)
                {
                    throw new InvalidOperationException("Google authentication did not return an identity.");
                }

                var principal = new ClaimsPrincipal(identity);
                var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue(ClaimTypes.Email)
                    ?? Guid.NewGuid().ToString("N");
                var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue(ClaimTypes.Upn);
                var name = principal.FindFirstValue(ClaimTypes.Name);
                var avatarUrl = identity.FindFirst("urn:google:picture")?.Value
                    ?? identity.FindFirst("picture")?.Value;

                var user = await userManager.FindByLoginAsync(loginProvider, providerKey);
                if (user is null && !string.IsNullOrWhiteSpace(email))
                {
                    user = await userManager.FindByEmailAsync(email);
                }

                if (user is null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email ?? providerKey,
                        Email = email,
                        EmailConfirmed = true,
                        FullName = name,
                        AvatarUrl = avatarUrl
                    };

                    var createResult = await userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        throw new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    }
                }

                var loginInfo = new UserLoginInfo(loginProvider, providerKey, "Google");
                var existingLogin = await userManager.FindByLoginAsync(loginProvider, providerKey);
                if (existingLogin is null)
                {
                    var addLoginResult = await userManager.AddLoginAsync(user, loginInfo);
                    if (!addLoginResult.Succeeded)
                    {
                        throw new InvalidOperationException(string.Join(", ", addLoginResult.Errors.Select(e => e.Description)));
                    }
                }

                var appPrincipal = await signInManager.CreateUserPrincipalAsync(user);
                await context.HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, appPrincipal);
            }
        };
    });
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();

    // Make sure there is always one staff account to sign in with. The email and
    // password come from configuration (appsettings.Development.json for local dev).
    // This is a dev seed only, NOT a real production secret.
    var adminEmail = app.Configuration["Admin:Email"];
    var adminPassword = app.Configuration["Admin:Password"];
    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await userManager.FindByEmailAsync(adminEmail);
        if (existing is null)
        {
            // No admin yet, so create one from the configured email/password.
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "AllFlightGo Admin",
                IsAdmin = true
            };
            await userManager.CreateAsync(admin, adminPassword);
        }
        else if (!existing.IsAdmin)
        {
            // The account exists but wasn't marked admin, so flip the flag on.
            existing.IsAdmin = true;
            await userManager.UpdateAsync(existing);
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/login-google", async (HttpContext context) =>
{
    var redirectUri = context.Request.Query["returnUrl"].ToString();
    var target = string.IsNullOrWhiteSpace(redirectUri) ? "/" : redirectUri;
    var properties = new AuthenticationProperties { RedirectUri = target };
    await context.ChallengeAsync(GoogleDefaults.AuthenticationScheme, properties);
});

app.MapGet("/logout", async (HttpContext context, SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
});

// Handles the admin login form POST. We use a real form POST to a minimal-API
// endpoint (not an interactive EditForm) because signing in and setting the auth
// cookie needs a full HttpContext, which an interactive Blazor circuit doesn't have.
app.MapPost("/admin/authenticate", async (
    [FromForm] string? email,
    [FromForm] string? password,
    [FromForm] string? returnUrl,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) =>
{
    // Look up the account. Reject anyone who isn't a real, admin-flagged user.
    var user = string.IsNullOrWhiteSpace(email) ? null : await userManager.FindByEmailAsync(email);
    if (user is null || !user.IsAdmin || string.IsNullOrEmpty(password))
    {
        return Results.Redirect("/admin/login?error=1");
    }

    // Check the password and set the sign-in cookie.
    var result = await signInManager.PasswordSignInAsync(user.UserName!, password, isPersistent: false, lockoutOnFailure: false);
    if (!result.Succeeded)
    {
        return Results.Redirect("/admin/login?error=1");
    }

    // Send them where they were headed (only if it's a safe in-app path), else the dashboard.
    var target = !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') ? returnUrl : "/admin";
    return Results.Redirect(target);
}).DisableAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();