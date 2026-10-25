using ExpenseTracker.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace ExpenseTracker.Testing;

public class AppFixture : IAsyncLifetime
{
    private bool _disposed;
    private bool _databaseStarted;

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("expensetracker_tests")
        .WithUsername("test_user")
        .WithPassword("TestOnlyPassword1")
        .Build();

    public TestAppFactory App { get; private set; } = null!;

    protected virtual bool UseBrowserServer => false;

    public string ArtifactDirectory { get; } = Path.GetFullPath(Path.Combine(
        Environment.GetEnvironmentVariable("TEST_ARTIFACTS") ?? "TestResults",
        $"{(AppDomain.CurrentDomain.FriendlyName)}-{Guid.NewGuid():N}"));

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(ArtifactDirectory);

        try
        {
            await _database.StartAsync();
            _databaseStarted = true;

            App = new TestAppFactory(_database.GetConnectionString(), ArtifactDirectory);
            if (UseBrowserServer)
            {
                App.UseKestrel(0);
            }

            App.StartServer();

            using var client = CreateClient();
            (await client.GetAsync("/health")).EnsureSuccessStatusCode();
        }
        catch
        {
            try
            {
                await DisposeAsync();
            }
            catch
            {
            }

            throw;
        }
    }

    public HttpClient CreateClient() => App.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = UseBrowserServer ? App.ClientOptions.BaseAddress : new Uri("https://localhost")
    });

    public async Task DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (App is not null)
            {
                await App.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                if (_databaseStarted)
                {
                    var (stdout, stderr) = await _database.GetLogsAsync();
                    await File.WriteAllTextAsync(Path.Combine(ArtifactDirectory, "postgres.log"), stdout + stderr);
                }
            }
            finally
            {
                await _database.DisposeAsync();
            }
        }
    }
}

public sealed class TestAppFactory(string connectionString, string artifactDirectory) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        });

        builder.ConfigureLogging(logging => logging.AddProvider(
            new FileLoggerProvider(Path.Combine(artifactDirectory, "application.log"))));
    }
}

internal sealed class FileLoggerProvider(string path) : ILoggerProvider
{
    private readonly object _gate = new();
    private readonly string _path = path;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose() { }

    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;

        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level))
            {
                return;
            }

            lock (owner._gate)
            {
                File.AppendAllText(
                    path: owner._path,
                    contents: $"{DateTimeOffset.UtcNow:O} {level} {category}: {formatter(state, exception)} {exception}{Environment.NewLine}");
            }
        }
    }
}
