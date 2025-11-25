using Microsoft.EntityFrameworkCore;
using MyFinance.API.Models; // Ajuste o namespace conforme necessário

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<RecurringTransaction> RecurringTransactions { get; set; }
}