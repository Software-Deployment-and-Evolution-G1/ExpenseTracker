using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace ExpenseTracker.Controllers;

public class NewExpenseController : Controller
{
    private readonly ExpenseService _expenseService;
    private readonly UserManager<ApplicationUser> _userManager;
    public NewExpenseController(ExpenseService expenseService, UserManager<ApplicationUser> userManager)
    {
        _expenseService = expenseService;
        _userManager = userManager;
    }
    public IActionResult Index()
    {
        var categories = _expenseService.GetCategoriesAsync();
        ViewBag.Add(categories);
        return View();
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExpense(ExpenseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var result = await _expenseService.CreateExpenseAsync(currentUser.Id, model, cancellationToken);
        if (result.Status == ExpenseMutationStatus.InvalidCategory)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Select a valid category.");
            return BadRequest(ModelState);
        }

        return CreatedAtAction(
            actionName: nameof(ExpenseController.RetrieveExpenses),
            controllerName: "Expense",
            routeValues: null,
            value: result.Expense);
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}