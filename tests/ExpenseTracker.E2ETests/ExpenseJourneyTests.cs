using ExpenseTracker.Testing;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2ETests;

public sealed class BrowserFixture : AppFixture
{
    protected override bool UseBrowserServer => true;
}

public sealed class ExpenseJourneyTests(BrowserFixture fixture) : IClassFixture<BrowserFixture>
{
    private const string Password = "StrongTestPass1";

    [Fact]
    public Task RegisterCreateEditAndDelete_UpdatesListAndTotal() => WithBrowser("expense-journey", async page =>
    {
        await Register(page);
        await CreateExpense(page, "Browser lunch", "12.50");

        await page.GetByRole(AriaRole.Link, new() { Name = "Edit expense: Browser lunch", Exact = true }).ClickAsync();
        await page.GetByLabel("Amount", new() { Exact = true }).FillAsync("25.75");
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Updated browser lunch");
        await page.GetByRole(AriaRole.Button, new() { Name = "Update", Exact = true }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Expense updated.");
        await Expect(page.Locator(".card").Filter(new() { HasText = "Total spending" })).ToContainTextAsync("25.75");

        await page.ReloadAsync();

        await Expect(page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Updated browser lunch" }))
            .ToContainTextAsync("25.75");

        await page.GetByRole(AriaRole.Button, new() { Name = "Delete expense: Updated browser lunch", Exact = true })
            .ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);

        await Expect(dialog).ToContainTextAsync("Updated browser lunch");

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete expense", Exact = true }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "No expenses yet" })).ToBeVisibleAsync();
        await Expect(page.Locator(".card").Filter(new() { HasText = "Total spending" })).ToContainTextAsync("0.00");
    });

    [Fact]
    public Task DeleteModal_CancelPreservesExpense_AndConfirmDeletesSelectedRow() => WithBrowser("delete-modal", async page =>
    {
        await Register(page);
        await CreateExpense(page, "Keep this expense", "10.00");
        await CreateExpense(page, "Delete this expense", "20.00");

        await page.GetByRole(AriaRole.Button, new() { Name = "Delete expense: Keep this expense", Exact = true })
            .ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog);

        await Expect(dialog).ToContainTextAsync("Keep this expense");
        await Expect(dialog).ToContainTextAsync("10.00");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true })).ToBeFocusedAsync();

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

        await Expect(dialog).ToBeHiddenAsync();

        await page.ReloadAsync();

        await Expect(page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Keep this expense" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Delete expense: Delete this expense", Exact = true })
            .ClickAsync();

        await Expect(dialog).ToContainTextAsync("Delete this expense");
        await Expect(dialog).ToContainTextAsync("20.00");

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete expense", Exact = true }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Delete this expense" }))
            .ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Row).Filter(new() { HasText = "Keep this expense" })).ToBeVisibleAsync();
    });

    [Fact]
    public Task LogoutLoginAndValidation_ShowUsefulFeedback() => WithBrowser("authentication-validation", async page =>
    {
        var email = await Register(page);

        await page.GetByRole(AriaRole.Link, new() { Name = "Create expense", Exact = true }).ClickAsync();
        await page.GetByLabel("Date", new() { Exact = true }).FillAsync("2026-10-01");
        await page.GetByLabel("Category", new() { Exact = true })
            .SelectOptionAsync(new SelectOptionValue { Label = "Food" });
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Invalid expense");
        await page.GetByLabel("Amount", new() { Exact = true }).FillAsync("-1");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save expense", Exact = true }).ClickAsync();

        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/NewExpense"));
        await Expect(page.Locator("[data-valmsg-for='Amount'].field-validation-error")).ToContainTextAsync("0.01");

        await page.GetByRole(AriaRole.Button, new() { Name = "Log out", Exact = true }).ClickAsync();
        await page.GotoAsync("/NewExpense");

        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/Authentication/Login"));

        await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("WrongPassword1");
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Invalid email or password.");

        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Add an expense" })).ToBeVisibleAsync();
    });

    private async Task WithBrowser(string name, Func<IPage, Task> test)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        using var client = fixture.CreateClient();
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = client.BaseAddress!.ToString(),
            Locale = "en-AU"
        });

        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();

        page.Console += (_, message) => File.AppendAllText(
            Path.Combine(fixture.ArtifactDirectory, $"{name}-browser.log"),
            $"{message.Type}: {message.Text}\n");
        page.PageError += (_, error) => File.AppendAllText(
            Path.Combine(fixture.ArtifactDirectory, $"{name}-browser.log"),
            $"ERROR: {error}\n");

        try
        {
            await test(page);
            await context.Tracing.StopAsync();
        }
        catch
        {
            try
            {
                await page.ScreenshotAsync(new()
                {
                    Path = Path.Combine(fixture.ArtifactDirectory, $"{name}.png"),
                    FullPage = true
                });
            }
            catch
            {
            }

            try
            {
                await context.Tracing.StopAsync(new()
                {
                    Path = Path.Combine(fixture.ArtifactDirectory, $"{name}-trace.zip")
                });
            }
            catch
            {
            }

            throw;
        }
    }

    private static async Task<string> Register(IPage page)
    {
        var email = $"browser-{Guid.NewGuid():N}@example.com";
        await page.GotoAsync("/Authentication/Register");
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(Password);
        await page.GetByLabel("Confirm Password", new() { Exact = true }).FillAsync(Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign up", Exact = true }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "No expenses yet" })).ToBeVisibleAsync();

        return email;
    }

    private static async Task CreateExpense(IPage page, string description, string amount)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Create expense", Exact = true }).ClickAsync();
        await page.GetByLabel("Date", new() { Exact = true }).FillAsync("2026-10-01");
        await page.GetByLabel("Category", new() { Exact = true })
            .SelectOptionAsync(new SelectOptionValue { Label = "Food" });
        await page.GetByLabel("Amount", new() { Exact = true }).FillAsync(amount);
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync(description);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save expense", Exact = true }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Expense saved.");
        await Expect(page.GetByRole(AriaRole.Row).Filter(new() { HasText = description })).ToBeVisibleAsync();
    }
}
