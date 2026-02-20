using Microsoft.EntityFrameworkCore;
using Notebook.Data;
using Notebook.Data.Repositories;
using Notebook.Server.Auth;
using Notebook.Server.Configuration;
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

// Content processing
builder.Services.AddSingleton<IContentNormalizer, ContentNormalizer>();
builder.Services.AddSingleton<IContentFilter, WikipediaContentFilter>();
builder.Services.AddSingleton<IContentFilterPipeline, ContentFilterPipeline>();
builder.Services.AddSingleton<IMarkdownFragmenter, MarkdownFragmenter>();

// Embedding (for semantic search)
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embedding"));
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanRead", policy =>
        policy.RequireClaim("scope", "notebook:read"));
    options.AddPolicy("CanWrite", policy =>
        policy.RequireClaim("scope", "notebook:write"));
    options.AddPolicy("CanShare", policy =>
        policy.RequireClaim("scope", "notebook:share"));
    options.AddPolicy("CanAdmin", policy =>
        policy.RequireClaim("scope", "notebook:admin"));
});

// Access control
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAccessControl, AccessControl>();

// Audit
builder.Services.AddSingleton<AuditService>();
builder.Services.AddSingleton<IAuditService>(sp => sp.GetRequiredService<AuditService>());
builder.Services.AddHostedService<AuditConsumerService>();
builder.Services.AddHostedService<AuditRecoveryService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthorEndpoints();
app.MapNotebookEndpoints();
app.MapBatchEndpoints();
app.MapClaimsEndpoints();
app.MapJobEndpoints();
app.MapBrowseEndpoints();
app.MapObserveEndpoints();
app.MapSearchEndpoints();
app.MapReadEndpoints();
app.MapShareEndpoints();
app.MapAuditEndpoints();

app.Run();

// Make the implicit Program class accessible to integration tests
public partial class Program { }
