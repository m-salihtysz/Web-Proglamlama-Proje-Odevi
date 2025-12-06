using FitnessCenter.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitnessCenter.Web.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("FitnessCenter.Web.Data.DbInitializer");

            // Ensure database is created - use timeout to prevent hanging
            try
            {
                // Check if we can connect to the database
                var canConnect = await context.Database.CanConnectAsync(cancellationToken);
                
                if (!canConnect)
                {
                    await context.Database.EnsureCreatedAsync(cancellationToken);
                    logger.LogInformation("Database created successfully");
                }
                else
                {
                    // If database exists, checkpoint WAL to ensure clean state
                    try
                    {
                        await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken);
                    }
                    catch
                    {
                        // Ignore checkpoint errors - not critical
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Database connection timeout - database may be locked. Continuing anyway.");
                return; // Exit early if database connection times out
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx) when (sqlEx.SqliteErrorCode == 5) // SQLITE_BUSY
            {
                logger.LogWarning("Database is busy/locked. Will retry on next request.");
                return; // Exit early if database is locked
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error checking/creating database - continuing anyway");
                return; // Exit early if database creation fails
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Create roles
            string[] roleNames = { "Admin", "Member" };
            foreach (var roleName in roleNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Create admin user
            var adminEmail = "ogrencinumarasi@sakarya.edu.tr";
            var adminPassword = "sau";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "User"
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    logger.LogInformation("Admin user created successfully: {Email}", adminEmail);
                }
                else
                {
                    logger.LogError("Failed to create admin user. Errors: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                // Ensure admin is in Admin role
                if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    logger.LogInformation("Admin role added to existing user: {Email}", adminEmail);
                }
                else
                {
                    logger.LogInformation("Admin user already exists with correct role: {Email}", adminEmail);
                }
            }

        }
    }
}

