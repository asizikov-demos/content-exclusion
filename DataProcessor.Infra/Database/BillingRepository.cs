namespace DataProcessor.Infra.Database;

using Microsoft.Data.Sqlite;
using DataProcessor.Domain.Interfaces;
using DataProcessor.Domain.Models;

public sealed class BillingRepository : IBillingRepository
{
    private readonly SqliteConnection _connection;

    public BillingRepository(string databasePath)
    {
        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
    }

    public void Initialize()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS billing_records (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                date TEXT NOT NULL,
                username TEXT NOT NULL,
                product TEXT NOT NULL,
                sku TEXT NOT NULL,
                model TEXT NOT NULL,
                quantity INTEGER NOT NULL,
                unit_type TEXT NOT NULL,
                applied_cost_per_quantity REAL NOT NULL,
                gross_amount REAL NOT NULL,
                discount_amount REAL NOT NULL,
                net_amount REAL NOT NULL,
                exceeds_quota INTEGER NOT NULL,
                total_monthly_quota INTEGER NOT NULL,
                organization TEXT NOT NULL,
                cost_center_name TEXT NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();
    }

    public void InsertRecords(IReadOnlyList<BillingRecord> records)
    {
        using var transaction = _connection.BeginTransaction();

        foreach (var record in records)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO billing_records (date, username, product, sku, model, quantity, unit_type,
                    applied_cost_per_quantity, gross_amount, discount_amount, net_amount,
                    exceeds_quota, total_monthly_quota, organization, cost_center_name)
                VALUES ($date, $username, $product, $sku, $model, $quantity, $unitType,
                    $appliedCost, $gross, $discount, $net, $excedsQuota, $monthlyQuota, $org, $costCenter)
                """;
            cmd.Parameters.AddWithValue("$date", record.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$username", record.Username);
            cmd.Parameters.AddWithValue("$product", record.Product);
            cmd.Parameters.AddWithValue("$sku", record.Sku);
            cmd.Parameters.AddWithValue("$model", record.Model);
            cmd.Parameters.AddWithValue("$quantity", record.Quantity);
            cmd.Parameters.AddWithValue("$unitType", record.UnitType);
            cmd.Parameters.AddWithValue("$appliedCost", (double)record.AppliedCostPerQuantity);
            cmd.Parameters.AddWithValue("$gross", (double)record.GrossAmount);
            cmd.Parameters.AddWithValue("$discount", (double)record.DiscountAmount);
            cmd.Parameters.AddWithValue("$net", (double)record.NetAmount);
            cmd.Parameters.AddWithValue("$excedsQuota", record.ExceedsQuota ? 1 : 0);
            cmd.Parameters.AddWithValue("$monthlyQuota", record.TotalMonthlyQuota);
            cmd.Parameters.AddWithValue("$org", record.Organization);
            cmd.Parameters.AddWithValue("$costCenter", record.CostCenterName);
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public SummaryResult GetSummary()
    {
        // Main aggregates
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                COALESCE(SUM(gross_amount), 0) AS total_gross,
                COALESCE(SUM(discount_amount), 0) AS total_discount,
                COALESCE(SUM(net_amount), 0) AS total_net,
                COUNT(*) AS total_records,
                COUNT(DISTINCT username) AS unique_users
            FROM billing_records
            """;

        using var reader = cmd.ExecuteReader();
        reader.Read();

        var totalGross = reader.GetDecimal(0);
        var totalDiscount = reader.GetDecimal(1);
        var totalNet = reader.GetDecimal(2);
        var totalRecords = reader.GetInt32(3);
        var uniqueUsers = reader.GetInt32(4);
        reader.Close();

        // Cost center breakdown
        using var ccCmd = _connection.CreateCommand();
        ccCmd.CommandText = """
            SELECT cost_center_name, SUM(net_amount) AS total_net, COUNT(*) AS record_count
            FROM billing_records
            GROUP BY cost_center_name
            ORDER BY total_net DESC
            """;

        var costCenters = new List<CostCenterSummary>();
        using var ccReader = ccCmd.ExecuteReader();
        while (ccReader.Read())
        {
            costCenters.Add(new CostCenterSummary
            {
                CostCenterName = ccReader.GetString(0),
                TotalNetAmount = ccReader.GetDecimal(1),
                RecordCount = ccReader.GetInt32(2),
            });
        }

        return new SummaryResult
        {
            TotalGrossAmount = totalGross,
            TotalDiscountAmount = totalDiscount,
            TotalNetAmount = totalNet,
            TotalRecords = totalRecords,
            UniqueUserCount = uniqueUsers,
            CostCenters = costCenters,
        };
    }

    public UserSummaryResult? GetUserSummary(string username)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                COALESCE(SUM(gross_amount), 0),
                COALESCE(SUM(discount_amount), 0),
                COALESCE(SUM(net_amount), 0),
                COUNT(*)
            FROM billing_records
            WHERE username = $username
            """;
        cmd.Parameters.AddWithValue("$username", username);

        using var reader = cmd.ExecuteReader();
        reader.Read();

        var totalRecords = reader.GetInt32(3);
        if (totalRecords == 0)
        {
            return null;
        }

        var totalGross = reader.GetDecimal(0);
        var totalDiscount = reader.GetDecimal(1);
        var totalNet = reader.GetDecimal(2);
        reader.Close();

        // Product breakdown
        using var pCmd = _connection.CreateCommand();
        pCmd.CommandText = """
            SELECT product, sku, SUM(quantity) AS total_qty, SUM(net_amount) AS total_net
            FROM billing_records
            WHERE username = $username
            GROUP BY product, sku
            ORDER BY total_net DESC
            """;
        pCmd.Parameters.AddWithValue("$username", username);

        var products = new List<ProductUsage>();
        using var pReader = pCmd.ExecuteReader();
        while (pReader.Read())
        {
            products.Add(new ProductUsage
            {
                Product = pReader.GetString(0),
                Sku = pReader.GetString(1),
                TotalQuantity = pReader.GetInt32(2),
                TotalNetAmount = pReader.GetDecimal(3),
            });
        }

        return new UserSummaryResult
        {
            Username = username,
            TotalGrossAmount = totalGross,
            TotalDiscountAmount = totalDiscount,
            TotalNetAmount = totalNet,
            TotalRecords = totalRecords,
            Products = products,
        };
    }

    public IReadOnlyList<string> GetAllUsernames()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT username FROM billing_records ORDER BY username";

        var usernames = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            usernames.Add(reader.GetString(0));
        }
        return usernames;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
