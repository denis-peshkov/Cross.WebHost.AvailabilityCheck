namespace Cross.WebHost.AvailabilityCheck.UnitTests;

[TestFixture]
public class AvailabilityHostsConfigurationTests : TestsBase
{

    private Mock<ILogger> _loggerMock;
    private CancellationTokenSource _cts;

    public override void OneTimeSetUp()
    {
        _loggerMock = new Mock<ILogger>();
        _cts = new CancellationTokenSource();
    }

    public override void OneTimeTearDown()
    {
        _cts.Dispose();
    }

    [Test]
    public void ConfigureAvailabilityHosts_WhenNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        IConfiguration configuration = null!;
        var hosts = new[] { "test.com" };

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            AvailabilityHostsConfiguration.ConfigureAvailabilityHosts(hosts, configuration, _loggerMock.Object));
        Assert.That(ex.ParamName, Is.EqualTo("configuration"));
    }

    [TestCase("custom-host:5000", "custom-host", 5000)]
    [TestCase("http://custom-host", "custom-host", 80)]
    [TestCase("https://custom-host", "custom-host", 443)]
    [TestCase("http://custom-host:80", "custom-host", 80)]
    [TestCase("https://custom-host:443", "custom-host", 443)]
    public void ParseHost_WithInvalidInput_ReturnHost(string input, string hostname, int port)
    {
        // Arrange
        var method = typeof(AvailabilityHostsConfiguration)
            .GetMethod("ParseHost", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = method!.Invoke(null, new object[] { input, _loggerMock.Object });

        // Assert
        result.Should().NotBeNull();
        var host = (AvailabilityHostsConfiguration.Host)result!;
        host.Hostname.Should().Be(hostname);
        host.Port.Should().Be(port);
    }

    [TestCase("")]
    [TestCase("invalid-host")]
    [TestCase("example.com:invalid")]
    public void ParseHost_WithInvalidInput_ReturnsNull(string input)
    {
        // Arrange
        var method = typeof(AvailabilityHostsConfiguration)
            .GetMethod("ParseHost", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = method!.Invoke(null, new object[] { input, _loggerMock.Object });

        // Assert
        result.Should().BeNull();
    }
}
