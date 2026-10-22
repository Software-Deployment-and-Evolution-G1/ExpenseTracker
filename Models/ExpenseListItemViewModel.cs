namespace ExpenseTracker.Models;

public sealed class ExpenseListItemViewModel
{
    public DateTime ExpenseDate { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
