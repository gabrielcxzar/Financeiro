using Microsoft.EntityFrameworkCore;
using MyFinance.API.Models;
using OpenIddict.EntityFrameworkCore;

namespace MyFinance.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<RecurringTransaction> RecurringTransactions { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<FiiHolding> FiiHoldings { get; set; }
        public DbSet<FinancialGoal> FinancialGoals { get; set; }
        public DbSet<ImportBatch> ImportBatches { get; set; }
        public DbSet<ImportedStatementItem> ImportedStatementItems { get; set; }
        public DbSet<CategorizationRule> CategorizationRules { get; set; }
        public DbSet<GmailIntegration> GmailIntegrations { get; set; }
        public DbSet<ExternalImportArtifact> ExternalImportArtifacts { get; set; }
        public DbSet<GmailOAuthState> GmailOAuthStates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseOpenIddict();

            modelBuilder.Entity<Transaction>()
                .HasIndex(t => new { t.UserId, t.AccountId, t.Source, t.ExternalId })
                .IsUnique()
                .HasFilter("external_id IS NOT NULL");

            modelBuilder.Entity<ImportBatch>()
                .HasIndex(b => new { b.UserId, b.AccountId, b.FileHash });

            modelBuilder.Entity<ImportedStatementItem>()
                .HasIndex(i => new { i.UserId, i.AccountId, i.Source, i.ExternalId });

            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.ImportBatch)
                .WithMany()
                .HasForeignKey(t => t.ImportBatchId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<GmailIntegration>()
                .HasIndex(x => x.UserId)
                .IsUnique();
            modelBuilder.Entity<GmailIntegration>()
                .HasOne(x => x.DefaultAccount)
                .WithMany()
                .HasForeignKey(x => x.DefaultAccountId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<ExternalImportArtifact>()
                .HasIndex(x => new { x.UserId, x.Provider, x.ExternalMessageId, x.ExternalAttachmentId })
                .IsUnique();
            modelBuilder.Entity<ExternalImportArtifact>()
                .HasOne(x => x.ImportBatch)
                .WithMany()
                .HasForeignKey(x => x.ImportBatchId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<GmailOAuthState>()
                .HasIndex(x => x.StateHash)
                .IsUnique();

            modelBuilder.Entity<ImportedStatementItem>()
                .HasOne(i => i.Transaction)
                .WithMany()
                .HasForeignKey(i => i.TransactionId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
