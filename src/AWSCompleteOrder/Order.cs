namespace AWSCompleteOrder;

public record Order(string OrderId, string OrderName, string ProductName, int Quantity);

public record OrderRequest(string OrderName, string ProductName, int Quantity);
