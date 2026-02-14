using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Notebook.Data;
using Notebook.Data.Repositories;
using Notebook.Server.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("Notebook")
    ?? throw new InvalidOperationException("ConnectionStrings:Notebook is required");

builder.Services.AddDbContext<NotebookDbContext>(options =>
    options.UseNpgsql(connectionString));

// Repositories
builder.Services.AddScoped<IEntryRepository, EntryRepository>();
builder.Services.AddScoped<IJobRepository, JobRepository>();

// Authentication — JWT Bearer validated externally (admin app issues tokens)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapBatchEndpoints();
app.MapClaimsEndpoints();

app.Run();
