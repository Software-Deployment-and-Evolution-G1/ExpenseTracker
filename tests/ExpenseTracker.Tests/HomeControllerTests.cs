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

public sealed class HomeControllerTests
{
    [Fact]
    public async Task AnonymousHome_ShowsWelcomeWithoutLoadingCategories()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var result = Assert.IsType<ViewResult>(await controller.Index(default));

        Assert.Null(result.ViewName);
        Assert.Null(result.Model);
        Assert.Null(controller.ViewData["Categories"]);
        Assert.Null(controller.ViewData["TotalSpending"]);
    }

    [Fact]
    public async Task SignedInHome_ListsOnlyOwnedExpensesNewestFirstWithCategory()
    {
        await using var context = CreateContext();
        var category = new Category { Name = "Food" };
        var userId = Guid.NewGuid();
        context.Categories.Add(category);
        context.Expenses.AddRange(
            new Expense { UserId = userId, CategoryId = category.Id, Amount = 12.50m, Description = "Older", ExpenseDate = new DateTime(2026, 10, 1) },
            new Expense { UserId = userId, CategoryId = category.Id, Amount = 20.75m, Description = "Newer", ExpenseDate = new DateTime(2026, 10, 2) },
            new Expense { UserId = Guid.NewGuid(), CategoryId = category.Id, Amount = 30, Description = "Private", ExpenseDate = new DateTime(2026, 10, 3) });
        await context.SaveChangesAsync();
        var controller = CreateController(context, userId);

        var result = Assert.IsType<ViewResult>(await controller.Index(default));

        var expenses = Assert.IsType<List<ExpenseListItemViewModel>>(result.Model);
        Assert.Equal(new[] { "Newer", "Older" }, expenses.Select(expense => expense.Description));
        Assert.All(expenses, expense => Assert.Equal("Food", expense.CategoryName));
        Assert.Equal(20.75m, expenses[0].Amount);
        Assert.Equal(33.25m, controller.ViewData["TotalSpending"]);
    }

    [Fact]
    public async Task SignedInHomeWithoutExpenses_ReturnsEmptyList()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, Guid.NewGuid());

        var result = Assert.IsType<ViewResult>(await controller.Index(default));

        Assert.Empty(Assert.IsType<List<ExpenseListItemViewModel>>(result.Model));
        Assert.Equal(0m, controller.ViewData["TotalSpending"]);
    }

    [Fact]
    public async Task NewExpensePage_LoadsFormWithCategories()
    {
        await using var context = CreateContext();
        context.Categories.Add(new Category { Name = "Food" });
        await context.SaveChangesAsync();
        var controller = new NewExpenseController(new ExpenseService(context), null!);

        var result = Assert.IsType<ViewResult>(await controller.Index(default));

        Assert.IsType<ExpenseViewModel>(result.Model);
        Assert.Equal("Food", Assert.Single(Assert.IsType<List<CategoryViewModel>>(controller.ViewData["Categories"])).Name);
    }

    [Fact]
    public async Task ValidSubmission_SavesForSignedInUserAndRedirectsWithConfirmation()
    {
        await using var context = CreateContext();
        var category = new Category { Name = "Food" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var userId = Guid.NewGuid();
        var controller = CreateController(context, userId);

        var result = Assert.IsType<RedirectToActionResult>(await controller.CreateExpense(ValidModel(category.Id), default));

        Assert.Equal("Index", result.ActionName);
        Assert.Equal(userId, Assert.Single(context.Expenses).UserId);
        Assert.NotNull(controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task InvalidSubmission_PreservesInputAndReloadsCategories()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, Guid.NewGuid());
        var model = ValidModel(Guid.NewGuid());
        controller.ModelState.AddModelError("Amount", "Enter a valid amount.");

        var result = Assert.IsType<ViewResult>(await controller.CreateExpense(model, default));

        Assert.Same(model, result.Model);
        Assert.NotNull(controller.ViewData["Categories"]);
        Assert.Empty(context.Expenses);
    }

    [Fact]
    public async Task UnknownCategory_ShowsFieldErrorWithoutSaving()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, Guid.NewGuid());

        Assert.IsType<ViewResult>(await controller.CreateExpense(ValidModel(Guid.NewGuid()), default));

        Assert.Single(controller.ModelState["CategoryId"]!.Errors);
        Assert.Empty(context.Expenses);
    }

    [Fact]
    public async Task SubmissionWithoutUserId_ChallengesWithoutSaving()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        Assert.IsType<ChallengeResult>(await controller.CreateExpense(ValidModel(Guid.NewGuid()), default));
        Assert.Empty(context.Expenses);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static HomeController CreateController(ApplicationDbContext context, Guid? userId = null)
    {
        var httpContext = new DefaultHttpContext();
        if (userId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "Test"));
        }

        return new HomeController(new ExpenseService(context))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    private static ExpenseViewModel ValidModel(Guid categoryId) => new()
    {
        Amount = 12.50m,
        CategoryId = categoryId,
        Description = "Lunch",
        ExpenseDate = new DateTime(2026, 10, 22)
    };

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
