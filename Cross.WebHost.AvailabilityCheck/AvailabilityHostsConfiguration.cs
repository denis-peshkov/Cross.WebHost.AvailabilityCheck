namespace Cross.WebHost.AvailabilityCheck;

/// <summary>
/// Provides functionality for validating microservice dependencies during application startup.
/// Ensures that all required services are available before the main application begins its operation.
/// </summary>
public static class AvailabilityHostsConfiguration
{
    private const string PORT_SEPARATOR = ":";
    private const string HTTP_PREFIX = "http://";
    private const string HTTPS_PREFIX = "https://";
    private static readonly Dictionary<string, int> _defaultPorts = new()
    {
        [HTTP_PREFIX] = 80,
        [HTTPS_PREFIX] = 443,
    };

    /// <summary>
    /// Configures and performs availability checks for the specified service hosts.
    /// </summary>
    /// <param name="configuredHosts">Array of configuration keys pointing to service URLs to check.</param>
    /// <param name="configuration">The application configuration containing service URLs.</param>
    /// <param name="logger">Logger instance for recording availability check results.</param>
    /// <param name="timeoutInMs">Timeout in milliseconds for each check attempt. Default is 1000 ms.</param>
    /// <param name="maxAttempts">Maximum number of retry attempts for each host. Default is 50 times.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the required parameters is null.</exception>
    /// <remarks>
    /// Supports various URL formats including:
    /// <list type="bullet">
    ///   <item><description>HTTP URLs: http://service-name:80</description></item>
    ///   <item><description>HTTPS URLs: https://service-name:443</description></item>
    ///   <item><description>Default ports: http://service-name (assumes port 80)</description></item>
    ///   <item><description>Default ports: https://service-name (assumes port 443)</description></item>
    ///   <item><description>Custom ports: service-name:5000</description></item>
    ///   <item><description>IP addresses: 192.168.1.100:5000</description></item>
    /// </list>
    /// </remarks>
    public static void ConfigureAvailabilityHosts(string[] configuredHosts, IConfiguration configuration, ILogger logger, int timeoutInMs = 1000, int maxAttempts = 50)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuredHosts);
        ArgumentNullException.ThrowIfNull(logger);

        var validHosts = configuration.GetValues(configuredHosts)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToArray();

        if (validHosts.Length == 0) return;

        logger.LogInformation("Checking the launch of the necessary microservices...");
        CheckHostsAsync(validHosts, logger, timeoutInMs, maxAttempts).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Extracts values from the configuration for the specified keys.
    /// </summary>
    /// <param name="configuration">The configuration to read from.</param>
    /// <param name="keys">List of configuration keys to retrieve values for.</param>
    /// <returns>Collection of configuration values.</returns>
    private static IEnumerable<string> GetValues(this IConfiguration configuration, IReadOnlyList<string> keys)
    {
        return keys.Select(key => configuration.GetValue<string>(key)!);
    }

    /// <summary>
    /// Asynchronously checks the availability of multiple hosts.
    /// </summary>
    /// <param name="hostStrings">Collection of host strings to check.</param>
    /// <param name="logger">Logger instance for recording check results.</param>
    /// <param name="timeoutInMs">Timeout in milliseconds for each check attempt.</param>
    /// <param name="maxAttempts">Maximum number of retry attempts.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task CheckHostsAsync(
        IEnumerable<string> hostStrings,
        ILogger logger,
        int timeoutInMs = 1000,
        int maxAttempts = 50,
        CancellationToken cancellationToken = default)
    {
        var validHosts = hostStrings
            .Select(host => ParseHost(host, logger))
            .Where(host => host != null)
            .ToList();

        var tasks = validHosts
            .Select(host => CheckHostAvailabilityAsync(host!, logger, timeoutInMs, maxAttempts, cancellationToken));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Parses a host string into a structured Host object.
    /// </summary>
    /// <param name="hostString">The host string to parse.</param>
    /// <param name="logger">Logger instance for recording parsing results.</param>
    /// <returns>A Host object if parsing is successful; null otherwise.</returns>
    /// <remarks>
    /// Supports parsing of:
    /// <list type="bullet">
    ///   <item><description>HTTP/HTTPS URLs with optional ports</description></item>
    ///   <item><description>Hostname with explicit port</description></item>
    ///   <item><description>IP addresses with port</description></item>
    /// </list>
    /// </remarks>
    private static Host? ParseHost(string hostString, ILogger logger)
    {
        logger.LogInformation("Processing host: {Host}", hostString);

        foreach (var (prefix, defaultPort) in _defaultPorts)
        {
            if (!hostString.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var hostname = hostString.Split(prefix)[1].TrimEnd('/');
            logger.LogInformation("Host {Host} matched pattern \"{Pattern}\"", hostString, prefix);

            // Check if hostname contains port
            return hostname.Contains(PORT_SEPARATOR)
                ? new Host(hostname[..hostname.IndexOf(PORT_SEPARATOR, StringComparison.OrdinalIgnoreCase)], defaultPort)
                : new Host(hostname, defaultPort);
        }

        if (hostString.Contains(PORT_SEPARATOR))
        {
            var parts = hostString.Split(PORT_SEPARATOR);
            if (parts.Length == 2 && int.TryParse(parts[1].TrimEnd('/'), out var port))
            {
                logger.LogInformation("Host {Host} matched pattern \"{Pattern}\"", hostString, PORT_SEPARATOR);
                return new Host(parts[0], port);
            }
        }

        logger.LogInformation("Host {Host} did not match any of the expected patterns. Skipping host", hostString);
        return null;
    }

    /// <summary>
    /// Checks the availability of a single host with the retry mechanism.
    /// </summary>
    /// <param name="host">The host to check.</param>
    /// <param name="logger">Logger instance for recording check results.</param>
    /// <param name="timeoutInMs">Timeout in milliseconds for each attempt.</param>
    /// <param name="maxAttempts">Maximum number of retry attempts.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    private static async Task CheckHostAvailabilityAsync(
        Host host,
        ILogger logger,
        int timeoutInMs,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMilliseconds(timeoutInMs);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var tcpClient = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeout);

                await tcpClient.ConnectAsync(host.Hostname, host.Port, cts.Token);

                sw.Stop();
                logger.LogInformation("Host '{HostName}:{Port}' is available after {Time:F2} seconds",
                    host.Hostname, host.Port, sw.Elapsed.TotalSeconds);
                return;
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException("Host availability check was cancelled", ex, cancellationToken);

                if (attempt == maxAttempts)
                {
                    logger.LogError(ex, "Host '{HostName}:{Port}' is down after {Attempts} attempts",
                        host.Hostname, host.Port, maxAttempts);
                    return;
                }

                logger.LogInformation(
                    "Attempt {CurrentAttempt}/{MaxAttempts}. Host '{HostName}:{Port}' not available yet: {Message}",
                    attempt, maxAttempts, host.Hostname, host.Port, ex.Message);

                await Task.Delay(timeoutInMs, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Represents a host configuration with hostname and port.
    /// </summary>
    /// <param name="Hostname">The hostname or IP address of the service.</param>
    /// <param name="Port">The port number the service is listening on.</param>
    internal sealed record Host(string Hostname, int Port);
}
