using FlexRequestExtractor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlexRequestExtractor.Service;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
            return SelfTest.Run();

        var builder = Host.CreateApplicationBuilder(args);
        builder.Environment.ContentRootPath = AppContext.BaseDirectory;
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = AppConfig.ServiceName;
        });
        builder.Services.AddHostedService<Worker>();
        builder.Build().Run();
        return 0;
    }
}
