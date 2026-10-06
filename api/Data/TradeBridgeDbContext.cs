using Microsoft.EntityFrameworkCore;
using TradeBridge.Api.Domain;

namespace TradeBridge.Api.Data;

public sealed class TradeBridgeDbContext(DbContextOptions<TradeBridgeDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<TradeUser> TradeUsers => Set<TradeUser>();
    public DbSet<AccessSession> AccessSessions => Set<AccessSession>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();
    public DbSet<TradeDocument> TradeDocuments => Set<TradeDocument>();
    public DbSet<PaymentMilestone> PaymentMilestones => Set<PaymentMilestone>();
    public DbSet<TradeRequest> TradeRequests => Set<TradeRequest>();
    public DbSet<TradeBid> TradeBids => Set<TradeBid>();
    public DbSet<KycRecord> KycRecords => Set<KycRecord>();
    public DbSet<TradeMessage> TradeMessages => Set<TradeMessage>();
    public DbSet<TradeContract> TradeContracts => Set<TradeContract>();
    public DbSet<DocumentAccessLog> DocumentAccessLogs => Set<DocumentAccessLog>();
    public DbSet<ShippingDocument> ShippingDocuments => Set<ShippingDocument>();
    public DbSet<Settlement> Settlements => Set<Settlement>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Organization>(e => { e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(200).IsRequired(); e.Property(x => x.Iec).HasMaxLength(10); e.Property(x => x.Gstin).HasMaxLength(15); e.HasIndex(x => x.Iec).IsUnique().HasFilter("\"Iec\" IS NOT NULL"); e.HasIndex(x => x.Gstin); });
        model.Entity<TradeUser>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.Email).IsUnique(); e.HasIndex(x => x.PhoneNumber).IsUnique(); e.Property(x => x.Email).HasMaxLength(320); e.Property(x => x.PhoneNumber).HasMaxLength(16); e.Property(x => x.FullName).HasMaxLength(200).IsRequired(); e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired(); e.Property(x => x.Role).HasMaxLength(40).IsRequired(); });
        model.Entity<AccessSession>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.TokenHash).IsUnique(); e.HasIndex(x => new { x.UserId, x.ExpiresAtUtc }); e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired(); });
        model.Entity<Shipment>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.Reference).IsUnique(); e.HasIndex(x => new { x.BuyerId, x.UpdatedAtUtc }); e.Property(x => x.Reference).HasMaxLength(40).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); e.Property(x => x.StatusSource).HasMaxLength(40).IsRequired(); e.Property(x => x.Commodity).HasMaxLength(240).IsRequired(); e.Property(x => x.Origin).HasMaxLength(120).IsRequired(); e.Property(x => x.Destination).HasMaxLength(120).IsRequired(); e.HasOne(x => x.Buyer).WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict); e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.ShipmentId); });
        model.Entity<ShipmentEvent>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.ShipmentId, x.OccurredAtUtc }); e.HasIndex(x => new { x.Source, x.ExternalReference }).IsUnique().HasFilter("\"ExternalReference\" IS NOT NULL"); e.Property(x => x.EventType).HasMaxLength(60).IsRequired(); e.Property(x => x.Source).HasMaxLength(40).IsRequired(); e.Property(x => x.Description).HasMaxLength(1000).IsRequired(); });
        model.Entity<TradeDocument>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.ShipmentId, x.DocumentType, x.CreatedAtUtc }); e.Property(x => x.DocumentType).HasMaxLength(60).IsRequired(); e.Property(x => x.ObjectKey).HasMaxLength(500).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); });
        model.Entity<PaymentMilestone>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.ShipmentId); e.Property(x => x.Currency).HasMaxLength(3).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); e.Property(x => x.Amount).HasPrecision(18, 2); });
        model.Entity<TradeRequest>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.Reference).IsUnique(); e.HasIndex(x => new { x.Status, x.CreatedAtUtc }); e.Property(x => x.Reference).HasMaxLength(40).IsRequired(); e.Property(x => x.Product).HasMaxLength(240).IsRequired(); e.Property(x => x.Quantity).HasMaxLength(120).IsRequired(); e.Property(x => x.Incoterms).HasMaxLength(20).IsRequired(); e.Property(x => x.OriginPort).HasMaxLength(120).IsRequired(); e.Property(x => x.DestinationPort).HasMaxLength(120).IsRequired(); e.Property(x => x.RequiredCertifications).HasMaxLength(1000); });
        model.Entity<TradeBid>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.TradeRequestId, x.ExporterOrganizationId }).IsUnique(); e.Property(x => x.EncryptedPayload).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); });
        model.Entity<KycRecord>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.OrganizationId, x.DocumentType }); e.Property(x => x.DocumentType).HasMaxLength(80).IsRequired(); e.Property(x => x.EncryptedContent).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); });
        model.Entity<TradeMessage>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.TradeRequestId, x.CreatedAtUtc }); e.Property(x => x.EncryptedBody).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); });
        model.Entity<TradeContract>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.TradeRequestId).IsUnique(); e.Property(x => x.EncryptedTerms).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); });
        model.Entity<DocumentAccessLog>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.ResourceType, x.ResourceId, x.OccurredAtUtc }); e.Property(x => x.ResourceType).HasMaxLength(40).IsRequired(); e.Property(x => x.Action).HasMaxLength(40).IsRequired(); });
        model.Entity<ShippingDocument>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.TradeRequestId, x.CreatedAtUtc }); e.Property(x => x.DocumentType).HasMaxLength(60).IsRequired(); e.Property(x => x.ContentType).HasMaxLength(100).IsRequired(); e.Property(x => x.EncryptedContent).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); });
        model.Entity<Settlement>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.TradeRequestId).IsUnique(); e.Property(x => x.Currency).HasMaxLength(3).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); e.Property(x => x.GatewayReference).HasMaxLength(200); e.Property(x => x.GrossAmount).HasPrecision(18, 2); e.Property(x => x.ChaCommission).HasPrecision(18, 2); e.Property(x => x.PlatformFee).HasPrecision(18, 2); e.Property(x => x.ExporterAmount).HasPrecision(18, 2); });
    }
}
