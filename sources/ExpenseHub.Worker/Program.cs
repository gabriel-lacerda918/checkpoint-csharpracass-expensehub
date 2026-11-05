using System.Threading.Tasks;
using ExpenseHub.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExpenseHub.Worker;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddExpenseHubPersistence(builder.Configuration);
        builder.Services.AddRabbitMqOptions(builder.Configuration);
        builder.Services.AddReportProcessing();
        builder.Services.AddHostedService<ReportConsumerService>();

        using IHost host = builder.Build();

        await host.RunAsync();
    }
}
