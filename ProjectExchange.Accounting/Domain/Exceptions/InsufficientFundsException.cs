namespace ProjectExchange.Accounting.Domain.Exceptions;

/// <summary>
/// Thrown when a buyer attempts a trade that would exceed their account balance.
/// </summary>
public class InsufficientFundsException : DomainException
{
    public decimal Required { get; }
    public decimal Available { get; }

    public InsufficientFundsException(decimal required, decimal available)
        : base($"Insufficient funds: required {required:N2}, available {available:N2}.")
    {
        Required = required;
        Available = available;
    }
}
