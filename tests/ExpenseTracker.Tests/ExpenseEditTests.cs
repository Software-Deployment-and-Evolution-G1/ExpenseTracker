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

public sealed class ExpenseEditTests
{
    [Fact]
    public async Task Edit_LoadsOwnedExpenseAndCategories()
    {
        await using var context = CreateContext();
        var expense = await SeedExpenseAsync(context);
        var controller = CreateController(context, expense.UserId);

        var result = Assert.IsType<ViewResult>(await controller.Edit(expense.Id, default));

        var model = Assert.IsType<ExpenseViewModel>(result.Model);
        Assert.Equal(expense.Id, model.Id);
        Assert.Equal(expense.CategoryId, model.CategoryId);
        Assert.Equal(expense.Amount, model.Amount);
        Assert.Equal(expense.Description, model.Description);
        Assert.Equal(expense.ExpenseDate, model.ExpenseDate);
        Assert.Equal("Food", Assert.Single(Assert.IsType<List<CategoryViewModel>>(controller.ViewData["Categories"])).Name);
        var list = await new ExpenseService(context).GetExpenseListAsync(expense.UserId);
        Assert.Equal(expense.Id, Assert.Single(list).Id);
    }

    [Fact]
    public async Task Edit_RejectsReadingAndChangingAnotherUsersExpense()
    {
        await using var context = CreateContext();
        var expense = await SeedExpenseAsync(context);
        var controller = CreateController(context, Guid.NewGuid());

        Assert.IsType<NotFoundResult>(await controller.Edit(expense.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Edit(ModelFor(expense), default));

        controller.ModelState.AddModelError("Amount", "Enter a valid amount.");
        Assert.IsType<NotFoundResult>(await controller.Edit(ModelFor(expense), default));
        Assert.Equal(10m, expense.Amount);
    }

    [Fact]
    public async Task Edit_ReturnsNotFoundForMissingExpense()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, Guid.NewGuid());

        Assert.IsType<NotFoundResult>(await controller.Edit(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Edit_SavesChangesAndReturnsToHomeWithConfirmation()
    {
        await using var context = CreateContext();
        var expense = await SeedExpenseAsync(context);
        var controller = CreateController(context, expense.UserId);
        var model = ModelFor(expense);
        model.Amount = 25.50m;
        model.Description = "Updated lunch";

        var result = Assert.IsType<RedirectToActionResult>(await controller.Edit(model, default));

        Assert.Equal("Home", result.ControllerName);
        Assert.Equal("Index", result.ActionName);
        Assert.Equal("Expense updated.", controller.TempData["SuccessMessage"]);
        Assert.Equal(25.50m, expense.Amount);
        Assert.Equal("Updated lunch", expense.Description);
    }

    [Fact]
    public async Task Edit_InvalidInputPreservesValuesAndReloadsCategories()
    {
        await using var context = CreateContext();
        var expense = await SeedExpenseAsync(context);
        var controller = CreateController(context, expense.UserId);
        var model = ModelFor(expense);
        model.Amount = -1;
        controller.ModelState.AddModelError("Amount", "Enter a valid amount.");

        var result = Assert.IsType<ViewResult>(await controller.Edit(model, default));

        Assert.Same(model, result.Model);
        Assert.NotNull(controller.ViewData["Categories"]);
        Assert.Equal(10m, expense.Amount);
    }

    [Fact]
    public async Task Edit_InvalidCategoryShowsFieldErrorWithoutSaving()
    {
        await using var context = CreateContext();
        var expense = await SeedExpenseAsync(context);
        var controller = CreateController(context, expense.UserId);
        var model = ModelFor(expense);
        model.CategoryId = Guid.NewGuid();
        model.Amount = 50;

        Assert.IsType<ViewResult>(await controller.Edit(model, default));

        Assert.Single(controller.ModelState["CategoryId"]!.Errors);
        Assert.Equal(10m, expense.Amount);
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Expense> SeedExpenseAsync(ApplicationDbContext context)
    {
        var category = new Category { Name = "Food" };
        var expense = new Expense
        {
            UserId = Guid.NewGuid(), CategoryId = category.Id,
            Amount = 10m, Description = "Lunch", ExpenseDate = new DateTime(2026, 10, 22)
        };
        context.AddRange(category, expense);
        await context.SaveChangesAsync();
        return expense;
    }

    private static ExpenseViewModel ModelFor(Expense expense) => new()
    {
        Id = expense.Id, CategoryId = expense.CategoryId, Amount = expense.Amount,
        Description = expense.Description, ExpenseDate = expense.ExpenseDate
    };

    private static ExpenseController CreateController(ApplicationDbContext context, Guid userId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "Test"))
        };
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
