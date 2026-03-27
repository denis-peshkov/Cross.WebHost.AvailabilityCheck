namespace Cross.WebHost.AvailabilityCheck.Tests;

[TestFixture]
public class AvailabilityHostsConfigurationTests : TestsBase
{

    private Mock<ILogger> _loggerMock;

    public override void OneTimeSetUp()
    {
        _loggerMock = new Mock<ILogger>();
    }

    public override void OneTimeTearDown()
    {
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

    [Test]
    public void ConfigureAvailabilityHosts_WhenNullHosts_ThrowsArgumentNullException()
    {
        // Arrange
        string[] hosts = null!;
        var configuration = new ConfigurationBuilder().Build();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            AvailabilityHostsConfiguration.ConfigureAvailabilityHosts(hosts, configuration, _loggerMock.Object));
        Assert.That(ex.ParamName, Is.EqualTo("configuredHosts"));
    }

    [Test]
    public void ConfigureAvailabilityHosts_WhenNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var hosts = new[] { "ServiceUrl" };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceUrl"] = "localhost:1234",
            })
            .Build();
        ILogger logger = null!;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            AvailabilityHostsConfiguration.ConfigureAvailabilityHosts(hosts, configuration, logger));
        Assert.That(ex.ParamName, Is.EqualTo("logger"));
    }

    [Test]
    public void ConfigureAvailabilityHosts_WhenNoValidHosts_ReturnsWithoutChecking()
    {
        // Arrange
        var hosts = new[] { "ServiceA", "ServiceB" };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceA"] = "",
                ["ServiceB"] = null,
            })
            .Build();

        // Act
        AvailabilityHostsConfiguration.ConfigureAvailabilityHosts(hosts, configuration, _loggerMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("Checking the launch of the necessary microservices...", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!),
            Times.Never);
    }

    [Test]
    public void ConfigureAvailabilityHosts_WhenHostUnavailable_CompletesAfterRetries()
    {
        // Arrange
        var closedPort = GetUnusedTcpPort();
        var hosts = new[] { "ServiceUrl" };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceUrl"] = $"127.0.0.1:{closedPort}",
            })
            .Build();

        // Act
        AvailabilityHostsConfiguration.ConfigureAvailabilityHosts(hosts, configuration, _loggerMock.Object, timeoutInMs: 10, maxAttempts: 2);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("is down after", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!),
            Times.Once);
    }

    [Test]
    public async Task CheckHostAvailabilityAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var method = typeof(AvailabilityHostsConfiguration)
            .GetMethod("CheckHostAvailabilityAsync", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var hostType = typeof(AvailabilityHostsConfiguration).GetNestedType("Host", BindingFlags.NonPublic);
        hostType.Should().NotBeNull();

        var host = Activator.CreateInstance(hostType!, new object[] { "127.0.0.1", GetUnusedTcpPort() });
        host.Should().NotBeNull();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var task = (Task)method!.Invoke(null, new[] { host!, _loggerMock.Object, 10, 2, cts.Token })!;

        // Assert
        var action = async () => await task;
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task CheckHostAvailabilityAsync_WhenHostBecomesAvailable_LogsSuccess()
    {
        // Arrange
        var method = typeof(AvailabilityHostsConfiguration)
            .GetMethod("CheckHostAvailabilityAsync", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var hostType = typeof(AvailabilityHostsConfiguration).GetNestedType("Host", BindingFlags.NonPublic);
        hostType.Should().NotBeNull();

        var port = GetUnusedTcpPort();
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        try
        {
            var acceptTask = listener.AcceptTcpClientAsync();
            var host = Activator.CreateInstance(hostType!, new object[] { "127.0.0.1", port });

            // Act
            var task = (Task)method!.Invoke(null, new[] { host!, _loggerMock.Object, 500, 1, CancellationToken.None })!;
            await task;
            using var acceptedClient = await acceptTask;
        }
        finally
        {
            listener.Stop();
        }

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("is available after", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!),
            Times.Once);
    }

    [TestCase("custom-host:5000", "custom-host", 5000)]
    [TestCase("http://custom-host", "custom-host", 80)]
    [TestCase("https://custom-host", "custom-host", 443)]
    [TestCase("http://custom-host:80", "custom-host", 80)]
    [TestCase("https://custom-host:443", "custom-host", 443)]
    [TestCase("http://custom-host:1234", "custom-host", 80)]
    [TestCase("https://custom-host:1234", "custom-host", 443)]
    [TestCase("https://custom-host/", "custom-host", 443)]
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
    [TestCase("a:b:c")]
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

    [Test]
    public void ParseHost_WhenPrefixHasDifferentCase_ThrowsTargetInvocationException()
    {
        // Arrange
        var method = typeof(AvailabilityHostsConfiguration)
            .GetMethod("ParseHost", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var action = () => method!.Invoke(null, new object[] { "HTTP://custom-host", _loggerMock.Object });

        // Assert
        action.Should().Throw<TargetInvocationException>();
    }

    private static int GetUnusedTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
