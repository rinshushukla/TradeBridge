using Microsoft.EntityFrameworkCore;
using TradeBridge.Api.Data;
using TradeBridge.Api.Domain;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("TradeBridge")
    ?? throw new InvalidOperationException("ConnectionStrings:TradeBridge is required.");
builder.Services.AddDbContext<TradeBridgeDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddCors(options => options.AddPolicy("web", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Web:Origins").Get<string[]>() ?? ["http://localhost:4200"])
          .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors("web");
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "tradebridge-api" }));
app.MapGet("/api/shipments", async (TradeBridgeDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Shipments.AsNoTracking()
        .OrderByDescending(s => s.UpdatedAtUtc)
        .Select(s => new ShipmentSummary(s.Id, s.Reference, s.Buyer.Name, s.Origin, s.Destination,
            s.Commodity, s.Status, s.UpdatedAtUtc, s.StatusSource))
        .ToListAsync(ct)));
app.MapGet("/api/shipments/{id:guid}", async (Guid id, TradeBridgeDbContext db, CancellationToken ct) =>
{
    var shipment = await db.Shipments.AsNoTracking()
        .Include(s => s.Events.OrderByDescending(e => e.OccurredAtUtc).Take(20))
        .Include(s => s.Buyer)
        .SingleOrDefaultAsync(s => s.Id == id, ct);
    return shipment is null ? Results.NotFound() : Results.Ok(new ShipmentDetail(
        shipment.Id, shipment.Reference, shipment.Buyer.Name, shipment.Origin, shipment.Destination,
        shipment.Commodity, shipment.Status, shipment.UpdatedAtUtc, shipment.StatusSource,
        shipment.Events.Select(e => new ShipmentEventDto(e.Id, e.EventType, e.Description,
            e.OccurredAtUtc, e.Source, e.ExternalReference))));
});
app.Run();

record ShipmentSummary(Guid Id, string Reference, string Buyer, string Origin, string Destination,
    string Commodity, string Status, DateTimeOffset UpdatedAtUtc, string StatusSource);
record ShipmentDetail(Guid Id, string Reference, string Buyer, string Origin, string Destination,
    string Commodity, string Status, DateTimeOffset UpdatedAtUtc, string StatusSource,
    IEnumerable<ShipmentEventDto> Events);
record ShipmentEventDto(Guid Id, string EventType, string Description, DateTimeOffset OccurredAtUtc,
    string Source, string? ExternalReference);
