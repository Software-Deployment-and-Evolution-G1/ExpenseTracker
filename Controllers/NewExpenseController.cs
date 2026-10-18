using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Models;
using ExpenseTracker.Data;

namespace ExpenseTracker.Controllers;

public class NewExpenseController : Controller
{
    private readonly ApplicationDbContext _context;
    public NewExpenseController(ApplicationDbContext context)
    {
        _context = context;
    }
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult AddExpense(ApplicationUser user, Category category, decimal amount, string description, DateTime expenseDate)
    {
        using (_context){
        var expense = new Expense{
            User = user,
            CategoryId = category.Id,
            Category = category,
            Amount = amount,
            Description = description,
            ExpenseDate = expenseDate
        };
        _context.Add(expense);
        }
        return Index();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}