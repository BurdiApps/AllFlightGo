using System.Security.Claims;
using AllFlight.Components;
using AllFlight.Data;
using AllFlight.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
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
builder.Services.AddAuthorization();
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
})
.AddCookie(IdentityConstants.ExternalScheme);

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();