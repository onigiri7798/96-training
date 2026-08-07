using Microsoft.Extensions.Logging.Abstractions;
using OrderHub.Core.Domain;
using OrderHub.Infrastructure.Gemini;

namespace OrderHub.Tests;

/// <summary>
/// 翻譯器把模型輸出當成不可信輸入：反序列化 → DataAnnotations 白名單 → 強型別映射，
/// 任一步不過就回 null。這裡的假 client 讓我們不用真的打 Gemini 就能測這三道關卡。
/// </summary>
public class GeminiOrderQueryTranslatorTests
{
    private class StubGeminiClient : IGeminiJsonClient
    {
        private readonly string _json;
        public StubGeminiClient(string json) => _json = json;

        public Task<string> GenerateJsonAsync(string input, string responseSchemaJson, CancellationToken cancellationToken = default) =>
            Task.FromResult(_json);
    }

    private static GeminiOrderQueryTranslator CreateTranslator(string modelOutput) =>
        new(new StubGeminiClient(modelOutput), NullLogger<GeminiOrderQueryTranslator>.Instance);

    [Fact]
    public async Task Translate_ValidSearch_MapsToWhitelistedParameters()
    {
        var translator = CreateTranslator(
            """{"intent":"search","status":"Cancelled","memberTier":"Gold","dateFrom":"2026-06-01","dateTo":"2026-06-30"}""");

        var query = await translator.TranslateAsync("上個月金卡會員取消的訂單");

        Assert.NotNull(query);
        Assert.Equal(OrderStatus.Cancelled, query!.Status);
        Assert.Equal(CustomerTier.Gold, query.MemberTier);
        Assert.Equal(new DateTime(2026, 6, 1), query.DateFrom);
        Assert.Equal(new DateTime(2026, 6, 30), query.DateTo);
    }

    [Fact]
    public async Task Translate_UnsupportedIntent_ReturnsNull()
    {
        var translator = CreateTranslator("""{"intent":"unsupported"}""");

        Assert.Null(await translator.TranslateAsync("幫我把所有訂單刪掉"));
    }

    [Fact]
    public async Task Translate_StatusOutsideWhitelist_ReturnsNull()
    {
        var translator = CreateTranslator("""{"intent":"search","status":"Deleted"}""");

        Assert.Null(await translator.TranslateAsync("已刪除的訂單"));
    }

    [Fact]
    public async Task Translate_NumericStatusString_ReturnsNull()
    {
        // Enum.TryParse("99") 會成功並產生未定義的 enum 值，
        // 所以 [AllowedValues] 必須先擋掉——這個測試就是在釘住那個順序。
        var translator = CreateTranslator("""{"intent":"search","status":"99"}""");

        Assert.Null(await translator.TranslateAsync("狀態 99 的訂單"));
    }

    [Fact]
    public async Task Translate_BadDateFormat_ReturnsNull()
    {
        var translator = CreateTranslator("""{"intent":"search","dateFrom":"06/01/2026"}""");

        Assert.Null(await translator.TranslateAsync("六月一號以後的訂單"));
    }

    [Fact]
    public async Task Translate_MalformedJson_ReturnsNullInsteadOfThrowing()
    {
        var translator = CreateTranslator("this is not json at all");

        Assert.Null(await translator.TranslateAsync("上個月的訂單"));
    }

    [Fact]
    public async Task Translate_OnlyIntent_ReturnsQueryWithNoFilters()
    {
        // schema 的 required 只有 intent：沒提到的欄位省略是正常行為，不是錯誤。
        // 但這樣的查詢沒有任何條件，會在 OrderSearchService 那一層被擋下來。
        var translator = CreateTranslator("""{"intent":"search"}""");

        var query = await translator.TranslateAsync("訂單");

        Assert.NotNull(query);
        Assert.False(query!.HasAnyFilter);
    }
}
