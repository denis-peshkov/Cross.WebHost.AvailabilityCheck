namespace Cross.WebHost.AvailabilityCheck.UnitTests.Common;

[TestFixture]
public abstract class TestsBase
{
    protected IConfiguration Configuration;

    [SetUp]
    public virtual void OneTimeSetUp()
    {

        Configuration = LoadConfiguration();
    }

    [TearDown]
    // [OneTimeTearDown]
    public virtual void OneTimeTearDown()
    {
    }

    private static IConfiguration LoadConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Test.json", false, true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        return builder.Build();
    }
}
