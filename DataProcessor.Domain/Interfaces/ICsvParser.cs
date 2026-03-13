namespace DataProcessor.Domain.Interfaces;

using DataProcessor.Domain.Models;

public interface ICsvParser
{
    IReadOnlyList<BillingRecord> Parse(string filePath);
}
