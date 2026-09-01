using MyFinance.API.Models;

namespace MyFinance.API.Services;

public static class ReportingPolicy
{
    public static bool IsOperational(Transaction transaction) =>
        transaction.ReportingKind == ReportingKinds.Normal && !transaction.IsTransfer && !transaction.ExcludeFromReports;

    public static bool IsSettlement(Transaction transaction) => transaction.ReportingKind == ReportingKinds.InvoicePayment;

    public static bool IsMovement(Transaction transaction) =>
        transaction.IsTransfer || ReportingKinds.ExcludedByDefault.Contains(transaction.ReportingKind);
}
