using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace ExpenseTracker.Controllers;

public class ExpenseController : Controller
{
    private readonly ExpenseService _expenseService;
    private readonly UserManager<ApplicationUser> _userManager;
    public ExpenseController(ExpenseService expenseService, UserManager<ApplicationUser> userManager)
    {
        _expenseService = expenseService;
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

        var expenses = await _expenseService.GetExpensesAsync(currentUser.Id, cancellationToken);
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

        var result = await _expenseService.UpdateExpenseAsync(currentUser.Id, model, cancellationToken);
        if (result.Status == ExpenseMutationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == ExpenseMutationStatus.InvalidCategory)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Select a valid category.");
            return BadRequest(ModelState);
        }

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

        var result = await _expenseService.DeleteExpenseAsync(currentUser.Id, id, cancellationToken);
        if (result.Status == ExpenseMutationStatus.NotFound)
        {
            return NotFound();
        }

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

        var categoryViewModels = await _expenseService.GetCategoriesAsync(cancellationToken);
        return categoryViewModels;
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}