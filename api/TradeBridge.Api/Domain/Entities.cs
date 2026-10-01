namespace TradeBridge.Api.Domain;

public sealed class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Iec { get; set; }
    public string? Gstin { get; set; }
    public string? ApedaRegistration { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class Shipment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Reference { get; set; }
    public Guid BuyerId { get; set; }
    public Organization Buyer { get; set; } = null!;
    public required string Commodity { get; set; }
    public required string Origin { get; set; }
    public required string Destination { get; set; }
    public string Status { get; set; } = "Draft";
    public string StatusSource { get; set; } = "TradeBridge";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<ShipmentEvent> Events { get; set; } = [];
}
public sealed class ShipmentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShipmentId { get; set; }
    public required string EventType { get; set; }
    public required string Description { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public required string Source { get; set; }
    public string? ExternalReference { get; set; }
    public Guid? RecordedByUserId { get; set; }
}
public sealed class TradeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShipmentId { get; set; }
    public required string DocumentType { get; set; }
    public required string ObjectKey { get; set; }
    public string Status { get; set; } = "Uploaded";
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class PaymentMilestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShipmentId { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public string Status { get; set; } = "Pending";
    public string ReleaseCondition { get; set; } = "";
    public DateTimeOffset? ReleasedAtUtc { get; set; }
}
