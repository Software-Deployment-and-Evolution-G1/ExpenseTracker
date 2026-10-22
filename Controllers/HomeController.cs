using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ExpenseTracker.Controllers;

public class HomeController : Controller
{
    private readonly ExpenseService _expenseService;

    public HomeController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return View();
        }

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Challenge();
        }

        return View(await _expenseService.GetExpenseListAsync(userId, cancellationToken));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExpense(ExpenseViewModel model, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Challenge();
        }

        if (ModelState.IsValid)
        {
            var result = await _expenseService.CreateExpenseAsync(userId, model, cancellationToken);
            if (result.Status == ExpenseMutationStatus.Succeeded)
            {
                TempData["SuccessMessage"] = "Expense saved.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(nameof(model.CategoryId), "Select a valid category.");
        }

        return await ExpenseFormAsync(model, cancellationToken);
    }

    private async Task<IActionResult> ExpenseFormAsync(ExpenseViewModel model, CancellationToken cancellationToken)
    {
        ViewBag.Categories = await _expenseService.GetCategoriesAsync(cancellationToken);
        return View("~/Views/NewExpense/Index.cshtml", model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
