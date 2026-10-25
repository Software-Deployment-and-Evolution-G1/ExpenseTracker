using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ExpenseTracker.Testing;
using Xunit;

namespace ExpenseTracker.IntegrationTests;

internal sealed class BrowserSession(AppFixture fixture) : IDisposable
{
    public const string Password = "StrongTestPass1";

    public HttpClient Client { get; } = fixture.CreateClient();
    public string Email { get; } = $"test-{Guid.NewGuid():N}@example.com";

    public async Task<IDocument> GetPage(string path)
    {
        using var response = await Client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        return await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync());
    }

    public async Task<HttpResponseMessage> Post(
        string endpoint,
        string formPage,
        Dictionary<string, string> fields)
    {
        var page = await GetPage(formPage);
        fields["__RequestVerificationToken"] = page.QuerySelector("input[name='__RequestVerificationToken']")!
            .GetAttribute("value")!;

        return await Client.PostAsync(endpoint, new FormUrlEncodedContent(fields));
    }

    public async Task Register()
    {
        using var response = await Post("/Authentication/Register", "/Authentication/Register", new()
        {
            ["Email"] = Email,
            ["Password"] = Password,
            ["ConfirmPassword"] = Password
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    public async Task<Guid> CategoryId()
    {
        var page = await GetPage("/NewExpense");
        return Guid.Parse(page.QuerySelector("select[name='CategoryId'] option[value]:not([value=''])")!
            .GetAttribute("value")!);
    }

    public static Dictionary<string, string> Expense(
        Guid category,
        string description = "Integration lunch",
        string amount = "12.50",
        string date = "2026-10-01",
        Guid? id = null) => new()
    {
        ["CategoryId"] = category.ToString(),
        ["Description"] = description,
        ["Amount"] = amount,
        ["ExpenseDate"] = date,
        ["Id"] = (id ?? Guid.Empty).ToString()
    };

    public async Task<Guid> CreateExpense(
        Guid category,
        string description = "Integration lunch",
        string amount = "12.50",
        string date = "2026-10-01")
    {
        using var response = await Post("/Home/CreateExpense", "/NewExpense", Expense(category, description, amount, date));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var page = await GetPage("/");
        var row = page.QuerySelectorAll("tbody tr").Single(r => r.TextContent.Contains(description));
        return Guid.Parse(row.QuerySelector("button[data-expense-id]")!.GetAttribute("data-expense-id")!);
    }

    public void Dispose() => Client.Dispose();
}
