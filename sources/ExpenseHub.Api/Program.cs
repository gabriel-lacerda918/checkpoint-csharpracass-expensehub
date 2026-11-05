using System.Threading.Tasks;
using ExpenseHub.Api.Http;
using ExpenseHub.Api.Infrastructure;
using ExpenseHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExpenseHub.Api;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddOpenApi();
        builder.Services.AddExpenseHub(builder.Configuration);

        WebApplication app = builder.Build();

        await DatabaseInitializer.InitializeAsync(app.Services, app.Lifetime.ApplicationStopping);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();

        // The version comes from the image build (APP_VERSION), so the running container can prove which commit it is.
        string version = app.Configuration["APP_VERSION"] ?? "local";
        app.MapGet("/health", () => Results.Ok(new { status = "ok", version }))
            .WithName("GetHealth");
        app.MapControllers();

        await app.RunAsync();
    }
}
