[![Nuget](https://img.shields.io/nuget/v/Cross.WebHost.AvailabilityCheck.svg)](https://nuget.org/packages/Cross.WebHost.AvailabilityCheck/) [![Documentation](https://img.shields.io/badge/docs-wiki-yellow.svg)](https://github.com/denis-peshkov/Cross.WebHost.AvailabilityCheck/wiki)

# Cross.WebHost.AvailabilityCheck

A lightweight .NET library for validating microservice dependencies during application startup. The library ensures that all required services are available before the main application begins its operation.


## Features
- Asynchronous health checks for HTTP/HTTPS endpoints
- Configurable retry policies with customizable timeouts
- Support for custom port configurations
- Built-in logging for dependency validation process
- Cancellation support for health check operations


## Purpose
Designed to prevent application startup failures due to unavailable dependencies by implementing a readiness probe pattern for microservice environments.


## Install with nuget.org:

https://www.nuget.org/packages/Cross.WebHost.AvailabilityCheck


## Installation

Install via NuGet Package Manager:
```bash
dotnet add package Cross.WebHost.AvailabilityCheck
```

Or via Package Manager Console:

```powershell
Install-Package Cross.WebHost.AvailabilityCheck
```


## Usage

### Basic Configuration

Add service availability check in your `Program.cs` or `Startup.cs`:

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ...

        var app = builder.Build();

        // ...

        // Configure service hosts to check
        var hostsToCheck = new[]
        {
            "CheckServiceUrls:AuthService",
            "CheckServiceUrls:UserService"
        };

        // Add availability check
        AvailabilityHostsConfiguration.ConfigureAvailabilityHosts(
            hostsToCheck,
            builder.Configuration,
            app.Logger,
            timeoutInMs: 1500, // (Optional) Default: 1000 ms.
            maxAttempts: 100   // (Optional) Default: 50 times.
            );

        app.Run();
    }
}
```

### Configuration in appsettings.json

```json
{
  "CheckServiceUrls": {
    "AuthService": "https://auth-service:5001",
    "UserService": "http://user-service:5002"
  }
}
```


## Supported URL Formats

The library supports various URL formats:

- HTTP URLs: `http://service-name:80`
- HTTPS URLs: `https://service-name:443`
- HTTP Default ports: `http://service-name` (assumes port 80)
- HTTPS Default ports: `http://service-name` (assumes port 443)
- Custom ports: `service-name:5000`
- IP addresses: `192.168.1.100:5000`


## Logging

The library uses standard .NET logging mechanisms. Configure logging level in your appsettings.json:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Cross.WebHost.AvailabilityCheck": "Debug"
    }
  }
}
```


## Issues and Pull Request

Contribution is welcomed. If you would like to provide a PR please add some testing.


## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request


## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.


## Roadmap
