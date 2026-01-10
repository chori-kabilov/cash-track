using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<ReminderSetting> ReminderSettings => Set<ReminderSetting>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();
    public DbSet<RegularPayment> RegularPayments => Set<RegularPayment>();
    public DbSet<RegularPaymentHistory> RegularPaymentHistories => Set<RegularPaymentHistory>();
    public DbSet<Limit> Limits => Set<Limit>();
}

