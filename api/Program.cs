using Microsoft.EntityFrameworkCore;
using TradeBridge.Api.Data;
using TradeBridge.Api.Domain;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("TradeBridge");
var keyDirectory = builder.Configuration["DataProtection:KeyDirectory"];
if (!string.IsNullOrWhiteSpace(keyDirectory))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
builder.Services.AddScoped<IPasswordHasher<TradeUser>, PasswordHasher<TradeUser>>();
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("auth", limiter =>
{
    limiter.PermitLimit = 10;
    limiter.Window = TimeSpan.FromMinutes(1);
    limiter.QueueLimit = 0;
}));
var connectionString = builder.Configuration.GetConnectionString("TradeBridge")
    ?? throw new InvalidOperationException("ConnectionStrings:TradeBridge is required.");
builder.Services.AddDbContext<TradeBridgeDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddCors(options => options.AddPolicy("web", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Web:Origins").Get<string[]>() ?? ["http://localhost:4200"])
          .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("web");
app.UseRateLimiter();
// Resolve opaque, revocable bearer sessions into role and organization claims.
app.Use(async (context, next) =>
{
    var authorization = context.Request.Headers.Authorization.ToString();
    if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var rawToken = authorization[7..].Trim();
        if (rawToken.Length is > 20 and <= 200)
        {
            var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
            await using var scope = context.RequestServices.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TradeBridgeDbContext>();
            var session = await db.AccessSessions.AsNoTracking()
                .Where(s => s.TokenHash == tokenHash && s.RevokedAtUtc == null && s.ExpiresAtUtc > DateTimeOffset.UtcNow)
                .Join(db.TradeUsers.AsNoTracking().Where(u => u.DisabledAtUtc == null), s => s.UserId, u => u.Id,
                    (s, u) => new { User = u }).SingleOrDefaultAsync(context.RequestAborted);
            if (session is not null)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, session.User.Id.ToString()),
                    new Claim(ClaimTypes.Name, session.User.Email ?? session.User.PhoneNumber!),
                    new Claim(ClaimTypes.Role, session.User.Role),
                    new Claim("organization_id", session.User.OrganizationId.ToString())
                };
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TradeBridgeBearer"));
            }
        }
    }
    await next();
});
app.MapPost("/api/auth/register", async (RegisterRequest request, TradeBridgeDbContext db,
    IPasswordHasher<TradeUser> passwordHasher, CancellationToken ct) =>
{
    var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
    var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
    var validPhone = phone is null || System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+[1-9]\d{7,14}$");
    var validEmail = email is null || System.Net.Mail.MailAddress.TryCreate(email, out var parsedEmail) &&
        string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase);
    if ((email is null && phone is null) || (email?.Length > 320) || !validEmail || !validPhone ||
        string.IsNullOrEmpty(request.Password) || request.Password.Length < 12 ||
        string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200 || string.IsNullOrWhiteSpace(request.Organization) ||
        request.Organization.Length > 200 || request.Role is not ("Importer" or "Exporter" or "CHA"))
        return Results.BadRequest("Provide a valid email or E.164 phone number, account details, a supported role, and a password with at least 12 characters.");
    if (email is not null && await db.TradeUsers.AnyAsync(u => u.Email == email, ct) ||
        phone is not null && await db.TradeUsers.AnyAsync(u => u.PhoneNumber == phone, ct))
        return Results.Conflict("An account with this email or phone number already exists.");
    var organization = new Organization { Name = request.Organization.Trim() };
    var user = new TradeUser
    {
        OrganizationId = organization.Id,
        Email = email,
        PhoneNumber = phone,
        FullName = request.Name.Trim(),
        PasswordHash = "pending",
        Role = request.Role == "CHA" ? "PendingCHA" : request.Role
    };
    user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
    db.Organizations.Add(organization);
    db.TradeUsers.Add(user);
    await db.SaveChangesAsync(ct);
    return Results.Created("/api/auth/login", new { user.Id, user.Email, user.Role, KycStatus = "NotSubmitted" });
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/login", async (LoginRequest request, TradeBridgeDbContext db,
    IPasswordHasher<TradeUser> passwordHasher, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrEmpty(request.Password)) return Results.Unauthorized();
    var identifier = request.Identifier.Trim();
    var email = identifier.Contains('@') ? identifier.ToLowerInvariant() : null;
    var user = await db.TradeUsers.SingleOrDefaultAsync(u => (email != null ? u.Email == email : u.PhoneNumber == identifier) && u.DisabledAtUtc == null, ct);
    if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();
    var token = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    db.AccessSessions.Add(new AccessSession { UserId = user.Id, TokenHash = tokenHash, ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(12) });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { accessToken = token, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(12), user.Id, user.Email, user.PhoneNumber, user.Role });
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/logout", async (HttpContext http, TradeBridgeDbContext db, CancellationToken ct) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Results.NoContent();
    var authorization = http.Request.Headers.Authorization.ToString();
    if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(authorization[7..].Trim())));
        var session = await db.AccessSessions.SingleOrDefaultAsync(s => s.TokenHash == hash && s.RevokedAtUtc == null, ct);
        if (session is not null) { session.RevokedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); }
    }
    return Results.NoContent();
});
app.MapGet("/api/organizations/{id:guid}/kyc-status", async (Guid id, HttpContext http, TradeBridgeDbContext db, CancellationToken ct) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var ownOrganization = Guid.TryParse(http.User.FindFirstValue("organization_id"), out var actorOrganizationId) && actorOrganizationId == id;
    var hasTradeRelationship = actorOrganizationId != Guid.Empty && await db.TradeRequests.AnyAsync(r =>
        r.ImporterOrganizationId == id && db.TradeBids.Any(b => b.TradeRequestId == r.Id && b.ExporterOrganizationId == actorOrganizationId) ||
        r.ImporterOrganizationId == actorOrganizationId && db.TradeBids.Any(b => b.TradeRequestId == r.Id && b.ExporterOrganizationId == id), ct);
    if (!ownOrganization && !hasTradeRelationship && !HasRole(http.User, "CHA") && !HasRole(http.User, "Admin"))
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    return Results.Ok(await db.KycRecords.AsNoTracking().Where(k => k.OrganizationId == id)
        .Select(k => new { k.DocumentType, k.Status, k.UploadedAtUtc }).ToListAsync(ct));
});
app.MapPost("/api/kyc", async (UploadKycRequest request, HttpContext http, TradeBridgeDbContext db,
    IDataProtectionProvider protection, CancellationToken ct) =>
{
    if (http.User.Identity?.IsAuthenticated != true || http.User.FindFirstValue(ClaimTypes.Role) is "PendingCHA")
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organizationId))
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (string.IsNullOrWhiteSpace(request.DocumentType) || request.DocumentType.Length > 80 ||
        string.IsNullOrEmpty(request.ContentBase64) || request.ContentBase64.Length > 7_000_000)
        return Results.BadRequest("Provide a document type and a file smaller than 5 MB.");
    byte[] document;
    try { document = Convert.FromBase64String(request.ContentBase64); }
    catch (FormatException) { return Results.BadRequest("The uploaded file is not valid base64 data."); }
    if (document.Length > 5_000_000) return Results.BadRequest("KYC files must be smaller than 5 MB.");
    var encrypted = protection.CreateProtector("TradeBridge.KYC.Document.v1")
        .Protect(Convert.ToBase64String(document));
    var record = new KycRecord
    {
        OrganizationId = organizationId,
        DocumentType = request.DocumentType.Trim(),
        EncryptedContent = encrypted
    };
    db.KycRecords.Add(record);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/kyc/{record.Id}", new { record.Id, record.DocumentType, record.Status, record.UploadedAtUtc });
});
app.MapGet("/api/kyc/{id:guid}/document", async (Guid id, HttpContext http, TradeBridgeDbContext db,
    IDataProtectionProvider protection, CancellationToken ct) =>
{
    if (!HasRole(http.User, "CHA") && !HasRole(http.User, "Admin"))
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    var record = await db.KycRecords.SingleOrDefaultAsync(k => k.Id == id, ct);
    if (record is null) return Results.NotFound();
    string content;
    try { content = protection.CreateProtector("TradeBridge.KYC.Document.v1").Unprotect(record.EncryptedContent); }
    catch (System.Security.Cryptography.CryptographicException) { return Results.Problem("The document encryption key is unavailable.", statusCode: 503); }
    db.DocumentAccessLogs.Add(new DocumentAccessLog
    {
        ResourceType = "KYC",
        ResourceId = id,
        ActorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        Action = "Viewed"
    });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { record.DocumentType, contentBase64 = content });
});
app.MapPost("/api/kyc/{id:guid}/review", async (Guid id, ReviewKycRequest request, HttpContext http,
    TradeBridgeDbContext db, CancellationToken ct) =>
{
    if (!HasRole(http.User, "CHA") && !HasRole(http.User, "Admin"))
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (request.Decision is not ("Verified" or "Rejected")) return Results.BadRequest("Decision must be Verified or Rejected.");
    var record = await db.KycRecords.SingleOrDefaultAsync(k => k.Id == id, ct);
    if (record is null) return Results.NotFound();
    record.Status = request.Decision;
    record.ReviewedByUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    db.DocumentAccessLogs.Add(new DocumentAccessLog
    {
        ResourceType = "KYC",
        ResourceId = id,
        ActorUserId = record.ReviewedByUserId.Value,
        Action = $"Reviewed:{record.Status}"
    });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { record.Id, record.Status, record.ReviewedByUserId });
});
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "tradebridge-api" }));
// RFQ discovery intentionally returns only fields approved for public exporter visibility.
app.MapGet("/api/rfqs", async (HttpContext http, TradeBridgeDbContext db, CancellationToken ct) =>
{
    var query = db.TradeRequests.AsNoTracking().Where(r => r.Status == "Open");
    if (HasRole(http.User, "CHA") || HasRole(http.User, "Admin"))
        query = db.TradeRequests.AsNoTracking();
    else if (HasRole(http.User, "Importer") && Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organizationId))
        query = db.TradeRequests.AsNoTracking().Where(r => r.ImporterOrganizationId == organizationId);
    Guid? exporterId = Guid.TryParse(http.User.FindFirstValue("organization_id"), out var parsedExporterId) && HasRole(http.User, "Exporter")
        ? parsedExporterId : null;
    return Results.Ok(await query.OrderByDescending(r => r.CreatedAtUtc)
        .Select(r => new PublicRfq(r.Id, r.Reference, r.Product, r.Quantity, r.Incoterms,
            r.TargetShipmentDate, r.OriginPort, r.DestinationPort, r.RequiredCertifications,
            r.Status, exporterId.HasValue && db.TradeBids.Any(b => b.TradeRequestId == r.Id && b.ExporterOrganizationId == exporterId.Value),
            exporterId.HasValue && db.TradeBids.Any(b => b.TradeRequestId == r.Id && b.ExporterOrganizationId == exporterId.Value && b.Status == "Selected"),
            r.CreatedAtUtc)).ToListAsync(ct));
});
app.MapPost("/api/rfqs", async (CreateRfqRequest request, HttpContext http, TradeBridgeDbContext db, IDataProtectionProvider protection, CancellationToken ct) =>
{
    if (!HasRole(http.User, "Importer")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (string.IsNullOrWhiteSpace(request.Product) || request.Product.Length > 240 ||
        string.IsNullOrWhiteSpace(request.Quantity) || request.Quantity.Length > 120 ||
        string.IsNullOrWhiteSpace(request.Incoterms) || request.Incoterms.Length > 20 ||
        string.IsNullOrWhiteSpace(request.OriginPort) || request.OriginPort.Length > 120 ||
        string.IsNullOrWhiteSpace(request.DestinationPort) || request.DestinationPort.Length > 120 ||
        (request.RequiredCertifications?.Length ?? 0) > 1000 ||
        (request.PrivateDetails?.ContactDetails?.Length ?? 0) > 1000 ||
        (request.PrivateDetails?.InternalNotes?.Length ?? 0) > 5000 ||
        request.PrivateDetails?.TargetPrice < 0 ||
        request.TargetShipmentDate < DateOnly.FromDateTime(DateTime.UtcNow))
        return Results.BadRequest("Required RFQ fields are missing or invalid.");
    if (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var importerId)) return Results.StatusCode(StatusCodes.Status403Forbidden);

    var rfq = new TradeRequest
    {
        Reference = $"RFQ-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21],
        ImporterOrganizationId = importerId,
        Product = request.Product.Trim(),
        Quantity = request.Quantity.Trim(),
        Incoterms = request.Incoterms.Trim().ToUpperInvariant(),
        TargetShipmentDate = request.TargetShipmentDate,
        OriginPort = request.OriginPort.Trim(),
        DestinationPort = request.DestinationPort.Trim(),
        RequiredCertifications = request.RequiredCertifications?.Trim() ?? "",
        EncryptedPrivateDetails = request.PrivateDetails is null ? null : Encoding.UTF8.GetBytes(
            protection.CreateProtector("TradeBridge.RFQ.PrivateDetails.v1")
                .Protect(JsonSerializer.Serialize(request.PrivateDetails)))
    };
    db.TradeRequests.Add(rfq);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/rfqs/{rfq.Id}", new PublicRfq(rfq.Id, rfq.Reference, rfq.Product,
        rfq.Quantity, rfq.Incoterms, rfq.TargetShipmentDate, rfq.OriginPort, rfq.DestinationPort,
        rfq.RequiredCertifications, rfq.Status, false, false, rfq.CreatedAtUtc));
});
app.MapGet("/api/rfqs/{id:guid}/private-details", async (Guid id, HttpContext http, TradeBridgeDbContext db,
    IDataProtectionProvider protection, CancellationToken ct) =>
{
    var rfq = await db.TradeRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
    if (rfq is null) return Results.NotFound();
    var owner = HasRole(http.User, "Importer") && Guid.TryParse(http.User.FindFirstValue("organization_id"), out var orgId) && rfq.ImporterOrganizationId == orgId;
    if (!owner && !HasRole(http.User, "CHA")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (rfq.EncryptedPrivateDetails is null) return Results.NoContent();
    string details;
    try { details = protection.CreateProtector("TradeBridge.RFQ.PrivateDetails.v1").Unprotect(Encoding.UTF8.GetString(rfq.EncryptedPrivateDetails)); }
    catch (System.Security.Cryptography.CryptographicException) { return Results.Problem("RFQ encryption key is unavailable.", statusCode: 503); }
    db.DocumentAccessLogs.Add(new DocumentAccessLog
    {
        ResourceType = "RfqPrivate",
        ResourceId = id,
        ActorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        Action = "Viewed"
    });
    await db.SaveChangesAsync(ct);
    return Results.Content(details, "application/json");
});
app.MapPost("/api/rfqs/{id:guid}/bids", async (Guid id, SubmitBidRequest request, HttpContext http, TradeBridgeDbContext db, IDataProtectionProvider protection, CancellationToken ct) =>
{
    if (!HasRole(http.User, "Exporter")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var exporterId)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (request.Price <= 0 || request.LeadTimeDays <= 0 || string.IsNullOrWhiteSpace(request.ShippingTerms) ||
        request.ShippingTerms.Length > 500 || request.Currency is not ("USD" or "EUR" or "INR"))
        return Results.BadRequest("Price, lead time, and tentative shipping terms are required.");
    if (!await db.TradeRequests.AnyAsync(r => r.Id == id && r.Status == "Open", ct)) return Results.NotFound();
    var bid = new TradeBid
    {
        TradeRequestId = id,
        ExporterOrganizationId = exporterId,
        EncryptedPayload = Encoding.UTF8.GetBytes(protection.CreateProtector("TradeBridge.Bid.Payload.v1")
            .Protect(JsonSerializer.Serialize(request)))
    };
    db.TradeBids.Add(bid);
    try { await db.SaveChangesAsync(ct); }
    catch (DbUpdateException) { return Results.Conflict("This exporter has already bid on this RFQ."); }
    return Results.Created($"/api/rfqs/{id}/bids/{bid.Id}", new { bid.Id, bid.Status, bid.SubmittedAtUtc });
});
app.MapGet("/api/rfqs/{id:guid}/bids", async (Guid id, HttpContext http, TradeBridgeDbContext db, CancellationToken ct) =>
{
    var rfq = await db.TradeRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
    if (rfq is null) return Results.NotFound();
    var importer = HasRole(http.User, "Importer") && rfq.Status is "BidsUnsealed" or "Awarded" &&
        Guid.TryParse(http.User.FindFirstValue("organization_id"), out var orgId) &&
        rfq.ImporterOrganizationId == orgId;
    if (!importer && !HasRole(http.User, "CHA")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    // Never return sealed bid content from this summary endpoint.
    return Results.Ok(await db.TradeBids.AsNoTracking().Where(b => b.TradeRequestId == id)
        .Select(b => new { b.Id, b.Status, b.SubmittedAtUtc }).ToListAsync(ct));
});
app.MapPost("/api/rfqs/{id:guid}/unseal-bids", async (Guid id, HttpContext http, TradeBridgeDbContext db, CancellationToken ct) =>
{
    if (!HasRole(http.User, "CHA")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var rfq = await db.TradeRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
    if (rfq is null) return Results.NotFound();
    if (rfq.Status != "Open") return Results.Conflict("Only open RFQs can have bids unsealed.");
    rfq.Status = "BidsUnsealed";
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});
app.MapGet("/api/rfqs/{id:guid}/bids/{bidId:guid}", async (Guid id, Guid bidId, HttpContext http,
    TradeBridgeDbContext db, IDataProtectionProvider protection, CancellationToken ct) =>
{
    if (!HasRole(http.User, "CHA") && !HasRole(http.User, "Importer")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var rfq = await db.TradeRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
    if (rfq is null) return Results.NotFound();
    var ownsRfq = HasRole(http.User, "CHA") ||
        rfq.Status is "BidsUnsealed" or "Awarded" &&
        Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organizationId) &&
        rfq.ImporterOrganizationId == organizationId;
    if (!ownsRfq) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var bid = await db.TradeBids.SingleOrDefaultAsync(b => b.Id == bidId && b.TradeRequestId == id, ct);
    if (bid is null) return Results.NotFound();
    string payload;
    try { payload = protection.CreateProtector("TradeBridge.Bid.Payload.v1").Unprotect(Encoding.UTF8.GetString(bid.EncryptedPayload)); }
    catch (System.Security.Cryptography.CryptographicException) { return Results.Problem("Bid encryption key is unavailable.", statusCode: 503); }
    db.DocumentAccessLogs.Add(new DocumentAccessLog
    {
        ResourceType = "Bid",
        ResourceId = bidId,
        ActorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        Action = "Viewed"
    });
    await db.SaveChangesAsync(ct);
    return Results.Content(payload, "application/json");
});
app.MapPost("/api/rfqs/{id:guid}/select-bid/{bidId:guid}", async (Guid id, Guid bidId, SelectBidRequest request,
    HttpContext http, TradeBridgeDbContext db, IDataProtectionProvider protection, CancellationToken ct) =>
{
    if (!HasRole(http.User, "Importer") && !HasRole(http.User, "CHA")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var rfq = await db.TradeRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
    if (rfq is null) return Results.NotFound();
    if (HasRole(http.User, "Importer") && (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organizationId) || rfq.ImporterOrganizationId != organizationId))
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (rfq.Status != "BidsUnsealed") return Results.Conflict("CHA must unseal bids before selection.");
    if (string.IsNullOrWhiteSpace(request.Incoterms) || string.IsNullOrWhiteSpace(request.DeliveryTerms) ||
        string.IsNullOrWhiteSpace(request.Penalties) || request.Incoterms.Length > 20 ||
        request.DeliveryTerms.Length > 1000 || request.Penalties.Length > 1000 ||
        request.Currency is not ("USD" or "EUR" or "INR"))
        return Results.BadRequest("Contract terms are incomplete or invalid.");
    var bid = await db.TradeBids.SingleOrDefaultAsync(b => b.Id == bidId && b.TradeRequestId == id, ct);
    if (bid is null) return Results.NotFound();
    var contract = new TradeContract
    {
        TradeRequestId = id,
        SelectedBidId = bidId,
        EncryptedTerms = Encoding.UTF8.GetBytes(protection.CreateProtector("TradeBridge.Contract.Terms.v1")
            .Protect(JsonSerializer.Serialize(request)))
    };
    bid.Status = "Selected";
    foreach (var otherBid in await db.TradeBids.Where(b => b.TradeRequestId == id && b.Id != bidId).ToListAsync(ct))
        otherBid.Status = "NotSelected";
    rfq.Status = "Awarded";
    db.TradeContracts.Add(contract);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/rfqs/{id}/contract", new { contract.Id, contract.Status, contract.CreatedAtUtc });
});
app.MapPost("/api/rfqs/{id:guid}/messages", async (Guid id, SendTradeMessageRequest request, HttpContext http,
    TradeBridgeDbContext db, IDataProtectionProvider protection, CancellationToken ct) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 5000) return Results.BadRequest("Message must be between 1 and 5,000 characters.");
    var rfq = await db.TradeRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
    if (rfq is null) return Results.NotFound();
    var role = http.User.FindFirstValue(ClaimTypes.Role);
    var org = Guid.TryParse(http.User.FindFirstValue("organization_id"), out var orgId) ? orgId : Guid.Empty;
    var participant = role == "CHA" || role == "Importer" && rfq.ImporterOrganizationId == org ||
        role == "Exporter" && await db.TradeBids.AnyAsync(b => b.TradeRequestId == id && b.ExporterOrganizationId == org, ct);
    if (!participant) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var encryptedBody = protection.CreateProtector("TradeBridge.Message.Body.v1").Protect(request.Body.Trim());
    var message = new TradeMessage
    {
        TradeRequestId = id,
        SenderUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        EncryptedBody = Encoding.UTF8.GetBytes(encryptedBody),
        Status = role == "CHA" ? "Released" : "PendingCHAReview"
    };
    db.TradeMessages.Add(message);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/rfqs/{id}/messages/{message.Id}", new { message.Id, message.Status, message.CreatedAtUtc });
});
app.MapGet("/api/rfqs/{id:guid}/messages", async (Guid id, HttpContext http, TradeBridgeDbContext db,
    IDataProtectionProvider protection, CancellationToken ct) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var rfq = await db.TradeRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
    if (rfq is null) return Results.NotFound();
    var role = http.User.FindFirstValue(ClaimTypes.Role);
    var org = Guid.TryParse(http.User.FindFirstValue("organization_id"), out var orgId) ? orgId : Guid.Empty;
    var participant = role == "CHA" || role == "Importer" && rfq.ImporterOrganizationId == org ||
        role == "Exporter" && await db.TradeBids.AnyAsync(b => b.TradeRequestId == id && b.ExporterOrganizationId == org, ct);
    if (!participant) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var messages = await db.TradeMessages.Where(m => m.TradeRequestId == id && (role == "CHA" || m.Status == "Released"))
        .OrderBy(m => m.CreatedAtUtc).ToListAsync(ct);
    var protector = protection.CreateProtector("TradeBridge.Message.Body.v1");
    return Results.Ok(messages.Select(m => new
    {
        m.Id,
        m.SenderUserId,
        m.Status,
        m.CreatedAtUtc,
        Body = protector.Unprotect(Encoding.UTF8.GetString(m.EncryptedBody))
    }));
});
app.MapPost("/api/rfqs/{id:guid}/messages/{messageId:guid}/release", async (Guid id, Guid messageId,
    HttpContext http, TradeBridgeDbContext db, CancellationToken ct) =>
{
    if (!HasRole(http.User, "CHA")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var message = await db.TradeMessages.SingleOrDefaultAsync(m => m.Id == messageId && m.TradeRequestId == id, ct);
    if (message is null) return Results.NotFound();
    message.Status = "Released";
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
});
app.MapPost("/api/rfqs/{id:guid}/documents", async (Guid id, UploadShippingDocumentRequest request,
    HttpContext http, TradeBridgeDbContext db, IDataProtectionProvider protection, CancellationToken ct) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    if (string.IsNullOrEmpty(request.ContentBase64) || request.ContentBase64.Length > 14_000_000 ||
        string.IsNullOrWhiteSpace(request.DocumentType) || request.DocumentType.Length > 60 ||
        request.ContentType is not ("application/pdf" or "image/jpeg" or "image/png") ||
        request.DocumentType is not ("Bill of lading" or "Commercial invoice" or "Packing list" or "Certificate of analysis" or "Customs paperwork"))
        return Results.BadRequest("Choose an allowed document type and a file smaller than 10 MB.");
    byte[] content;
    try { content = Convert.FromBase64String(request.ContentBase64); }
    catch (FormatException) { return Results.BadRequest("The uploaded file is not valid base64 data."); }
    if (content.Length is 0 or > 10_000_000) return Results.BadRequest("Documents must be smaller than 10 MB.");
    var rfq = await db.TradeRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
    if (rfq is null) return Results.NotFound();
    var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var orgId = Guid.TryParse(http.User.FindFirstValue("organization_id"), out var parsedOrg) ? parsedOrg : Guid.Empty;
    var canUpload = HasRole(http.User, "CHA") || HasRole(http.User, "Importer") && rfq.ImporterOrganizationId == orgId ||
        HasRole(http.User, "Exporter") && rfq.Status == "Awarded" &&
        await db.TradeBids.AnyAsync(b => b.TradeRequestId == id && b.ExporterOrganizationId == orgId && b.Status == "Selected", ct);
    if (!canUpload) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var protectedContent = protection.CreateProtector("TradeBridge.ShippingDocument.Content.v1").Protect(Convert.ToBase64String(content));
    var document = new ShippingDocument
    {
        TradeRequestId = id,
        UploadedByUserId = userId,
        DocumentType = request.DocumentType,
        ContentType = request.ContentType,
        EncryptedContent = protectedContent,
        Status = HasRole(http.User, "CHA") ? "Released" : "PendingCHAReview"
    };
    db.ShippingDocuments.Add(document);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/rfqs/{id}/documents/{document.Id}", new { document.Id, document.DocumentType, document.Status, document.CreatedAtUtc });
});
app.MapGet("/api/rfqs/{id:guid}/documents", async (Guid id, HttpContext http, TradeBridgeDbContext db, CancellationToken ct) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var rfq = await db.TradeRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
    if (rfq is null) return Results.NotFound();
    var role = http.User.FindFirstValue(ClaimTypes.Role);
    var org = Guid.TryParse(http.User.FindFirstValue("organization_id"), out var orgId) ? orgId : Guid.Empty;
    var participant = role is "CHA" or "Admin" || role == "Importer" && rfq.ImporterOrganizationId == org ||
        role == "Exporter" && await db.TradeBids.AnyAsync(b => b.TradeRequestId == id && b.ExporterOrganizationId == org, ct);
    if (!participant) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var userId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    return Results.Ok(await db.ShippingDocuments.AsNoTracking().Where(d => d.TradeRequestId == id)
        .Select(d => new ShippingDocumentMetadata(d.Id, d.DocumentType, d.ContentType, d.Status, d.CreatedAtUtc, d.UploadedByUserId == userId)).ToListAsync(ct));
});
app.MapGet("/api/rfqs/{id:guid}/documents/{documentId:guid}/content", async (Guid id, Guid documentId,
    HttpContext http, TradeBridgeDbContext db, IDataProtectionProvider protection, CancellationToken ct) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var document = await db.ShippingDocuments.SingleOrDefaultAsync(d => d.Id == documentId && d.TradeRequestId == id, ct);
    if (document is null) return Results.NotFound();
    var role = http.User.FindFirstValue(ClaimTypes.Role);
    var org = Guid.TryParse(http.User.FindFirstValue("organization_id"), out var orgId) ? orgId : Guid.Empty;
    var canRead = role is "CHA" or "Admin" || role == "Importer" && document.Status == "Released" ||
        role == "Exporter" && document.UploadedByUserId == Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!) &&
        await db.TradeBids.AnyAsync(b => b.TradeRequestId == id && b.ExporterOrganizationId == org && b.Status == "Selected", ct);
    if (!canRead) return Results.StatusCode(StatusCodes.Status403Forbidden);
    string content;
    try { content = protection.CreateProtector("TradeBridge.ShippingDocument.Content.v1").Unprotect(document.EncryptedContent); }
    catch (System.Security.Cryptography.CryptographicException) { return Results.Problem("Document encryption key is unavailable.", statusCode: 503); }
    db.DocumentAccessLogs.Add(new DocumentAccessLog
    {
        ResourceType = "ShippingDocument",
        ResourceId = documentId,
        ActorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        Action = "Downloaded"
    });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { document.DocumentType, document.ContentType, contentBase64 = content });
});
app.MapPost("/api/rfqs/{id:guid}/documents/{documentId:guid}/review", async (Guid id, Guid documentId,
    ReviewShippingDocumentRequest request, HttpContext http, TradeBridgeDbContext db, CancellationToken ct) =>
{
    if (!HasRole(http.User, "CHA")) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var document = await db.ShippingDocuments.SingleOrDefaultAsync(d => d.Id == documentId && d.TradeRequestId == id, ct);
    if (document is null) return Results.NotFound();
    if (request.Decision is not ("Verified" or "Rejected")) return Results.BadRequest("Decision must be Verified or Rejected.");
    document.Status = request.Decision == "Verified" ? "Released" : "Rejected";
    db.DocumentAccessLogs.Add(new DocumentAccessLog
    {
        ResourceType = "ShippingDocument",
        ResourceId = documentId,
        ActorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        Action = $"Reviewed:{document.Status}"
    });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { document.Id, document.Status });
});
app.MapGet("/api/rfqs/{id:guid}/contract", async (Guid id, HttpContext http, TradeBridgeDbContext db,
    IDataProtectionProvider protection, CancellationToken ct) =>
{
    var contract = await db.TradeContracts.SingleOrDefaultAsync(c => c.TradeRequestId == id, ct);
    if (contract is null) return Results.NotFound();
    var rfq = await db.TradeRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
    var isParty = HasRole(http.User, "CHA") ||
        HasRole(http.User, "Importer") && Guid.TryParse(http.User.FindFirstValue("organization_id"), out var orgId) && rfq!.ImporterOrganizationId == orgId ||
        HasRole(http.User, "Exporter") && Guid.TryParse(http.User.FindFirstValue("organization_id"), out var exporterId) &&
            await db.TradeBids.AnyAsync(b => b.Id == contract.SelectedBidId && b.ExporterOrganizationId == exporterId, ct);
    if (!isParty) return Results.StatusCode(StatusCodes.Status403Forbidden);
    string terms;
    try { terms = protection.CreateProtector("TradeBridge.Contract.Terms.v1").Unprotect(Encoding.UTF8.GetString(contract.EncryptedTerms)); }
    catch (System.Security.Cryptography.CryptographicException) { return Results.Problem("Contract encryption key is unavailable.", statusCode: 503); }
    db.DocumentAccessLogs.Add(new DocumentAccessLog
    {
        ResourceType = "Contract",
        ResourceId = contract.Id,
        ActorUserId = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        Action = "Viewed"
    });
    await db.SaveChangesAsync(ct);
    return Results.Content(terms, "application/json");
});
app.MapGet("/api/shipments", async (HttpContext http, TradeBridgeDbContext db, CancellationToken ct) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var query = db.Shipments.AsNoTracking();
    if (!HasRole(http.User, "CHA") && !HasRole(http.User, "Admin"))
    {
        if (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organizationId)) return Results.Unauthorized();
        query = query.Where(s => s.BuyerId == organizationId);
    }
    return Results.Ok(await query.OrderByDescending(s => s.UpdatedAtUtc)
        .Select(s => new ShipmentSummary(s.Id, s.Reference, s.Buyer.Name, s.Origin, s.Destination,
            s.Commodity, s.Status, s.UpdatedAtUtc, s.StatusSource)).ToListAsync(ct));
});
app.MapGet("/api/shipments/{id:guid}", async (Guid id, HttpContext http, TradeBridgeDbContext db, CancellationToken ct) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var shipment = await db.Shipments.AsNoTracking()
        .Include(s => s.Events.OrderByDescending(e => e.OccurredAtUtc).Take(20))
        .Include(s => s.Buyer)
        .SingleOrDefaultAsync(s => s.Id == id, ct);
    if (shipment is not null && !HasRole(http.User, "CHA") && !HasRole(http.User, "Admin") &&
        (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organizationId) || shipment.BuyerId != organizationId))
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    return shipment is null ? Results.NotFound() : Results.Ok(new ShipmentDetail(
        shipment.Id, shipment.Reference, shipment.Buyer.Name, shipment.Origin, shipment.Destination,
        shipment.Commodity, shipment.Status, shipment.UpdatedAtUtc, shipment.StatusSource,
        shipment.Events.Select(e => new ShipmentEventDto(e.Id, e.EventType, e.Description,
            e.OccurredAtUtc, e.Source, e.ExternalReference))));
});
app.Run();

static bool HasRole(ClaimsPrincipal user, string role) =>
    user.Identity?.IsAuthenticated == true && user.IsInRole(role);

record ShipmentSummary(Guid Id, string Reference, string Buyer, string Origin, string Destination,
    string Commodity, string Status, DateTimeOffset UpdatedAtUtc, string StatusSource);
record ShipmentDetail(Guid Id, string Reference, string Buyer, string Origin, string Destination,
    string Commodity, string Status, DateTimeOffset UpdatedAtUtc, string StatusSource,
    IEnumerable<ShipmentEventDto> Events);
record ShipmentEventDto(Guid Id, string EventType, string Description, DateTimeOffset OccurredAtUtc,
    string Source, string? ExternalReference);
record PublicRfq(Guid Id, string Reference, string Product, string Quantity, string Incoterms,
    DateOnly TargetShipmentDate, string OriginPort, string DestinationPort,
    string RequiredCertifications, string Status, bool HasSubmittedBid, bool IsSelectedExporter, DateTimeOffset CreatedAtUtc);
record CreateRfqRequest(string Product, string Quantity, string Incoterms, DateOnly TargetShipmentDate,
    string OriginPort, string DestinationPort, string? RequiredCertifications,
    RfqPrivateDetails? PrivateDetails);
record RfqPrivateDetails(string? ContactDetails, decimal? TargetPrice, string? InternalNotes);
record SubmitBidRequest(decimal Price, string Currency, int LeadTimeDays, string ShippingTerms,
    string? SampleCoaObjectKey);
record RegisterRequest(string Name, string Organization, string? Email, string? PhoneNumber, string Password, string Role);
record LoginRequest(string Identifier, string Password);
record UploadKycRequest(string DocumentType, string ContentBase64);
record ReviewKycRequest(string Decision);
record SelectBidRequest(string Incoterms, string DeliveryTerms, string Penalties, string Currency);
record SendTradeMessageRequest(string Body);
record UploadShippingDocumentRequest(string DocumentType, string ContentType, string ContentBase64);
record ReviewShippingDocumentRequest(string Decision);
record ShippingDocumentMetadata(Guid Id, string DocumentType, string ContentType, string Status, DateTimeOffset CreatedAtUtc, bool UploadedByMe);
