using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        await context.Database.MigrateAsync();

        if (await context.Categories.AnyAsync())
        {
            return;
        }

        var categories = new[]
        {
            new Category { Name = "Food", Description = "Groceries and dining out" },
            new Category { Name = "Transport", Description = "Fuel, transit, and travel" },
            new Category { Name = "Shopping", Description = "Personal purchases and essentials" },
            new Category { Name = "Utilities", Description = "Bills and household services" },
            new Category { Name = "Housing", Description = "Rent and accommodation" },
            new Category { Name = "Health", Description = "Medical and wellness expenses" }
        };

        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();
    }
}
