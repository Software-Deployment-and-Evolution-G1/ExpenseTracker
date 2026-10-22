using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Models;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
