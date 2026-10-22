using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Models;
using ExpenseTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace ExpenseTracker.Controllers;

public class NewExpenseController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    public NewExpenseController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
    public IActionResult Index()
    {
        var categories = (from category in _context.Categories select category).ToList();
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

        var categoryExists = await _context.Categories
            .AnyAsync(category => category.Id == model.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Select a valid category.");
            return BadRequest(ModelState);
        }

        var expense = new Expense
        {
            UserId = currentUser.Id,
            CategoryId = model.CategoryId!.Value,
            Amount = model.Amount,
            Description = model.Description,
            ExpenseDate = model.ExpenseDate!.Value,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            actionName: nameof(ExpenseController.RetrieveExpenses),
            controllerName: "Expense",
            routeValues: null,
            value: expense);
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}