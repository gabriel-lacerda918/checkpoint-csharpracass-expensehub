using System;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Infrastructure.Persistence;

/// <summary>
/// Single database context: ASP.NET Core Identity tables plus the expense workflow tables.
/// </summary>
internal sealed class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    public DbSet<ExpenseHistory> ExpenseHistories => Set<ExpenseHistory>();

    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    public DbSet<ExpenseReport> ExpenseReports => Set<ExpenseReport>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ExpenseCategory>(category =>
        {
            category.ToTable("ExpenseCategories");
            category.HasKey(item => item.Id);
            category.Property(item => item.Id).ValueGeneratedNever();
            category.Property(item => item.Name).IsRequired().HasMaxLength(100);
            category.HasIndex(item => item.Name).IsUnique();
        });

        builder.Entity<Expense>(expense =>
        {
            expense.ToTable("Expenses");
            expense.HasKey(item => item.Id);
            expense.Property(item => item.OwnerId).IsRequired().HasMaxLength(450);
            expense.Property(item => item.Description).IsRequired().HasMaxLength(500);
            expense.Property(item => item.Amount).HasColumnType("decimal(18,2)");
            expense.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            expense.Property(item => item.RejectionReason).HasMaxLength(500);
            expense.Property(item => item.Version).IsConcurrencyToken();
            expense.HasIndex(item => item.OwnerId);
            expense.HasIndex(item => item.Status);
            expense.HasOne<IdentityUser>().WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Restrict);
            expense.HasOne<ExpenseCategory>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExpenseHistory>(history =>
        {
            history.ToTable("ExpenseHistories");
            history.HasKey(item => item.Id);
            history.Property(item => item.Action).HasConversion<string>().HasMaxLength(20);
            history.Property(item => item.ActorId).IsRequired().HasMaxLength(450);
            history.Property(item => item.PreviousStatus).HasConversion<string>().HasMaxLength(20);
            history.Property(item => item.NewStatus).HasConversion<string>().HasMaxLength(20);
            history.Property(item => item.Reason).HasMaxLength(500);
            history.Property(item => item.Changes).HasMaxLength(4000);
            history.HasIndex(item => item.ExpenseId);
            history.HasOne<Expense>().WithMany().HasForeignKey(item => item.ExpenseId).OnDelete(DeleteBehavior.Restrict);
            history.HasOne<IdentityUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExpenseReport>(report =>
        {
            report.ToTable("ExpenseReports");
            report.HasKey(item => item.Id);
            report.Property(item => item.RequesterId).IsRequired().HasMaxLength(450);
            report.Property(item => item.Scope).HasConversion<string>().HasMaxLength(10);
            report.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
            report.Property(item => item.ErrorCode).HasMaxLength(50);
            report.Property(item => item.ErrorMessage).HasMaxLength(500);
            report.Property(item => item.Version).IsConcurrencyToken();
            report.HasIndex(item => item.RequesterId);
            report.HasOne<IdentityUser>().WithMany().HasForeignKey(item => item.RequesterId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PaymentRecord>(payment =>
        {
            payment.ToTable("PaymentRecords");
            payment.HasKey(item => item.Id);
            payment.Property(item => item.PaidByUserId).IsRequired().HasMaxLength(450);
            payment.Property(item => item.Amount).HasColumnType("decimal(18,2)");
            payment.HasIndex(item => item.ExpenseId).IsUnique();
            payment.HasOne<Expense>().WithMany().HasForeignKey(item => item.ExpenseId).OnDelete(DeleteBehavior.Restrict);
            payment.HasOne<IdentityUser>().WithMany().HasForeignKey(item => item.PaidByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
