namespace ProjectExchange.Core.Markets;

/// <summary>
/// Binary contract side for liquid contracts. Each market can expose YES and NO as separate tradable instruments.
/// </summary>
public enum ContractType
{
    Yes = 0,
    No = 1
}
