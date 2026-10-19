using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Models;
using ExpenseTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace ExpenseTracker.Controllers;

public class ExpenseController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    public ExpenseController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<Expense>>> RetrieveExpenses(CancellationToken cancellationToken)
    {
        var currentUser = await _userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var expenses = await _context.Expenses
            .Where(expense => expense.UserId == currentUser.Id)
            .ToListAsync(cancellationToken);

        return expenses;
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateExpense(
        ExpenseViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (model.Id == Guid.Empty)
        {
            ModelState.AddModelError(nameof(model.Id), "An expense ID is required to update an expense.");
            return BadRequest(ModelState);
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var expense = await _context.Expenses
            .FirstOrDefaultAsync(item => item.Id == model.Id && item.UserId == currentUser.Id, cancellationToken);
        if (expense is null)
        {
            return NotFound();
        }

        var categoryExists = await _context.Categories
            .AnyAsync(category => category.Id == model.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Select a valid category.");
            return BadRequest(ModelState);
        }

        expense.CategoryId = model.CategoryId!.Value;
        expense.Amount = model.Amount;
        expense.Description = model.Description;
        expense.ExpenseDate = model.ExpenseDate!.Value;
        expense.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var expense = await _context.Expenses
            .FirstOrDefaultAsync(item => item.Id == id && item.UserId == currentUser.Id, cancellationToken);
        if (expense is null)
        {
            return NotFound();
        }

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryViewModel>>> RetrieveCategories(CancellationToken cancellationToken)
    {
        var currentUser = await _userManager.GetUserAsync(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var categories = await _context.Categories
            .ToListAsync(cancellationToken);

        var categoryViewModels = categories.Select(c => new CategoryViewModel
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description
        }).ToList();

        return categoryViewModels;
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}