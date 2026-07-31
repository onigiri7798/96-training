using ModelContextProtocol.Server;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using System.ComponentModel;

[McpServerResourceType]
public class OrderHubResources(IOrderService orderService)
{
    // 折扣率直接向 OrderService 現查,不在這裡另存一份數字——避免規則改版時 resource 和程式碼各說各話
    [McpServerResource(UriTemplate = "orderhub://discount-rules",
        Name = "會員折扣規則", MimeType = "text/markdown")]
    [Description("目前生效的會員折扣規則與計算方式")]
    public string DiscountRules()
    {
        string Pct(CustomerTier tier) => $"{orderService.GetDiscountRate(tier):P0}";
        return $"""
            # OrderHub 會員折扣規則
            - Standard:不打折（{Pct(CustomerTier.Standard)}）
            - Silver:折扣 {Pct(CustomerTier.Silver)}
            - Gold:折扣 {Pct(CustomerTier.Gold)}
            折扣在訂單總額上折抵一次,單價快照(UnitPriceSnapshot)為下單當下原價。
            """;
    }
}
