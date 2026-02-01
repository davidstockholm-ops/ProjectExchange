namespace ProjectExchange.Accounting.Domain.Exceptions;

/// <summary>
/// Base for domain rule violations in the accounting bounded context.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}
