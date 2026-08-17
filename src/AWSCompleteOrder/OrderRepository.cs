using Amazon;
using Amazon.RDS.Util;
using Npgsql;

namespace AWSCompleteOrder;

public interface IOrderRepository
{
    Task SaveAsync(Order order);
    Task<List<Order>> GetAllAsync();
}

public sealed class PostgreSqlOrderRepository : IOrderRepository
{
    private readonly Func<string> _connectionStringFactory;

    public PostgreSqlOrderRepository(string connectionString)
    {
        _connectionStringFactory = () => connectionString;
    }

    private PostgreSqlOrderRepository(Func<string> connectionStringFactory)
    {
        _connectionStringFactory = connectionStringFactory;
    }

    public static PostgreSqlOrderRepository FromEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new PostgreSqlOrderRepository(connectionString);
        }

        var host = RequiredEnvironmentVariable("RDS_ENDPOINT");
        var database = RequiredEnvironmentVariable("RDS_DATABASE");
        var username = RequiredEnvironmentVariable("RDS_USERNAME");
        var region = RequiredEnvironmentVariable("AWS_REGION");
        var port = int.TryParse(Environment.GetEnvironmentVariable("RDS_PORT"), out var configuredPort)
            ? configuredPort
            : 5432;

        return new PostgreSqlOrderRepository(() =>
        {
            var token = RDSAuthTokenGenerator.GenerateAuthToken(
                RegionEndpoint.GetBySystemName(region), host, port, username);

            return new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = database,
                Username = username,
                Password = token,
                SslMode = SslMode.Require,
                SslNegotiation = SslNegotiation.Direct,
                Timeout = 60
            }.ConnectionString;
        });
    }

    private static string RequiredEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} is not configured.");
    }

    public async Task SaveAsync(Order order)
    {
        const string sql = """
            INSERT INTO orders (order_id, order_name, product_name, quantity)
            VALUES (@orderId, @orderName, @productName, @quantity);
            """;

        await using var connection = new NpgsqlConnection(_connectionStringFactory());
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("orderId", order.OrderId);
        command.Parameters.AddWithValue("orderName", order.OrderName);
        command.Parameters.AddWithValue("productName", order.ProductName);
        command.Parameters.AddWithValue("quantity", order.Quantity);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Order>> GetAllAsync()
    {
        const string sql = """
            SELECT order_id, order_name, product_name, quantity
            FROM orders
            ORDER BY order_name;
            """;

        await using var connection = new NpgsqlConnection(_connectionStringFactory());
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var orders = new List<Order>();

        while (await reader.ReadAsync())
        {
            orders.Add(new Order(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)));
        }

        return orders;
    }
}
