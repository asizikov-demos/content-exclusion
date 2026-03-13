namespace DataProcessor.Infra.Csv;

using System.Globalization;
using CsvHelper;
using DataProcessor.Domain.Interfaces;
using DataProcessor.Domain.Models;

public sealed class CsvBillingParser : ICsvParser
{
    public IReadOnlyList<BillingRecord> Parse(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = CsvHelper.Configuration.TrimOptions.Trim,
        });

        csv.Context.RegisterClassMap<BillingRecordMap>();
        return csv.GetRecords<BillingRecord>().ToList();
    }
}
