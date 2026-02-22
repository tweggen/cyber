using NotebookAdmin.Data;
using NotebookAdmin.Models;
using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.EntityFrameworkCore;

namespace NotebookAdmin.Services;

/// <summary>
/// Export user data to CSV format.
/// </summary>
public class UserExportService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UserExportService> _logger;

    public UserExportService(ApplicationDbContext db, ILogger<UserExportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Export all users to CSV with quota information.
    /// Returns CSV content as string.
    /// </summary>
    public async Task<string> ExportAllUsersAsync()
    {
        var users = await _db.Users
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var quotas = await _db.UserQuotas.ToListAsync();
        var quotaDict = quotas.ToDictionary(q => q.UserId);

        _logger.LogInformation("Exporting {Count} users to CSV", users.Count);
        return GenerateCSV(users, quotaDict);
    }

    /// <summary>
    /// Export filtered users to CSV.
    /// </summary>
    public async Task<string> ExportUsersAsync(
        string? userType = null,
        bool? isLocked = null)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrEmpty(userType))
            query = query.Where(u => u.UserType == userType);

        if (isLocked.HasValue)
            query = query.Where(u => (u.LockoutEnd > DateTimeOffset.UtcNow) == isLocked.Value);

        var users = await query
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var quotas = await _db.UserQuotas
            .Where(q => users.Select(u => u.Id).Contains(q.UserId))
            .ToListAsync();

        var quotaDict = quotas.ToDictionary(q => q.UserId);

        _logger.LogInformation("Exporting {Count} filtered users to CSV", users.Count);
        return GenerateCSV(users, quotaDict);
    }

    private static string GenerateCSV(List<ApplicationUser> users, Dictionary<string, UserQuota> quotaDict)
    {
        var sb = new StringBuilder();

        // Write header
        sb.AppendLine(CsvEncodeLine(new[]
        {
            "username",
            "email",
            "display_name",
            "user_type",
            "author_id_hex",
            "account_created",
            "last_login",
            "lock_status",
            "lock_reason",
            "max_notebooks",
            "max_entries_per_notebook",
            "max_entry_size_bytes",
            "max_total_storage_bytes"
        }));

        // Write data rows
        foreach (var user in users)
        {
            quotaDict.TryGetValue(user.Id, out var quota);
            var isLocked = user.LockoutEnd > DateTimeOffset.UtcNow;
            var lockStatus = isLocked ? "locked" : "active";

            sb.AppendLine(CsvEncodeLine(new[]
            {
                user.UserName ?? "",
                user.Email ?? "",
                user.DisplayName ?? "",
                user.UserType,
                user.AuthorIdHex,
                user.CreatedAt.ToString("O"),
                user.LastLoginAt?.ToString("O") ?? "",
                lockStatus,
                isLocked ? (user.LockReason ?? "") : "",
                quota?.MaxNotebooks.ToString() ?? "",
                quota?.MaxEntriesPerNotebook.ToString() ?? "",
                quota?.MaxEntrySizeBytes.ToString() ?? "",
                quota?.MaxTotalStorageBytes.ToString() ?? ""
            }));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Encode a CSV line, handling quotes and commas.
    /// </summary>
    private static string CsvEncodeLine(string[] fields)
    {
        var encoded = fields.Select(field =>
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";

            // If field contains comma, quote, or newline, wrap in quotes and escape quotes
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            {
                var escaped = field.Replace("\"", "\"\"");
                return $"\"{escaped}\"";
            }

            return field;
        });

        return string.Join(",", encoded);
    }
}
