namespace DataProcessor.Infra.Csv;

using CsvHelper.Configuration;
using DataProcessor.Domain.Models;

public sealed class BillingRecordMap : ClassMap<BillingRecord>
{
    public BillingRecordMap()
    {
        Map(m => m.Date).Name("date").TypeConverter<CsvHelper.TypeConversion.DateOnlyConverter>();
        Map(m => m.Username).Name("username");
        Map(m => m.Product).Name("product");
        Map(m => m.Sku).Name("sku");
        Map(m => m.Model).Name("model");
        Map(m => m.Quantity).Name("quantity");
        Map(m => m.UnitType).Name("unit_type");
        Map(m => m.AppliedCostPerQuantity).Name("applied_cost_per_quantity");
        Map(m => m.GrossAmount).Name("gross_amount");
        Map(m => m.DiscountAmount).Name("discount_amount");
        Map(m => m.NetAmount).Name("net_amount");
        Map(m => m.ExceedsQuota).Name("exceeds_quota").TypeConverterOption.BooleanValues(true, true, "TRUE", "true", "yes", "1").TypeConverterOption.BooleanValues(false, true, "FALSE", "false", "no", "0");
        Map(m => m.TotalMonthlyQuota).Name("total_monthly_quota");
        Map(m => m.Organization).Name("organization");
        Map(m => m.CostCenterName).Name("cost_center_name");
    }
}
