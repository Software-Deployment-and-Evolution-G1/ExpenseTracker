using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ExpenseTracker.Models;
using ExpenseTracker.Data;

namespace ExpenseTracker.Controllers;

public class ExpenseController : Controller
{
    private readonly ApplicationDbContext _context;
    public ExpenseController(ApplicationDbContext context)
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

    public IActionResult DeleteExpense(Expense expense)
    {
        using (_context){
     
        _context.Remove(expense);
        }
        return Index();
    }

    public IActionResult EditExpense(Expense expense, Category category, decimal amount, string description)
    {
        using (_context)
        {
            expense.CategoryId = category.Id;
            expense.Category = category;
            expense.Amount = amount;
            expense.Description = description;
            expense.UpdatedAt = DateTimeOffset.UtcNow;

            _context.Update(expense);
        }
        return Index();
    }

    public List<Expense> RetrieveExpenses()
    {
        using (_context)
        {
            var result = new List<Expense>();
            foreach (Expense e in _context.Expenses)
            {
                result.Add(e);
            }
            return result;
        }   
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}