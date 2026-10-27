using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Data;
using ExpenseTracker.Models;
using ExpenseTracker.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.IntegrationTests;

public sealed class ApplicationTests(AppFixture fixture) : IClassFixture<AppFixture>
{
    [Fact]
    public async Task Startup_AppliesMigrations_AndSeedingIsIdempotent()
    {
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        var before = await db.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();

        Assert.Equal(6, before.Count);

        await SeedData.InitializeAsync(scope.ServiceProvider);

        Assert.Equal(before, await db.Categories.OrderBy(c => c.Name).Select(c => c.Name).ToListAsync());
    }

    [Theory]
    [InlineData("/NewExpense")]
    [InlineData("/Expense/Edit/00000000-0000-0000-0000-000000000001")]
    [InlineData("/Expense/RetrieveExpenses")]
    [InlineData("/Expense/RetrieveCategories")]
    public async Task ProtectedRoutes_ChallengeAnonymousRequests(string path)
    {
        using var client = fixture.CreateClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Authentication/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Authentication_Registers_RejectsBadPassword_LogsIn_AndLogsOut()
    {
        using var session = new BrowserSession(fixture);
        await session.Register();

        Assert.Contains("No expenses yet", (await session.GetPage("/")).Body!.TextContent);

        using var logout = await session.Post("/Authentication/Logout", "/", new());

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        using var bad = await session.Post("/Authentication/Login", "/Authentication/Login", new()
        {
            ["Email"] = session.Email,
            ["Password"] = "WrongPassword1"
        });

        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Contains("Invalid email or password.", await bad.Content.ReadAsStringAsync());

        using var login = await session.Post("/Authentication/Login", "/Authentication/Login", new()
        {
            ["Email"] = session.Email,
            ["Password"] = BrowserSession.Password,
            ["ReturnUrl"] = "/NewExpense"
        });

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/NewExpense", login.Headers.Location!.ToString());
        Assert.Contains("Add an expense", (await session.GetPage("/NewExpense")).Body!.TextContent);

        using var loggedOut = await session.Post("/Authentication/Logout", "/", new());
        using var protectedPage = await session.Client.GetAsync("/NewExpense");

        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
    }

    [Fact]
    public async Task Registration_RejectsDuplicateEmail_AndLoginRejectsExternalReturnUrl()
    {
        using var first = new BrowserSession(fixture);
        await first.Register();

        using var second = new BrowserSession(fixture);
        using var duplicate = await second.Post("/Authentication/Register", "/Authentication/Register", new()
        {
            ["Email"] = first.Email,
            ["Password"] = BrowserSession.Password,
            ["ConfirmPassword"] = BrowserSession.Password
        });

        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("already taken", await duplicate.Content.ReadAsStringAsync());

        using var login = await second.Post("/Authentication/Login", "/Authentication/Login", new()
        {
            ["Email"] = first.Email,
            ["Password"] = BrowserSession.Password,
            ["ReturnUrl"] = "https://example.com/evil"
        });

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/Home/CreateExpense")]
    [InlineData("/NewExpense/CreateExpense")]
    [InlineData("/Expense/Edit")]
    [InlineData("/Expense/UpdateExpense")]
    [InlineData("/Expense/Delete")]
    [InlineData("/Expense/DeleteExpense")]
    [InlineData("/Authentication/Logout")]
    [InlineData("/Authentication/Login")]
    [InlineData("/Authentication/Register")]
    public async Task Mutations_RejectMissingAndInvalidAntiforgeryTokens(string endpoint)
    {
        using var session = new BrowserSession(fixture);
        await session.Register();

        var tokenCases = new[]
        {
            new Dictionary<string, string>(),
            new() { ["__RequestVerificationToken"] = "invalid" }
        };

        foreach (var fields in tokenCases)
        {
            using var response = await session.Client.PostAsync(endpoint, new FormUrlEncodedContent(fields));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Forms_PersistCreateEditDelete_AndRenderOrderingAndTotals()
    {
        using var session = new BrowserSession(fixture);
        await session.Register();
        var category = await session.CategoryId();

        await session.CreateExpense(category, "Older lunch", "12.50", "2026-10-01");
        var newestId = await session.CreateExpense(category, "Newest lunch", "20.75", "2026-10-02");
        var page = await session.GetPage("/");

        Assert.Contains("Newest lunch", page.QuerySelectorAll("tbody tr")[0].TextContent);
        Assert.Contains("33.25", page.Body!.TextContent);

        using var edit = await session.Post("/Expense/Edit", $"/Expense/Edit/{newestId}",
            BrowserSession.Expense(category, "Edited lunch", "25.25", id: newestId));

        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        Assert.Contains("37.75", (await session.GetPage("/")).Body!.TextContent);

        var stored = await session.Client.GetFromJsonAsync<List<Expense>>("/Expense/RetrieveExpenses");

        Assert.Equal(25.25m, stored!.Single(e => e.Id == newestId).Amount);

        using var delete = await session.Post("/Expense/Delete", "/", new() { ["id"] = newestId.ToString() });

        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);

        page = await session.GetPage("/");

        Assert.DoesNotContain("Edited lunch", page.Body!.TextContent);
        Assert.Contains("12.50", page.Body.TextContent);
    }

    [Fact]
    public async Task InvalidForms_RedisplayErrorsAndCategories_WithoutChangingDatabase()
    {
        using var session = new BrowserSession(fixture);
        await session.Register();
        var category = await session.CategoryId();

        using var invalid = await session.Post("/Home/CreateExpense", "/NewExpense",
            BrowserSession.Expense(category, amount: "-1"));

        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("field-validation-error", await invalid.Content.ReadAsStringAsync());

        using var unknown = await session.Post("/Home/CreateExpense", "/NewExpense",
            BrowserSession.Expense(Guid.NewGuid()));

        Assert.Contains("Select a valid category.", await unknown.Content.ReadAsStringAsync());
        Assert.Empty((await session.Client.GetFromJsonAsync<List<Expense>>("/Expense/RetrieveExpenses"))!);

        var id = await session.CreateExpense(category);
        using var edit = await session.Post("/Expense/Edit", $"/Expense/Edit/{id}",
            BrowserSession.Expense(Guid.NewGuid(), id: id));

        Assert.Contains("Select a valid category.", await edit.Content.ReadAsStringAsync());

        var stored = (await session.Client.GetFromJsonAsync<List<Expense>>("/Expense/RetrieveExpenses"))!.Single();

        Assert.Equal(category, stored.CategoryId);
    }

    [Fact]
    public async Task JsonEndpoints_CreateUpdateDelete_AndRejectInvalidCategory()
    {
        using var session = new BrowserSession(fixture);
        await session.Register();
        var category = await session.CategoryId();

        using var create = await session.Post("/NewExpense/CreateExpense", "/NewExpense",
            BrowserSession.Expense(category));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var expense = (await create.Content.ReadFromJsonAsync<Expense>())!;

        using var invalid = await session.Post("/Expense/UpdateExpense", "/",
            BrowserSession.Expense(Guid.NewGuid(), id: expense.Id));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var update = await session.Post("/Expense/UpdateExpense", "/",
            BrowserSession.Expense(category, amount: "99.99", id: expense.Id));

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal(99.99m, (await session.Client.GetFromJsonAsync<List<Expense>>("/Expense/RetrieveExpenses"))!.Single().Amount);

        using var delete = await session.Post("/Expense/DeleteExpense", "/", new() { ["id"] = expense.Id.ToString() });

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty((await session.Client.GetFromJsonAsync<List<Expense>>("/Expense/RetrieveExpenses"))!);
    }

    [Fact]
    public async Task OtherUsers_CannotReadEditOrDeleteOwnedExpense_ThroughAnyEndpoint()
    {
        using var owner = new BrowserSession(fixture);
        await owner.Register();
        var category = await owner.CategoryId();
        var id = await owner.CreateExpense(category, "Private expense");

        using var stranger = new BrowserSession(fixture);
        await stranger.Register();

        Assert.Empty((await stranger.Client.GetFromJsonAsync<List<Expense>>("/Expense/RetrieveExpenses"))!);
        Assert.DoesNotContain("Private expense", (await stranger.GetPage("/")).Body!.TextContent);

        using var read = await stranger.Client.GetAsync($"/Expense/Edit/{id}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        var mutationEndpoints = new[]
        {
            "/Expense/Edit",
            "/Expense/UpdateExpense",
            "/Expense/Delete",
            "/Expense/DeleteExpense"
        };

        foreach (var endpoint in mutationEndpoints)
        {
            using var response = await stranger.Post(endpoint, "/", BrowserSession.Expense(category, id: id));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        Assert.Equal("Private expense", (await owner.Client.GetFromJsonAsync<List<Expense>>("/Expense/RetrieveExpenses"))!.Single().Description);
    }
}
