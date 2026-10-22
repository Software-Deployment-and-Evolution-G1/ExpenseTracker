using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Models;

public class ExpenseViewModel
{
	public Guid Id { get; set; }

	[Required]
	[Range(typeof(decimal), "0.01", "9999999999.99")]
	public decimal Amount { get; set; }

	[Required]
	public Guid? CategoryId { get; set; }

	[Required]
	[StringLength(500)]
	public string Description { get; set; } = string.Empty;

	[Required]
	[DataType(DataType.Date)]
	public DateTime? ExpenseDate { get; set; }
}
