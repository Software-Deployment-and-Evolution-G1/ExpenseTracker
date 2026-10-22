using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class ExpenseServiceTests
{
    [Fact]
    public async Task GetExpensesAsync_ReturnsOnlyExpensesForRequestedUser()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        context.Expenses.AddRange(
            CreateExpense(userId),
            CreateExpense(otherUserId));
        await context.SaveChangesAsync();

        var results = await new ExpenseService(context).GetExpensesAsync(userId);

        Assert.Single(results);
        Assert.All(results, expense => Assert.Equal(userId, expense.UserId));
    }

    [Fact]
    public async Task GetCategoriesAsync_ProjectsCategoryProperties()
    {
        await using var context = CreateContext();
        var category = new Category { Name = "Food", Description = "Meals" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var results = await new ExpenseService(context).GetCategoriesAsync();

        var result = Assert.Single(results);
        Assert.Equal(category.Id, result.Id);
        Assert.Equal(category.Name, result.Name);
        Assert.Equal(category.Description, result.Description);
    }

    [Fact]
    public async Task CreateExpenseAsync_StoresExpenseForUserWithCategory()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var category = new Category { Name = "Food" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var result = await new ExpenseService(context).CreateExpenseAsync(userId, ValidModel(category.Id));

        Assert.Equal(ExpenseMutationStatus.Succeeded, result.Status);
        Assert.NotNull(result.Expense);
        Assert.Equal(userId, result.Expense.UserId);
        Assert.Equal(category.Id, result.Expense.CategoryId);
        Assert.Equal(12.50m, result.Expense.Amount);
        Assert.Single(context.Expenses);
    }

    [Fact]
    public async Task CreateExpenseAsync_RejectsMissingCategoryWithoutSaving()
    {
        await using var context = CreateContext();

        var result = await new ExpenseService(context).CreateExpenseAsync(Guid.NewGuid(), ValidModel(Guid.NewGuid()));

        Assert.Equal(ExpenseMutationStatus.InvalidCategory, result.Status);
        Assert.Empty(context.Expenses);
    }

    [Fact]
    public async Task UpdateExpenseAsync_ChangesOwnedExpense()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var category = new Category { Name = "Food" };
        var expense = CreateExpense(userId);
        context.AddRange(category, expense);
        await context.SaveChangesAsync();
        var model = ValidModel(category.Id);
        model.Id = expense.Id;
        model.Amount = 24.75m;
        model.Description = "Updated";

        var result = await new ExpenseService(context).UpdateExpenseAsync(userId, model);

        Assert.Equal(ExpenseMutationStatus.Succeeded, result.Status);
        Assert.Equal(24.75m, expense.Amount);
        Assert.Equal("Updated", expense.Description);
        Assert.Equal(category.Id, expense.CategoryId);
    }

    [Fact]
    public async Task UpdateExpenseAsync_DoesNotFindAnotherUsersExpense()
    {
        await using var context = CreateContext();
        var expense = CreateExpense(Guid.NewGuid());
        context.Expenses.Add(expense);
        await context.SaveChangesAsync();
        var model = ValidModel(Guid.NewGuid());
        model.Id = expense.Id;

        var result = await new ExpenseService(context).UpdateExpenseAsync(Guid.NewGuid(), model);

        Assert.Equal(ExpenseMutationStatus.NotFound, result.Status);
        Assert.Equal(10m, expense.Amount);
    }

    [Fact]
    public async Task UpdateExpenseAsync_RejectsInvalidCategoryWithoutChangingExpense()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var expense = CreateExpense(userId);
        context.Expenses.Add(expense);
        await context.SaveChangesAsync();
        var model = ValidModel(Guid.NewGuid());
        model.Id = expense.Id;
        model.Amount = 99m;

        var result = await new ExpenseService(context).UpdateExpenseAsync(userId, model);

        Assert.Equal(ExpenseMutationStatus.InvalidCategory, result.Status);
        Assert.Equal(10m, expense.Amount);
    }

    [Fact]
    public async Task DeleteExpenseAsync_RemovesOwnedExpense()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var expense = CreateExpense(userId);
        context.Expenses.Add(expense);
        await context.SaveChangesAsync();

        var result = await new ExpenseService(context).DeleteExpenseAsync(userId, expense.Id);

        Assert.Equal(ExpenseMutationStatus.Succeeded, result.Status);
        Assert.Empty(context.Expenses);
    }

    [Fact]
    public async Task DeleteExpenseAsync_DoesNotRemoveAnotherUsersExpense()
    {
        await using var context = CreateContext();
        var expense = CreateExpense(Guid.NewGuid());
        context.Expenses.Add(expense);
        await context.SaveChangesAsync();

        var result = await new ExpenseService(context).DeleteExpenseAsync(Guid.NewGuid(), expense.Id);

        Assert.Equal(ExpenseMutationStatus.NotFound, result.Status);
        Assert.Single(context.Expenses);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Expense CreateExpense(Guid userId)
    {
        return new Expense
        {
            UserId = userId,
            CategoryId = Guid.NewGuid(),
            Amount = 10m,
            Description = "Original",
            ExpenseDate = new DateTime(2026, 1, 1)
        };
    }

    private static ExpenseViewModel ValidModel(Guid categoryId)
    {
        return new ExpenseViewModel
        {
            Amount = 12.50m,
            CategoryId = categoryId,
            Description = "Lunch",
            ExpenseDate = new DateTime(2026, 2, 1)
        };
    }
}
