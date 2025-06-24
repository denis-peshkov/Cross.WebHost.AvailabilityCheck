using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Cross.WebHost.AvailabilityCheck.UnitTests")]

namespace Cross.WebHost.AvailabilityCheck;

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

    public static void ConfigureAvailabilityHosts(string[] configuredHosts, IConfiguration configuration, ILogger logger, int timeoutInMs = 1000, int maxAttempt = 50)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configuredHosts);
        ArgumentNullException.ThrowIfNull(logger);

        var validHosts = configuration.GetValues(configuredHosts)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToArray();

        if (validHosts.Length == 0) return;

        logger.LogInformation("Checking the launch of the necessary microservices...");
        CheckHostsAsync(validHosts, logger, timeoutInMs, maxAttempt).GetAwaiter().GetResult();
    }

    private static IEnumerable<string> GetValues(this IConfiguration configuration, IReadOnlyList<string> keys)
    {
        return keys.Select(key => configuration.GetValue<string>(key)!);
    }

    private static async Task CheckHostsAsync(
        IEnumerable<string> hostStrings,
        ILogger logger,
        int timeoutInMs = 1000,
        int maxAttempt = 50,
        CancellationToken cancellationToken = default)
    {
        var validHosts = hostStrings
            .Select(host => ParseHost(host, logger))
            .Where(host => host != null)
            .ToList();

        var tasks = validHosts.Select(host =>
            CheckHostAvailabilityAsync(host!, logger, timeoutInMs, maxAttempt, cancellationToken));

        await Task.WhenAll(tasks);
    }

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


    private static async Task CheckHostAvailabilityAsync(
        Host host,
        ILogger logger,
        int timeoutInMs,
        int maxAttempt,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMilliseconds(timeoutInMs);

        for (var attempt = 1; attempt <= maxAttempt; attempt++)
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

                if (attempt == maxAttempt)
                {
                    logger.LogError(ex, "Host '{HostName}:{Port}' is down after {Attempts} attempts",
                        host.Hostname, host.Port, maxAttempt);
                    return;
                }

                logger.LogInformation(
                    "Attempt {CurrentAttempt}/{MaxAttempts}. Host '{HostName}:{Port}' not available yet: {Message}",
                    attempt, maxAttempt, host.Hostname, host.Port, ex.Message);

                await Task.Delay(timeoutInMs, cancellationToken);
            }
        }
    }

    internal sealed record Host(string Hostname, int Port);
}
