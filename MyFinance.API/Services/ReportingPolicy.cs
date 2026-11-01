using MyFinance.API.Models;
using System.Linq.Expressions;

namespace MyFinance.API.Services;

public static class ReportingPolicy
{
    public static Expression<Func<Transaction, bool>> OperationalPredicate { get; } =
        transaction => transaction.ReportingKind == ReportingKinds.Normal &&
                       !transaction.IsTransfer &&
                       !transaction.ExcludeFromReports;

    public static Expression<Func<Transaction, bool>> CardLiabilityPredicate { get; } =
        transaction => !transaction.IsTransfer &&
                       transaction.ReportingKind != ReportingKinds.InvoicePayment;

    private static readonly Func<Transaction, bool> Operational = OperationalPredicate.Compile();
    private static readonly Func<Transaction, bool> CardLiability = CardLiabilityPredicate.Compile();

    public static bool IsOperational(Transaction transaction) =>
        Operational(transaction);

    public static bool IsCardLiabilityMovement(Transaction transaction) =>
        CardLiability(transaction);

    public static bool IsSettlement(Transaction transaction) => transaction.ReportingKind == ReportingKinds.InvoicePayment;

    public static bool IsMovement(Transaction transaction) =>
        transaction.IsTransfer || ReportingKinds.ExcludedByDefault.Contains(transaction.ReportingKind);
}
