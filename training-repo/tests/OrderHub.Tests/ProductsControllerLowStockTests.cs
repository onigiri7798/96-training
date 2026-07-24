using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Web.Controllers;
using OrderHub.Web.ViewModels;

namespace OrderHub.Tests;

public class ProductsControllerLowStockTests
{
    private class StubProductService : IProductService
    {
        public int? CapturedThreshold { get; private set; }

        public Task<IReadOnlyList<Product>> GetAllAsync() => throw new NotImplementedException();
        public Task<IReadOnlyList<Product>> GetActiveAsync() => throw new NotImplementedException();

        public Task<IReadOnlyList<LowStockProduct>> GetLowStockAsync(int threshold)
        {
            CapturedThreshold = threshold;
            return Task.FromResult<IReadOnlyList<LowStockProduct>>(Array.Empty<LowStockProduct>());
        }
    }

    [Fact]
    public async Task LowStock_ThresholdOmitted_QueriesWithDefaultOfTen()
    {
        var service = new StubProductService();
        var controller = new ProductsController(service);
        // A vm bound from a query string with no "threshold" key stays at its property default (10),
        // matching how ComplexObjectModelBinder only overwrites keys actually present.
        var vm = new LowStockViewModel();

        var result = await controller.LowStock(vm);

        Assert.Equal(10, service.CapturedThreshold);
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Same(vm, viewResult.Model);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task LowStock_InvalidModelState_SkipsQueryAndReturnsEmptyProducts(int threshold)
    {
        var service = new StubProductService();
        var controller = new ProductsController(service);
        var vm = new LowStockViewModel { Threshold = threshold };
        controller.ModelState.AddModelError(nameof(vm.Threshold), "門檻必須大於 0");

        var result = await controller.LowStock(vm);

        Assert.Null(service.CapturedThreshold);
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Empty(((LowStockViewModel)viewResult.Model!).Products);
    }
}
