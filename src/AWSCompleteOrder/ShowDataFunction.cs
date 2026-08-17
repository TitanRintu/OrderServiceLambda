using Amazon.Lambda.Core;

namespace AWSCompleteOrder;

public class ShowDataFunction
{
    private readonly IOrderRepository _orderRepository;

    public ShowDataFunction() : this(PostgreSqlOrderRepository.FromEnvironment())
    {
    }

    public ShowDataFunction(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<List<Order>> FunctionHandler(object? request, ILambdaContext context)
    {
        context.Logger.LogInformation(
            $"ShowDataFunction started. RequestId: {context.AwsRequestId}");

        var orders = await _orderRepository.GetAllAsync();

        context.Logger.LogInformation($"ShowDataFunction returned {orders.Count} orders.");
        return orders;
    }
}
