using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AllFlight.Services;
namespace AllFlight.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();

    // Admin-managed flights. Regular search still pulls live data from Duffel; this
    // table just backs the admin "Manage Flights" CRUD page. (Additive — flag for review.)
    public DbSet<Flight> Flights => Set<Flight>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Flight's Id is a string (a Duffel offer id, or a generated id for manual rows).
        modelBuilder.Entity<Flight>().HasKey(f => f.Id);
        // Score and Duration are calculated on the fly, so they aren't real columns.
        modelBuilder.Entity<Flight>().Ignore(f => f.Score);
        modelBuilder.Entity<Flight>().Ignore(f => f.Duration);
    }
}
