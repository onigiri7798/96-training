using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderHub.Core.Interfaces;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Data;
using OrderHub.Infrastructure.Repositories;

// Same tools/resources/prompts either way — only the transport differs.
if (args.Contains("--http"))
{
    // HTTP: for remote clients such as n8n, whose MCP node speaks SSE / streamable HTTP but not stdio.
    var builder = WebApplication.CreateBuilder(args);

    AddOrderHubServices(builder.Services, builder.Configuration);

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options => options.Stateless = true)
        .WithTools<OrderHubTools>()
        .WithResources<OrderHubResources>()
        .WithPrompts<OrderHubPrompts>();

    var app = builder.Build();
    app.MapMcp();
    await app.RunAsync("http://localhost:3001");
}
else
{
    var builder = Host.CreateApplicationBuilder(args);

    // stdout is the MCP protocol channel — all logging must go to stderr
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    AddOrderHubServices(builder.Services, builder.Configuration);

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<OrderHubTools>()
        .WithResources<OrderHubResources>()
        .WithPrompts<OrderHubPrompts>();

    await builder.Build().RunAsync();
}

static void AddOrderHubServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<OrderHubDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("Default")
            ?? "Server=localhost;Database=OrderHubTraining;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"));

    services.AddScoped<ICustomerRepository, CustomerRepository>();
    services.AddScoped<IProductRepository, ProductRepository>();
    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<IOrderService, OrderService>();
}
