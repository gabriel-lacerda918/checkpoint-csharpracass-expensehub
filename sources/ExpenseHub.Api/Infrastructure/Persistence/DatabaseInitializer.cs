using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ExpenseHub.Api.Infrastructure.Persistence;

/// <summary>
/// Creates the database when missing and seeds the required data. Running it again changes nothing.
/// The only user ever created by the seed is the initial Admin.
/// </summary>
internal static class DatabaseInitializer
{
    // Databases created before the report feature do not get the new table from EnsureCreated, so the
    // table is also created here when missing. Keep it identical to the ExpenseReport mapping in AppDbContext.
    private const string CreateReportsTable = """
        CREATE TABLE IF NOT EXISTS "ExpenseReports" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_ExpenseReports" PRIMARY KEY,
            "RequesterId" TEXT NOT NULL,
            "Scope" TEXT NOT NULL,
            "FromDate" TEXT NOT NULL,
            "ToDate" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            "CreatedAtUtc" TEXT NOT NULL,
            "StartedAtUtc" TEXT NULL,
            "CompletedAtUtc" TEXT NULL,
            "ResultJson" TEXT NULL,
            "ErrorCode" TEXT NULL,
            "ErrorMessage" TEXT NULL,
            "Attempts" INTEGER NOT NULL,
            "Version" INTEGER NOT NULL,
            CONSTRAINT "FK_ExpenseReports_AspNetUsers_RequesterId" FOREIGN KEY ("RequesterId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
        );
        """;

    private const string CreateReportsIndex = """
        CREATE INDEX IF NOT EXISTS "IX_ExpenseReports_RequesterId" ON "ExpenseReports" ("RequesterId");
        """;

    private static readonly string[] DefaultCategories = { "Travel", "Meals", "Lodging", "Transport", "Other" };

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IServiceProvider provider = scope.ServiceProvider;

        var context = provider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);

        // WAL lets the web application and the worker share the same SQLite file without blocking each other.
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await context.Database.ExecuteSqlRawAsync(CreateReportsTable, cancellationToken);
        await context.Database.ExecuteSqlRawAsync(CreateReportsIndex, cancellationToken);

        await SeedRolesAsync(provider.GetRequiredService<RoleManager<IdentityRole>>());
        await SeedCategoriesAsync(context, cancellationToken);
        await SeedAdminAsync(
            provider.GetRequiredService<UserManager<IdentityUser>>(),
            provider.GetRequiredService<IConfiguration>(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("ExpenseHub.Seed"));
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (string role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)), "create role " + role);
            }
        }
    }

    private static async Task SeedCategoriesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.ExpenseCategories.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = DefaultCategories.Select((name, index) => new ExpenseCategory(index + 1, name)).ToList();
        context.ExpenseCategories.AddRange(categories);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAdminAsync(UserManager<IdentityUser> userManager, IConfiguration configuration, ILogger logger)
    {
        string? email = configuration["Seed:AdminEmail"];
        string? initialPassword = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(initialPassword))
        {
            logger.LogWarning("Seed:AdminEmail or Seed:AdminPassword is not configured; the initial Admin was not created.");
            return;
        }

        IdentityUser? admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            EnsureSucceeded(await userManager.CreateAsync(admin, initialPassword), "create the initial Admin");
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(admin, Roles.Admin), "assign the Admin role");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            IEnumerable<string> errors = result.Errors.Select(error => error.Description);
            throw new InvalidOperationException($"Seed failed to {operation}: {string.Join("; ", errors)}");
        }
    }
}
