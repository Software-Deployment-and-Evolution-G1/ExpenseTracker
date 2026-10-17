using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
