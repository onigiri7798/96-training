using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task GetAll_ReturnsAllProductsIncludingInactive()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetAllAsync();

        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task GetActive_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetActiveAsync();

        Assert.All(products, p => Assert.True(p.IsActive));
        Assert.Single(products);
    }

    [Fact]
    public async Task GetLowStock_FiltersByThresholdAndSortsAscending()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var low = TestSetup.AddProduct(db, sku: "SKU-LOW", stock: 3);
        var mid = TestSetup.AddProduct(db, sku: "SKU-MID", stock: 7);
        TestSetup.AddProduct(db, sku: "SKU-HIGH", stock: 50);

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(2, result.Count);
        Assert.Equal(low.Id, result[0].Product.Id);
        Assert.Equal(mid.Id, result[1].Product.Id);
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-INACTIVE", stock: 2, isActive: false);
        var active = TestSetup.AddProduct(db, sku: "SKU-ACTIVE", stock: 3);

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal(active.Id, result[0].Product.Id);
    }

    [Fact]
    public async Task GetLowStock_SoldLast30Days_ExcludesCancelledOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 3);

        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 4, UnitPriceSnapshot = product.UnitPrice } }
        });
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Cancelled,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 100, UnitPriceSnapshot = product.UnitPrice } }
        });
        db.SaveChanges();

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(4, result.Single().SoldLast30Days);
    }

    [Fact]
    public async Task GetLowStock_SoldLast30Days_ExcludesOrdersOlderThan30Days()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 3);

        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-40),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 9, UnitPriceSnapshot = product.UnitPrice } }
        });
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 2, UnitPriceSnapshot = product.UnitPrice } }
        });
        db.SaveChanges();

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(2, result.Single().SoldLast30Days);
    }
}
