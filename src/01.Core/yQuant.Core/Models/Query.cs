namespace yQuant.Core.Models;

/// <summary>
/// Represents a query request for information from the broker
/// </summary>
public record Query
{
    /// <summary>
    /// Type of query (e.g., "price", "account", "position")
    /// </summary>
    public required string QueryType { get; init; }

    /// <summary>
    /// Target identifier (e.g., ticker symbol for price queries, account alias for account queries)
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Optional account alias for queries that require account context
    /// </summary>
    public string? AccountAlias { get; init; }

    /// <summary>
    /// Optional parameters for the query
    /// </summary>
    public Dictionary<string, string>? Parameters { get; init; }

    /// <summary>
    /// Timestamp when the query was created
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
