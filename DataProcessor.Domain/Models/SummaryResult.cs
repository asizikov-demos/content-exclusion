namespace DataProcessor.Domain.Models;

public sealed record SummaryResult
{
    public required decimal TotalGrossAmount { get; init; }
    public required decimal TotalDiscountAmount { get; init; }
    public required decimal TotalNetAmount { get; init; }
    public required int TotalRecords { get; init; }
    public required int UniqueUserCount { get; init; }
    public required IReadOnlyList<CostCenterSummary> CostCenters { get; init; }
}

public sealed record CostCenterSummary
{
    public required string CostCenterName { get; init; }
    public required decimal TotalNetAmount { get; init; }
    public required int RecordCount { get; init; }
}
