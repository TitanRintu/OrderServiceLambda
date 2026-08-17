using System.Text.Json;
using Amazon.Lambda.Core;

namespace AWSCompleteOrder;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--show")
        {
            var orders = await new ShowDataFunction(PostgreSqlOrderRepository.FromEnvironment())
                .FunctionHandler(null, new LocalLambdaContext());
            Console.WriteLine(JsonSerializer.Serialize(orders));
            return;
        }

        var orderName = ReadValue(args, 0, "Order name");
        var productName = ReadValue(args, 1, "Product name");
        var quantityText = ReadValue(args, 2, "Quantity");

        if (!int.TryParse(quantityText, out var quantity) || quantity <= 0)
        {
            Console.WriteLine("Quantity must be a positive number.");
            return;
        }

        var context = new LocalLambdaContext();
        var dispatchFunction = new DispatchFunction();
        var orderRepository = PostgreSqlOrderRepository.FromEnvironment();
        var orderFunction = new OrderFunction(request =>
        {
            var order = JsonSerializer.Deserialize<Order>(request.Payload)
                ?? throw new InvalidOperationException("Invalid dispatch payload.");

            dispatchFunction.FunctionHandler(order, context);
            return Task.CompletedTask;
        }, orderRepository);

        var createdOrder = await orderFunction.FunctionHandler(
            new OrderRequest(orderName, productName, quantity), context);

        Console.WriteLine($"Created order: {JsonSerializer.Serialize(createdOrder)}");
    }

    private static string ReadValue(string[] args, int index, string label)
    {
        if (args.Length > index)
        {
            return args[index];
        }

        Console.Write($"{label}: ");
        return Console.ReadLine() ?? string.Empty;
    }
}

internal sealed class LocalLambdaContext : ILambdaContext
{
    public string AwsRequestId => "local-request";
    public IClientContext ClientContext => null!;
    public string FunctionName => "AWSCompleteOrder";
    public string FunctionVersion => "local";
    public ICognitoIdentity Identity => null!;
    public string InvokedFunctionArn => "local";
    public ILambdaLogger Logger { get; } = new LocalLambdaLogger();
    public string LogGroupName => "local";
    public string LogStreamName => "local";
    public int MemoryLimitInMB => 256;
    public TimeSpan RemainingTime => TimeSpan.FromMinutes(5);
}

internal sealed class LocalLambdaLogger : ILambdaLogger
{
    public void Log(string message) => Console.Write(message);
    public void LogLine(string message) => Console.WriteLine(message);
}
