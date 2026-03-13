namespace DataProcessor.Domain.Models;

public sealed record BillingRecord
{
    public required DateOnly Date { get; init; }
    public required string Username { get; init; }
    public required string Product { get; init; }
    public required string Sku { get; init; }
    public required string Model { get; init; }
    public required int Quantity { get; init; }
    public required string UnitType { get; init; }
    public required decimal AppliedCostPerQuantity { get; init; }
    public required decimal GrossAmount { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required decimal NetAmount { get; init; }
    public required bool ExceedsQuota { get; init; }
    public required int TotalMonthlyQuota { get; init; }
    public required string Organization { get; init; }
    public required string CostCenterName { get; init; }
}
