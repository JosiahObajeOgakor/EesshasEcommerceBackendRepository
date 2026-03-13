using System;
using System.Collections.Generic;

namespace Easshas.WebApi.Dtos
{
    public record OrderItemDto(string Name, int Quantity, decimal UnitPrice, decimal Total);

    public record PaymentDto(string Reference, string Status, DateTime? PaidAt);

    public record OrderDto(Guid Id, string Status, decimal TotalAmount, string Currency, DateTime CreatedAt, DateTime? ExpectedDeliveryDate, List<OrderItemDto> Items, PaymentDto? Payment);
}
