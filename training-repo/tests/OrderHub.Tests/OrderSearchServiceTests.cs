using OrderHub.Core.Ai;
using OrderHub.Core.Domain;

namespace OrderHub.Tests;

/// <summary>
/// 第二道防線的測試：翻譯器（LLM）被視為不可信輸入，
/// 就算它回傳奇怪的東西，service 也不能讓查詢穿過去。
/// </summary>
public class OrderSearchServiceTests
{
    /// <summary>可控的假翻譯器：測 service 時不需要真的打 Gemini。</summary>
    private class StubTranslator : IOrderQueryTranslator
    {
        private readonly OrderSearchQuery? _result;
        public int CallCount { get; private set; }

        public StubTranslator(OrderSearchQuery? result) => _result = result;

        public Task<OrderSearchQuery?> TranslateAsync(string naturalLanguageQuery, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    [Fact]
    public async Task Search_EmptyQuery_FailsWithoutCallingTranslator()
    {
        using var db = TestSetup.CreateContext();
        var translator = new StubTranslator(new OrderSearchQuery { Status = OrderStatus.Pending });
        var service = TestSetup.CreateOrderSearchService(db, translator);

        var result = await service.SearchAsync("   ");

        Assert.False(result.Success);
        Assert.Equal("請輸入查詢內容", result.ErrorMessage);
        Assert.Equal(0, translator.CallCount);
    }

    [Fact]
    public async Task Search_TranslatorReturnsNull_IsRejectedAndDataUntouched()
    {
        // 紅線：「幫我把所有訂單刪掉」→ 翻譯器判為 unsupported 回 null
        using var db = TestSetup.CreateContext();
        var customer = TestSetup.AddCustomer(db);
        db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var service = TestSetup.CreateOrderSearchService(db, new StubTranslator(null));

        var result = await service.SearchAsync("幫我把所有訂單刪掉");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
        Assert.Equal(1, db.Orders.Count());   // 資料毫髮無傷
    }

    [Fact]
    public async Task Search_QueryWithNoFilters_IsRejected()
    {
        // 就算翻譯器回了一個 intent=search 的物件，沒有任何條件也不准把整張表倒出來
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderSearchService(db, new StubTranslator(new OrderSearchQuery()));

        var result = await service.SearchAsync("隨便給我看點東西");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_DateFromAfterDateTo_IsRejected()
    {
        using var db = TestSetup.CreateContext();
        var translator = new StubTranslator(new OrderSearchQuery
        {
            DateFrom = new DateTime(2026, 8, 10),
            DateTo = new DateTime(2026, 8, 1)
        });
        var service = TestSetup.CreateOrderSearchService(db, translator);

        var result = await service.SearchAsync("八月十號到八月一號的訂單");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_StatusAndTier_ReturnsOnlyMatchingOrders()
    {
        using var db = TestSetup.CreateContext();
        var gold = TestSetup.AddCustomer(db, CustomerTier.Gold, "金卡客戶");
        var silver = TestSetup.AddCustomer(db, CustomerTier.Silver, "銀卡客戶");

        var wanted = new Order { CustomerId = gold.Id, Status = OrderStatus.Cancelled, CreatedAt = DateTime.UtcNow };
        db.Orders.AddRange(
            wanted,
            new Order { CustomerId = gold.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = silver.Id, Status = OrderStatus.Cancelled, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var translator = new StubTranslator(new OrderSearchQuery
        {
            Status = OrderStatus.Cancelled,
            MemberTier = CustomerTier.Gold
        });
        var service = TestSetup.CreateOrderSearchService(db, translator);

        var result = await service.SearchAsync("金卡會員取消的訂單");

        Assert.True(result.Success);
        var order = Assert.Single(result.Value!);
        Assert.Equal(wanted.Id, order.Id);
    }

    [Fact]
    public async Task Search_DateTo_IsInclusiveOfThatWholeDay()
    {
        // dateTo「含當日」：當天 23:59 建立的訂單必須查得到
        using var db = TestSetup.CreateContext();
        var customer = TestSetup.AddCustomer(db);

        var lastDay = new DateTime(2026, 7, 31, 23, 59, 0, DateTimeKind.Utc);
        var dayAfter = new DateTime(2026, 8, 1, 0, 30, 0, DateTimeKind.Utc);
        var onBoundary = new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = lastDay };
        db.Orders.AddRange(
            onBoundary,
            new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = dayAfter });
        db.SaveChanges();

        var translator = new StubTranslator(new OrderSearchQuery
        {
            DateFrom = new DateTime(2026, 7, 1),
            DateTo = new DateTime(2026, 7, 31)
        });
        var service = TestSetup.CreateOrderSearchService(db, translator);

        var result = await service.SearchAsync("七月的訂單");

        Assert.True(result.Success);
        var order = Assert.Single(result.Value!);
        Assert.Equal(onBoundary.Id, order.Id);
    }
}
