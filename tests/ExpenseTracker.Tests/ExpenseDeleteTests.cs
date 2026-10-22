using System.Security.Claims;
using ExpenseTracker.Controllers;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class ExpenseDeleteTests
{
    [Fact]
    public async Task Delete_RemovesOwnedExpenseAndReturnsToListWithConfirmation()
    {
        await using var context = CreateContext();
        var expense = await SeedExpenseAsync(context);
        var controller = CreateController(context, expense.UserId);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Delete(expense.Id, default));

        Assert.Equal("Home", result.ControllerName);
        Assert.Equal("Index", result.ActionName);
        Assert.Equal("Expense deleted.", controller.TempData["SuccessMessage"]);
        Assert.Empty(context.Expenses);
    }

    [Fact]
    public async Task Delete_RejectsAnotherUsersExpense()
    {
        await using var context = CreateContext();
        var expense = await SeedExpenseAsync(context);
        var controller = CreateController(context, Guid.NewGuid());

        Assert.IsType<NotFoundResult>(await controller.Delete(expense.Id, default));

        Assert.Single(context.Expenses);
        Assert.Null(controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task Delete_MissingExpenseReturnsNotFound()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, Guid.NewGuid());

        Assert.IsType<NotFoundResult>(await controller.Delete(Guid.NewGuid(), default));
        Assert.Null(controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task Delete_WithoutUserIdChallengesWithoutDeleting()
    {
        await using var context = CreateContext();
        var expense = await SeedExpenseAsync(context);
        var controller = CreateController(context);

        Assert.IsType<ChallengeResult>(await controller.Delete(expense.Id, default));
        Assert.Single(context.Expenses);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Expense> SeedExpenseAsync(ApplicationDbContext context)
    {
        var expense = new Expense
        {
            UserId = Guid.NewGuid(), CategoryId = Guid.NewGuid(),
            Amount = 10m, Description = "Lunch", ExpenseDate = new DateTime(2026, 10, 22)
        };
        context.Expenses.Add(expense);
        await context.SaveChangesAsync();
        return expense;
    }

    private static ExpenseController CreateController(ApplicationDbContext context, Guid? userId = null)
    {
        var httpContext = new DefaultHttpContext();
        if (userId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "Test"));
        }
        return new ExpenseController(new ExpenseService(context), null!)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
