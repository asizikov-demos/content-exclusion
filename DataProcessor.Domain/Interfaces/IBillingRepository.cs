namespace DataProcessor.Domain.Interfaces;

using DataProcessor.Domain.Models;

public interface IBillingRepository : IDisposable
{
    void Initialize();
    void InsertRecords(IReadOnlyList<BillingRecord> records);
    SummaryResult GetSummary();
    UserSummaryResult? GetUserSummary(string username);
    IReadOnlyList<string> GetAllUsernames();
}
