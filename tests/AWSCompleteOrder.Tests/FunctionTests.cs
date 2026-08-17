using System.Text.Json;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestUtilities;
using Moq;
using Xunit;

namespace AWSCompleteOrder.Tests;

public class FunctionTests
{
    [Fact]
    public async Task OrderFunctionCreatesAndDispatchesOrder()
    {
        InvokeRequest? dispatchedRequest = null;
        var lambdaClient = new Mock<IAmazonLambda>();
        lambdaClient
            .Setup(client => client.InvokeAsync(It.IsAny<InvokeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<InvokeRequest, CancellationToken>((request, _) => dispatchedRequest = request)
            .ReturnsAsync(new InvokeResponse());
        var orderRepository = new Mock<IOrderRepository>();

        var function = new OrderFunction(lambdaClient.Object, orderRepository.Object);
        var order = await function.FunctionHandler(
            new OrderRequest("Sample order", "Keyboard", 2),
            new TestLambdaContext());

        Assert.False(string.IsNullOrWhiteSpace(order.OrderId));
        Assert.Equal("Sample order", order.OrderName);
        Assert.Equal("Keyboard", order.ProductName);
        Assert.Equal(2, order.Quantity);
        orderRepository.Verify(repository => repository.SaveAsync(order), Times.Once);
        Assert.Equal("DispatchFunction", dispatchedRequest?.FunctionName);
        Assert.Equal("Event", dispatchedRequest?.InvocationType);

        var dispatchedOrder = JsonSerializer.Deserialize<Order>(dispatchedRequest!.Payload);
        Assert.Equal(order, dispatchedOrder);
    }

    [Fact]
    public void DispatchFunctionLogsOrderId()
    {
        var context = new TestLambdaContext();
        var order = new Order("order-123", "Sample order", "Keyboard", 2);

        new DispatchFunction().FunctionHandler(order, context);

        var logger = Assert.IsType<TestLambdaLogger>(context.Logger);
        Assert.Contains("order-123", logger.Buffer.ToString());
    }

    [Fact]
    public async Task ShowDataFunctionReturnsOrders()
    {
        var expected = new List<Order>
        {
            new("order-123", "Sample order", "Keyboard", 2)
        };
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(repository => repository.GetAllAsync()).ReturnsAsync(expected);

        var result = await new ShowDataFunction(orderRepository.Object)
            .FunctionHandler(null, new TestLambdaContext());

        Assert.Equal(expected, result);
    }

}
