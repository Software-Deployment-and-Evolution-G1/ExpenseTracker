using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Models;
using Xunit;

namespace ExpenseTracker.Tests;

public sealed class ViewModelValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000000000)]
    public void ExpenseViewModel_RejectsAmountsOutsideSupportedRange(decimal amount)
    {
        var model = ValidExpenseModel();
        model.Amount = amount;

        var errors = Validate(model);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.Amount)));
    }

    [Fact]
    public void ExpenseViewModel_RejectsMissingCategoryDescriptionAndDate()
    {
        var model = ValidExpenseModel();
        model.CategoryId = null;
        model.Description = string.Empty;
        model.ExpenseDate = null;

        var errors = Validate(model);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.CategoryId)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.Description)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.ExpenseDate)));
    }

    [Fact]
    public void RegisterViewModel_RejectsMismatchedPasswords()
    {
        var model = new RegisterViewModel
        {
            Email = "person@example.com",
            Password = "StrongPass1",
            ConfirmPassword = "DifferentPass1"
        };

        var errors = Validate(model);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.ConfirmPassword)));
    }

    [Fact]
    public void RegisterViewModel_RejectsInvalidEmail()
    {
        var model = new RegisterViewModel
        {
            Email = "not-an-email",
            Password = "StrongPass1",
            ConfirmPassword = "StrongPass1"
        };

        var errors = Validate(model);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.Email)));
    }

    private static ExpenseViewModel ValidExpenseModel()
    {
        return new ExpenseViewModel
        {
            Amount = 20m,
            CategoryId = Guid.NewGuid(),
            Description = "Dinner",
            ExpenseDate = new DateTime(2026, 1, 1)
        };
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
