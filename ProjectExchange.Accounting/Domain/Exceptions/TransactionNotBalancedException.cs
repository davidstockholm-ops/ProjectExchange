namespace ProjectExchange.Accounting.Domain.Exceptions;

/// <summary>
/// Thrown when a transaction's debits do not equal credits (double-entry invariant violated).
/// </summary>
public class TransactionNotBalancedException : DomainException
{
    public decimal TotalDebits { get; }
    public decimal TotalCredits { get; }

    public TransactionNotBalancedException(decimal totalDebits, decimal totalCredits)
        : base($"Transaction is not balanced: Debits={totalDebits}, Credits={totalCredits}. They must be equal.")
    {
        TotalDebits = totalDebits;
        TotalCredits = totalCredits;
    }
}
