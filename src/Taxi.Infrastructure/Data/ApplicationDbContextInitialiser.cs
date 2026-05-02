namespace Taxi.Infrastructure.Data;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Taxi.Domain.Cars;
using Taxi.Infrastructure.Identity;

public class ApplicationDbContextInitialiser(
    ILogger<ApplicationDbContextInitialiser> logger,
    AppDbContext context,
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager)
{
    private readonly ILogger<ApplicationDbContextInitialiser> logger = logger;
    private readonly AppDbContext context = context;
    private readonly UserManager<AppUser> userManager = userManager;
    private readonly RoleManager<IdentityRole> roleManager = roleManager;

    public async Task InitialiseAsync()
    {
        try
        {
            if (this.context.Database.IsNpgsql())
            {
                await this.context.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "An error occurred while initialising the database.");
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await this.TrySeedAsync();
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task TrySeedAsync()
    {
        // 1. Seed Roles
        if (!await this.roleManager.RoleExistsAsync("Manager"))
        {
            await this.roleManager.CreateAsync(new IdentityRole("Manager"));
        }

        // 2. Seed Manager
        var managerEmail = "admin@taxi.com";
        if (await this.userManager.FindByEmailAsync(managerEmail) == null)
        {
            var manager = new AppUser { UserName = managerEmail, Email = managerEmail, EmailConfirmed = true };
            await this.userManager.CreateAsync(manager, "Admin123!");
            await this.userManager.AddToRoleAsync(manager, "Manager");
        }

        // 3. Seed Cars
        if (!await this.context.Cars.AnyAsync())
        {
            var carResult = Car.Create(
                Guid.NewGuid(),
                "Tesla",
                "Model S",
                2024,
                "A fast electric sedan.",
                "سيارة سيدان كهربائية سريعة.");
            if (carResult.IsSuccess)
            {
                this.context.Cars.Add(carResult.Value);
                await this.context.SaveChangesAsync();
            }
        }
    }
}