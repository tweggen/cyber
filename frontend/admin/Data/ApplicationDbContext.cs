using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NotebookAdmin.Models;

namespace NotebookAdmin.Data;

/// <summary>
/// EF Core database context for the admin application.
/// Extends IdentityDbContext to include ASP.NET Core Identity tables.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserQuota> UserQuotas => Set<UserQuota>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.AuthorId)
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(e => e.AuthorIdHex)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(e => e.DisplayName)
                .HasMaxLength(256);

            entity.HasIndex(e => e.AuthorIdHex)
                .IsUnique();
        });

        builder.Entity<UserQuota>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<UserQuota>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
