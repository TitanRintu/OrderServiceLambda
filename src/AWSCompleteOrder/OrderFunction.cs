using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSCompleteOrder;

public class OrderFunction
{
    private readonly Func<InvokeRequest, Task> _invokeDispatch;
    private readonly IOrderRepository _orderRepository;

    public OrderFunction() : this(
        CreateDispatchInvoker(),
        PostgreSqlOrderRepository.FromEnvironment())
    {
    }

    public OrderFunction(
        Amazon.Lambda.IAmazonLambda lambdaClient,
        IOrderRepository orderRepository) : this(
        request => lambdaClient.InvokeAsync(request), orderRepository)
    {
    }

    public OrderFunction(
        Func<InvokeRequest, Task> invokeDispatch,
        IOrderRepository orderRepository)
    {
        _invokeDispatch = invokeDispatch;
        _orderRepository = orderRepository;
    }

    private static Func<InvokeRequest, Task> CreateDispatchInvoker()
    {
        if (string.Equals(
                System.Environment.GetEnvironmentVariable("AWS_SAM_LOCAL"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return request =>
            {
                var order = JsonSerializer.Deserialize<Order>(request.Payload)
                    ?? throw new InvalidOperationException("Invalid dispatch payload.");

                new DispatchFunction().FunctionHandler(order, new LocalLambdaContext());
                return Task.CompletedTask;
            };
        }

        var lambdaClient = new Amazon.Lambda.AmazonLambdaClient();
        return request => lambdaClient.InvokeAsync(request);
    }

    public async Task<Order> FunctionHandler(OrderRequest request, ILambdaContext context)
    {
        context.Logger.LogInformation(
            $"OrderFunction started. RequestId: {context.AwsRequestId}, Quantity: {request.Quantity}");

        var order = new Order(
            Guid.NewGuid().ToString(),
            request.OrderName,
            request.ProductName,
            request.Quantity);

        context.Logger.LogInformation($"Saving order {order.OrderId} to PostgreSQL.");
        await _orderRepository.SaveAsync(order);
        context.Logger.LogInformation($"Order {order.OrderId} saved successfully.");

        context.Logger.LogInformation($"Sending order {order.OrderId} to DispatchFunction.");
        await _invokeDispatch(new InvokeRequest
        {
            FunctionName = System.Environment.GetEnvironmentVariable("DISPATCH_FUNCTION_NAME") ?? "DispatchFunction",
            InvocationType = "Event",
            Payload = JsonSerializer.Serialize(order)
        });
        context.Logger.LogInformation($"Order {order.OrderId} accepted for dispatch.");

        return order;
    }

}
