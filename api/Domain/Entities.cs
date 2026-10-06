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
public sealed class TradeUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public required string FullName { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DisabledAtUtc { get; set; }
}
public sealed class AccessSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
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

// Workflow records keep sensitive values separate from the public RFQ projection.
// EncryptedPayload/ObjectKey fields must contain ciphertext or private object-store keys only.
public sealed class TradeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Reference { get; set; }
    public Guid ImporterOrganizationId { get; set; }
    public required string Product { get; set; }
    public required string Quantity { get; set; }
    public required string Incoterms { get; set; }
    public DateOnly TargetShipmentDate { get; set; }
    public required string OriginPort { get; set; }
    public required string DestinationPort { get; set; }
    public string RequiredCertifications { get; set; } = "";
    public string Status { get; set; } = "Open";
    public byte[]? EncryptedPrivateDetails { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TradeBid
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TradeRequestId { get; set; }
    public Guid ExporterOrganizationId { get; set; }
    public required byte[] EncryptedPayload { get; set; }
    public string Status { get; set; } = "Sealed";
    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class KycRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string DocumentType { get; set; }
    public required string EncryptedContent { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ReviewedByUserId { get; set; }
}

public sealed class TradeMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TradeRequestId { get; set; }
    public Guid SenderUserId { get; set; }
    public Guid? RecipientUserId { get; set; }
    public required byte[] EncryptedBody { get; set; }
    public string Status { get; set; } = "PendingCHAReview";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TradeContract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TradeRequestId { get; set; }
    public Guid SelectedBidId { get; set; }
    public required byte[] EncryptedTerms { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DocumentAccessLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TradeDocumentId { get; set; }
    public required string ResourceType { get; set; }
    public Guid ResourceId { get; set; }
    public Guid ActorUserId { get; set; }
    public required string Action { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ShippingDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TradeRequestId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public required string DocumentType { get; set; }
    public required string ContentType { get; set; }
    public required string EncryptedContent { get; set; }
    public string Status { get; set; } = "PendingCHAReview";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Settlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TradeRequestId { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal ChaCommission { get; set; }
    public decimal PlatformFee { get; set; }
    public decimal ExporterAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "PendingGateway";
    public string? GatewayReference { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
