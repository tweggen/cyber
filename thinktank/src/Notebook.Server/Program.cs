using Microsoft.EntityFrameworkCore;
using Notebook.Data;
using Notebook.Data.Repositories;
using Notebook.Server.Auth;
using Notebook.Server.Endpoints;
using Notebook.Server.Services;
using Org.BouncyCastle.Crypto.Parameters;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("Notebook")
    ?? throw new InvalidOperationException("ConnectionStrings:Notebook is required");

builder.Services.AddDbContext<NotebookDbContext>(options =>
    options.UseNpgsql(connectionString));

// Repositories
builder.Services.AddScoped<IEntryRepository, EntryRepository>();
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IJobResultProcessor, JobResultProcessor>();
builder.Services.AddScoped<INotebookRepository, NotebookRepository>();
builder.Services.AddScoped<IAccessRepository, AccessRepository>();
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();

// Audit — fire-and-forget bounded channel writer
builder.Services.AddSingleton<IAuditService, AuditService>();
builder.Services.AddHostedService(sp => (AuditService)sp.GetRequiredService<IAuditService>());

// Authentication — EdDSA (Ed25519) JWT signed by admin app
builder.Services.AddAuthentication(EdDsaAuthenticationHandler.SchemeName)
    .AddScheme<EdDsaAuthenticationOptions, EdDsaAuthenticationHandler>(
        EdDsaAuthenticationHandler.SchemeName, options =>
        {
            var publicKeyBase64 = builder.Configuration["Jwt:PublicKey"];
            if (!string.IsNullOrEmpty(publicKeyBase64))
            {
                var spkiBytes = Convert.FromBase64String(publicKeyBase64);
                // SPKI for Ed25519 is 44 bytes: 12-byte ASN.1 prefix + 32-byte raw key
                var rawKey = new byte[32];
                Array.Copy(spkiBytes, 12, rawKey, 0, 32);
                options.PublicKey = new Ed25519PublicKeyParameters(rawKey, 0);
            }

            options.AllowDevIdentity = builder.Configuration.GetValue<bool>("AllowDevIdentity");
        });

// Authorization — scope-based policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanRead", p => p.RequireClaim("scope", "notebook:read"));
    options.AddPolicy("CanWrite", p => p.RequireClaim("scope", "notebook:write"));
    options.AddPolicy("CanShare", p => p.RequireClaim("scope", "notebook:share"));
    options.AddPolicy("CanAdmin", p => p.RequireClaim("scope", "notebook:admin"));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapNotebookEndpoints();
app.MapBatchEndpoints();
app.MapClaimsEndpoints();
app.MapJobEndpoints();
app.MapBrowseEndpoints();
app.MapSearchEndpoints();
app.MapShareEndpoints();
app.MapAuditEndpoints();
app.MapOrganizationEndpoints();
app.MapGroupEndpoints();

app.Run();

// Make the implicit Program class accessible to integration tests
public partial class Program { }
