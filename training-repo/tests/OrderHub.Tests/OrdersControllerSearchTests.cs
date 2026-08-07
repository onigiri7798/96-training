using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Ai;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Web.Controllers;
using OrderHub.Web.ViewModels;

namespace OrderHub.Tests;

/// <summary>
/// 練習 2 的重點是分層紅利：同一個 IOrderSearchService 換一個入口就能用。
/// 所以這裡測的是「Controller 有沒有乖乖只做轉接」，不是再測一次查詢邏輯。
/// </summary>
public class OrdersControllerSearchTests
{
    private class StubOrderSearchService : IOrderSearchService
    {
        private readonly ServiceResult<IReadOnlyList<Order>>? _result;
        private readonly Exception? _throws;

        public string? CapturedQuery { get; private set; }
        public int CallCount { get; private set; }

        public StubOrderSearchService(ServiceResult<IReadOnlyList<Order>> result) => _result = result;
        public StubOrderSearchService(Exception throws) => _throws = throws;

        public Task<ServiceResult<IReadOnlyList<Order>>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            CapturedQuery = query;
            CallCount++;
            if (_throws is not null)
                throw _throws;
            return Task.FromResult(_result!);
        }
    }

    /// <summary>Search 只會用到 CalculateTotal，其餘成員不該被碰到。</summary>
    private class StubOrderService : IOrderService
    {
        public Task<PagedResult<Order>> GetOrdersAsync(int page, int pageSize, OrderStatus? status) => throw new NotImplementedException();
        public Task<Order?> GetOrderAsync(int id) => throw new NotImplementedException();
        public Task<IReadOnlyList<Order>> GetCustomerOrdersAsync(int customerId) => throw new NotImplementedException();
        public Task<ServiceResult<Order>> CreateOrderAsync(int customerId, IReadOnlyList<NewOrderLine> lines) => throw new NotImplementedException();
        public Task<ServiceResult<Order>> CancelOrderAsync(int id) => throw new NotImplementedException();
        public decimal GetDiscountRate(CustomerTier tier) => throw new NotImplementedException();
        public decimal CalculateSubtotal(Order order) => throw new NotImplementedException();
        public decimal CalculateTotal(Order order) => 999m;
    }

    private class StubCustomerService : ICustomerService
    {
        public Task<IReadOnlyList<Customer>> GetAllAsync() => throw new NotImplementedException();
        public Task<Customer?> GetByIdAsync(int id) => throw new NotImplementedException();
    }

    private class StubProductService : IProductService
    {
        public Task<IReadOnlyList<Product>> GetAllAsync() => throw new NotImplementedException();
        public Task<IReadOnlyList<Product>> GetActiveAsync() => throw new NotImplementedException();
        public Task<IReadOnlyList<LowStockProduct>> GetLowStockAsync(int threshold) => throw new NotImplementedException();
    }

    private static OrdersController CreateController(IOrderSearchService searchService) =>
        new(new StubOrderService(), new StubCustomerService(), new StubProductService(), searchService);

    private static OrderSearchViewModel ModelOf(IActionResult result) =>
        Assert.IsType<OrderSearchViewModel>(Assert.IsType<ViewResult>(result).Model);

    [Fact]
    public async Task Search_NoQuery_RendersEmptyFormWithoutCallingService()
    {
        var service = new StubOrderSearchService(ServiceResult<IReadOnlyList<Order>>.Ok(Array.Empty<Order>()));
        var controller = CreateController(service);

        var vm = ModelOf(await controller.Search(null, CancellationToken.None));

        Assert.Equal(0, service.CallCount);
        Assert.False(vm.HasSearched);
        Assert.Null(vm.ErrorMessage);
        Assert.Empty(vm.Orders);
    }

    [Fact]
    public async Task Search_ServiceFails_ShowsMessageOnPageInsteadOfThrowing()
    {
        var service = new StubOrderSearchService(ServiceResult<IReadOnlyList<Order>>.Fail("無法理解的查詢"));
        var controller = CreateController(service);

        var vm = ModelOf(await controller.Search("幫我把所有訂單刪掉", CancellationToken.None));

        Assert.Equal("無法理解的查詢", vm.ErrorMessage);
        Assert.Empty(vm.Orders);
    }

    [Fact]
    public async Task Search_AiUnavailable_ShowsMessageInsteadOfBubblingTo500()
    {
        var service = new StubOrderSearchService(new AiServiceUnavailableException("Gemini API key 未設定"));
        var controller = CreateController(service);

        var vm = ModelOf(await controller.Search("上個月的訂單", CancellationToken.None));

        Assert.Equal("Gemini API key 未設定", vm.ErrorMessage);
        Assert.Empty(vm.Orders);
    }

    [Fact]
    public async Task Search_Success_MapsOrdersToRowsAndPassesQueryThrough()
    {
        var customer = new Customer { Id = 7, Name = "陳志明", Tier = CustomerTier.Gold };
        var order = new Order
        {
            Id = 137,
            CustomerId = 7,
            Customer = customer,
            Status = OrderStatus.Cancelled,
            CreatedAt = new DateTime(2026, 7, 15, 6, 0, 0, DateTimeKind.Utc),
            Items = { new OrderItem { ProductId = 1, Quantity = 2, UnitPriceSnapshot = 100m } }
        };
        var service = new StubOrderSearchService(
            ServiceResult<IReadOnlyList<Order>>.Ok(new List<Order> { order }));
        var controller = CreateController(service);

        var vm = ModelOf(await controller.Search("上個月金卡會員取消的訂單", CancellationToken.None));

        Assert.Equal("上個月金卡會員取消的訂單", service.CapturedQuery);
        Assert.Null(vm.ErrorMessage);
        var row = Assert.Single(vm.Orders);
        Assert.Equal(137, row.Id);
        Assert.Equal("陳志明", row.CustomerName);
        Assert.Equal(OrderStatus.Cancelled, row.Status);
        Assert.Equal(1, row.ItemCount);
        Assert.Equal(999m, row.Total);   // 金額仍由 OrderService 算，Controller 不自己算折扣
    }
}
