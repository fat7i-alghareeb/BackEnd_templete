namespace Taxi.Infrastructure.Data;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Taxi.Domain.Cars;
using Taxi.Infrastructure.Identity;
using Taxi.Infrastructure.Settings;

public class ApplicationDbContextInitialiser(
    ILogger<ApplicationDbContextInitialiser> logger,
    AppDbContext context,
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<SeedSettings> seedSettings)
{
    private readonly ILogger<ApplicationDbContextInitialiser> logger = logger;
    private readonly AppDbContext context = context;
    private readonly UserManager<AppUser> userManager = userManager;
    private readonly RoleManager<IdentityRole> roleManager = roleManager;
    private readonly SeedSettings seedSettings = seedSettings.Value;

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

    /// <summary>
    /// Drops the entire database and re-applies all migrations.
    /// DESTRUCTIVE — only meant for local development resets.
    /// Caller is responsible for gating this behind an environment / config check.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await this.context.Database.EnsureDeletedAsync();
        await this.context.Database.MigrateAsync();
    }

    private async Task TrySeedAsync()
    {
        // 1. Roles — not secret, always present so role checks never reference a missing role.
        if (!await this.roleManager.RoleExistsAsync("Manager"))
        {
            EnsureSucceeded(await this.roleManager.CreateAsync(new IdentityRole("Manager")), "create role Manager");
        }

        // 2. Bootstrap admin — only when Seed:Admin:Email and Seed:Admin:Password are configured.
        await this.SeedUserAsync(this.seedSettings.Admin, "Manager");

        // 3. Sample data — opt-in, so a fresh staging or production database starts empty.
        if (this.seedSettings.SampleData && !await this.context.Cars.AnyAsync())
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

    private async Task SeedUserAsync(SeedUserSettings settings, string role)
    {
        if (!settings.IsConfigured)
        {
            this.logger.LogInformation("No seed account configured for role {Role}; skipping.", role);
            return;
        }

        if (await this.userManager.FindByEmailAsync(settings.Email!) is not null)
        {
            return;
        }

        var user = new AppUser { UserName = settings.Email, Email = settings.Email, EmailConfirmed = true };

        EnsureSucceeded(await this.userManager.CreateAsync(user, settings.Password!), $"create seed user for role {role}");
        EnsureSucceeded(await this.userManager.AddToRoleAsync(user, role), $"add seed user to role {role}");

        this.logger.LogInformation("Seeded {Role} account.", role);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seeding failed to {operation}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
