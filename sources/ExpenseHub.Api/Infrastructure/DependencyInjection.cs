using System;
using ExpenseHub.Api.Application.Abstractions;
using ExpenseHub.Api.Application.Accounts;
using ExpenseHub.Api.Application.Expenses;
using ExpenseHub.Api.Application.Reports;
using ExpenseHub.Api.Application.Reports.Processing;
using ExpenseHub.Api.Infrastructure.Identity;
using ExpenseHub.Api.Infrastructure.Messaging;
using ExpenseHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ExpenseHub.Api.Infrastructure;

internal static class DependencyInjection
{
    private const string DefaultConnection = "Data Source=expensehub.db";

    /// <summary>
    /// Registrations shared by the web application and the worker: the clock, the database and the report storage.
    /// Both processes must point at the same database.
    /// </summary>
    public static IServiceCollection AddExpenseHubPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("ExpenseHub") ?? DefaultConnection));

        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IReportDataSource, ReportDataSource>();

        return services;
    }

    public static IServiceCollection AddRabbitMqOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Worker side: calculates and saves the reports that arrive on the queue.
    /// </summary>
    public static IServiceCollection AddReportProcessing(this IServiceCollection services)
    {
        services.AddScoped<ReportProcessingService>();

        return services;
    }

    public static IServiceCollection AddExpenseHub(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddExpenseHubPersistence(configuration);
        services.AddRabbitMqOptions(configuration);

        services
            .AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IReportPublisher, RabbitMqReportPublisher>();
        services.AddScoped<IIdentityGateway, IdentityGateway>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
