using Amazon.Lambda.Core;

namespace AWSCompleteOrder;

public class DispatchFunction
{
    public void FunctionHandler(Order order, ILambdaContext context)
    {
        context.Logger.LogInformation($"Dispatching order: {order.OrderId} and Order UUID {Guid.NewGuid()}");
    }
}
