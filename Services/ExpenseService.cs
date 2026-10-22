using ExpenseTracker.Data;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services;

public enum ExpenseMutationStatus
{
    Succeeded,
    NotFound,
    InvalidCategory
}

public sealed record ExpenseMutationResult(ExpenseMutationStatus Status, Expense? Expense = null);

public sealed class ExpenseService
{
    private readonly ApplicationDbContext _context;

    public ExpenseService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Expense>> GetExpensesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Expenses
            .Where(expense => expense.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ExpenseListItemViewModel>> GetExpenseListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Expenses
            .AsNoTracking()
            .Where(expense => expense.UserId == userId)
            .OrderByDescending(expense => expense.ExpenseDate)
            .ThenByDescending(expense => expense.CreatedAt)
            .Select(expense => new ExpenseListItemViewModel
            {
                ExpenseDate = expense.ExpenseDate,
                CategoryName = expense.Category.Name,
                Description = expense.Description,
                Amount = expense.Amount
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CategoryViewModel>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .Select(category => new CategoryViewModel
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ExpenseMutationResult> CreateExpenseAsync(
        Guid userId,
        ExpenseViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!await CategoryExistsAsync(model.CategoryId, cancellationToken))
        {
            return new ExpenseMutationResult(ExpenseMutationStatus.InvalidCategory);
        }

        var expense = new Expense
        {
            UserId = userId,
            CategoryId = model.CategoryId!.Value,
            Amount = model.Amount,
            Description = model.Description,
            ExpenseDate = model.ExpenseDate!.Value,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync(cancellationToken);

        return new ExpenseMutationResult(ExpenseMutationStatus.Succeeded, expense);
    }

    public async Task<ExpenseMutationResult> UpdateExpenseAsync(
        Guid userId,
        ExpenseViewModel model,
        CancellationToken cancellationToken = default)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(item => item.Id == model.Id && item.UserId == userId, cancellationToken);
        if (expense is null)
        {
            return new ExpenseMutationResult(ExpenseMutationStatus.NotFound);
        }

        if (!await CategoryExistsAsync(model.CategoryId, cancellationToken))
        {
            return new ExpenseMutationResult(ExpenseMutationStatus.InvalidCategory);
        }

        expense.CategoryId = model.CategoryId!.Value;
        expense.Amount = model.Amount;
        expense.Description = model.Description;
        expense.ExpenseDate = model.ExpenseDate!.Value;
        expense.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return new ExpenseMutationResult(ExpenseMutationStatus.Succeeded, expense);
    }

    public async Task<ExpenseMutationResult> DeleteExpenseAsync(
        Guid userId,
        Guid expenseId,
        CancellationToken cancellationToken = default)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(item => item.Id == expenseId && item.UserId == userId, cancellationToken);
        if (expense is null)
        {
            return new ExpenseMutationResult(ExpenseMutationStatus.NotFound);
        }

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync(cancellationToken);
        return new ExpenseMutationResult(ExpenseMutationStatus.Succeeded, expense);
    }

    private Task<bool> CategoryExistsAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        return categoryId.HasValue && categoryId.Value != Guid.Empty
            ? _context.Categories.AnyAsync(category => category.Id == categoryId.Value, cancellationToken)
            : Task.FromResult(false);
    }
}
