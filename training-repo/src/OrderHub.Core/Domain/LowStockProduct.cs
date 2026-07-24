namespace OrderHub.Core.Domain;

public record LowStockProduct(Product Product, int SoldLast30Days);
