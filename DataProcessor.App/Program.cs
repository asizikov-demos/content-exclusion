using DataProcessor.Domain.Models;
using DataProcessor.Infra.Csv;
using DataProcessor.Infra.Database;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: DataProcessor <csv-file-path>");
    Console.Error.WriteLine("  Provide the path to a Premium Request billing report CSV file.");
    return 1;
}

var filePath = args[0];
if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"Error: File not found: {filePath}");
    return 1;
}

Console.WriteLine($"📂 Reading CSV file: {filePath}");

var parser = new CsvBillingParser();
IReadOnlyList<BillingRecord> records;
try
{
    records = parser.Parse(filePath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error parsing CSV: {ex.Message}");
    return 1;
}

Console.WriteLine($"✅ Parsed {records.Count} records.");

var dbPath = Path.Combine(Path.GetTempPath(), "DataProcessor_billing.db");

// Remove stale DB from previous runs
if (File.Exists(dbPath))
{
    File.Delete(dbPath);
}

using var repository = new BillingRepository(dbPath);
repository.Initialize();
repository.InsertRecords(records);

Console.WriteLine($"💾 Stored {records.Count} records in database.");
Console.WriteLine();
PrintHelp();
Console.WriteLine();

while (true)
{
    Console.Write("dataprocessor> ");
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input))
        continue;

    if (input is "/quit" or "/exit")
    {
        Console.WriteLine("Goodbye!");
        break;
    }

    if (input == "/help")
    {
        PrintHelp();
        continue;
    }

    if (input == "/summary")
    {
        PrintSummary(repository);
        continue;
    }

    if (input.StartsWith("/summary-per-user"))
    {
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            // List available users
            var usernames = repository.GetAllUsernames();
            Console.WriteLine($"\n📋 Available users ({usernames.Count}):");
            foreach (var name in usernames)
            {
                Console.WriteLine($"  • {name}");
            }
            Console.WriteLine($"\nUsage: /summary-per-user <username>");
            continue;
        }
        PrintUserSummary(repository, parts[1]);
        continue;
    }

    Console.WriteLine($"Unknown command: {input}. Type /help for available commands.");
}

return 0;

static void PrintHelp()
{
    Console.WriteLine("Available commands:");
    Console.WriteLine("  /summary                        — Overall billing summary");
    Console.WriteLine("  /summary-per-user <username>    — Per-user billing summary");
    Console.WriteLine("  /summary-per-user               — List all usernames");
    Console.WriteLine("  /help                           — Show this help");
    Console.WriteLine("  /quit                           — Exit the application");
}

static void PrintSummary(BillingRepository repository)
{
    var summary = repository.GetSummary();

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine("              📊 BILLING SUMMARY");
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine($"  Total Records:      {summary.TotalRecords:N0}");
    Console.WriteLine($"  Unique Users:       {summary.UniqueUserCount:N0}");
    Console.WriteLine($"  Gross Amount:       ${summary.TotalGrossAmount:N2}");
    Console.WriteLine($"  Discount Amount:    ${summary.TotalDiscountAmount:N2}");
    Console.WriteLine($"  Net Amount (USD):   ${summary.TotalNetAmount:N2}");
    Console.WriteLine("───────────────────────────────────────────────────");
    Console.WriteLine("  Cost Centers:");

    foreach (var cc in summary.CostCenters)
    {
        Console.WriteLine($"    • {cc.CostCenterName,-30} ${cc.TotalNetAmount,12:N2}  ({cc.RecordCount} records)");
    }

    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine();
}

static void PrintUserSummary(BillingRepository repository, string username)
{
    var result = repository.GetUserSummary(username);
    if (result is null)
    {
        Console.WriteLine($"\n⚠️  No records found for user: {username}");
        Console.WriteLine("Use /summary-per-user (without arguments) to see available usernames.\n");
        return;
    }

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine($"         👤 USER SUMMARY: {result.Username}");
    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine($"  Total Records:      {result.TotalRecords:N0}");
    Console.WriteLine($"  Gross Amount:       ${result.TotalGrossAmount:N2}");
    Console.WriteLine($"  Discount Amount:    ${result.TotalDiscountAmount:N2}");
    Console.WriteLine($"  Net Amount (USD):   ${result.TotalNetAmount:N2}");
    Console.WriteLine("───────────────────────────────────────────────────");
    Console.WriteLine("  Products:");

    foreach (var p in result.Products)
    {
        Console.WriteLine($"    • {p.Product,-25} (SKU: {p.Sku})");
        Console.WriteLine($"      Quantity: {p.TotalQuantity:N0}    Net: ${p.TotalNetAmount:N2}");
    }

    Console.WriteLine("═══════════════════════════════════════════════════");
    Console.WriteLine();
}
