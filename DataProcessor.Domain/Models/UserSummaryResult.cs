namespace DataProcessor.Domain.Models;

public sealed record UserSummaryResult
{
    public required string Username { get; init; }
    public required decimal TotalGrossAmount { get; init; }
    public required decimal TotalDiscountAmount { get; init; }
    public required decimal TotalNetAmount { get; init; }
    public required int TotalRecords { get; init; }
    public required IReadOnlyList<ProductUsage> Products { get; init; }
}

public sealed record ProductUsage
{
    public required string Product { get; init; }
    public required string Sku { get; init; }
    public required int TotalQuantity { get; init; }
    public required decimal TotalNetAmount { get; init; }
}
