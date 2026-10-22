using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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
        var expenses = RetrieveExpenses(CancellationToken.None);
        ViewBag.expenses = expenses;
        return View();
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Challenge();
        }

        var expense = await _expenseService.GetExpenseAsync(userId, id, cancellationToken);
        if (expense is null)
        {
            return NotFound();
        }

        return await EditFormAsync(new ExpenseViewModel
        {
            Id = expense.Id,
            ExpenseDate = expense.ExpenseDate,
            CategoryId = expense.CategoryId,
            Amount = expense.Amount,
            Description = expense.Description
        }, cancellationToken);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ExpenseViewModel model, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Challenge();
        }

        if (await _expenseService.GetExpenseAsync(userId, model.Id, cancellationToken) is null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var result = await _expenseService.UpdateExpenseAsync(userId, model, cancellationToken);
            if (result.Status == ExpenseMutationStatus.NotFound)
            {
                return NotFound();
            }

            if (result.Status == ExpenseMutationStatus.Succeeded)
            {
                TempData["SuccessMessage"] = "Expense updated.";
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(nameof(model.CategoryId), "Select a valid category.");
        }

        return await EditFormAsync(model, cancellationToken);
    }

    private async Task<IActionResult> EditFormAsync(ExpenseViewModel model, CancellationToken cancellationToken)
    {
        ViewBag.Categories = await _expenseService.GetCategoriesAsync(cancellationToken);
        return View("Edit", model);
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
