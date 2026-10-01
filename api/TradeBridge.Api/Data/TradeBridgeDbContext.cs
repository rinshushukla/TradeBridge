using Microsoft.EntityFrameworkCore;
using TradeBridge.Api.Domain;

namespace TradeBridge.Api.Data;

public sealed class TradeBridgeDbContext(DbContextOptions<TradeBridgeDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();
    public DbSet<TradeDocument> TradeDocuments => Set<TradeDocument>();
    public DbSet<PaymentMilestone> PaymentMilestones => Set<PaymentMilestone>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Organization>(e => { e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(200).IsRequired(); e.Property(x => x.Iec).HasMaxLength(10); e.Property(x => x.Gstin).HasMaxLength(15); e.HasIndex(x => x.Iec).IsUnique().HasFilter("\"Iec\" IS NOT NULL"); e.HasIndex(x => x.Gstin); });
        model.Entity<Shipment>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.Reference).IsUnique(); e.HasIndex(x => new { x.BuyerId, x.UpdatedAtUtc }); e.Property(x => x.Reference).HasMaxLength(40).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); e.Property(x => x.StatusSource).HasMaxLength(40).IsRequired(); e.Property(x => x.Commodity).HasMaxLength(240).IsRequired(); e.Property(x => x.Origin).HasMaxLength(120).IsRequired(); e.Property(x => x.Destination).HasMaxLength(120).IsRequired(); e.HasOne(x => x.Buyer).WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict); e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.ShipmentId); });
        model.Entity<ShipmentEvent>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.ShipmentId, x.OccurredAtUtc }); e.HasIndex(x => new { x.Source, x.ExternalReference }).IsUnique().HasFilter("\"ExternalReference\" IS NOT NULL"); e.Property(x => x.EventType).HasMaxLength(60).IsRequired(); e.Property(x => x.Source).HasMaxLength(40).IsRequired(); e.Property(x => x.Description).HasMaxLength(1000).IsRequired(); });
        model.Entity<TradeDocument>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.ShipmentId, x.DocumentType, x.CreatedAtUtc }); e.Property(x => x.DocumentType).HasMaxLength(60).IsRequired(); e.Property(x => x.ObjectKey).HasMaxLength(500).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); });
        model.Entity<PaymentMilestone>(e => { e.HasKey(x => x.Id); e.HasIndex(x => x.ShipmentId); e.Property(x => x.Currency).HasMaxLength(3).IsRequired(); e.Property(x => x.Status).HasMaxLength(40).IsRequired(); e.Property(x => x.Amount).HasPrecision(18, 2); });
    }
}
