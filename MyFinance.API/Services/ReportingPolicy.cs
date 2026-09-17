using MyFinance.API.Models;
using System.Linq.Expressions;

namespace MyFinance.API.Services;

public static class ReportingPolicy
{
    public static Expression<Func<Transaction, bool>> OperationalPredicate { get; } =
        transaction => transaction.ReportingKind == ReportingKinds.Normal &&
                       !transaction.IsTransfer &&
                       !transaction.ExcludeFromReports;

    private static readonly Func<Transaction, bool> Operational = OperationalPredicate.Compile();

    public static bool IsOperational(Transaction transaction) =>
        Operational(transaction);

    public static bool IsSettlement(Transaction transaction) => transaction.ReportingKind == ReportingKinds.InvoicePayment;

    public static bool IsMovement(Transaction transaction) =>
        transaction.IsTransfer || ReportingKinds.ExcludedByDefault.Contains(transaction.ReportingKind);
}
