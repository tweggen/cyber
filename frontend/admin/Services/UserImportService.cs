using NotebookAdmin.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CsvHelper;
using Microsoft.AspNetCore.Identity;

namespace NotebookAdmin.Services;

public record ImportRecord(
    string Username,
    string? Email,
    string? DisplayName,
    string UserType,
    string? LockStatus,
    string? LockReason,
    int? MaxNotebooks,
    int? MaxEntriesPerNotebook,
    long? MaxEntrySizeBytes,
    long? MaxTotalStorageBytes);

public record ImportResult(
    int SuccessCount,
    int ErrorCount,
    List<ImportError> Errors,
    Dictionary<string, string> CreatedPasswords = null!);

public record ImportError(
    int RowNumber,
    string Username,
    string Message);

/// <summary>
/// Import users from CSV file.
/// </summary>
public class UserImportService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly QuotaService _quotaService;
    private readonly ILogger<UserImportService> _logger;

    public UserImportService(
        UserManager<ApplicationUser> userManager,
        QuotaService quotaService,
        ILogger<UserImportService> logger)
    {
        _userManager = userManager;
        _quotaService = quotaService;
        _logger = logger;
    }

    /// <summary>
    /// Validate and import users from CSV stream.
    /// Performs validation without writing to database.
    /// </summary>
    public async Task<(bool IsValid, List<ImportError> Errors)> ValidateImportAsync(
        Stream csvStream)
    {
        var errors = new List<ImportError>();
        var records = await ParseCSVAsync(csvStream);
        var seenUsernames = new HashSet<string>();
        var rowNumber = 2; // Start at 2 (after header)

        foreach (var record in records)
        {
            var recordErrors = ValidateRecord(record, rowNumber, seenUsernames);
            errors.AddRange(recordErrors);
            seenUsernames.Add(record.Username);
            rowNumber++;
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Import users from CSV stream.
    /// Returns summary with success/error counts and created passwords.
    /// </summary>
    public async Task<ImportResult> ImportAsync(Stream csvStream)
    {
        var errors = new List<ImportError>();
        var successCount = 0;
        var createdPasswords = new Dictionary<string, string>();
        var records = await ParseCSVAsync(csvStream);
        var rowNumber = 2;

        foreach (var record in records)
        {
            try
            {
                // Validate
                var validationErrors = ValidateRecord(record, rowNumber, new());
                if (validationErrors.Any())
                {
                    errors.AddRange(validationErrors);
                    rowNumber++;
                    continue;
                }

                // Check if user exists
                var existingUser = await _userManager.FindByNameAsync(record.Username);
                if (existingUser != null)
                {
                    errors.Add(new ImportError(
                        rowNumber,
                        record.Username,
                        "User already exists"));
                    rowNumber++;
                    continue;
                }

                // Create user
                var authorIdBytes = GenerateAuthorIdBytes();
                var user = new ApplicationUser
                {
                    UserName = record.Username,
                    Email = record.Email,
                    DisplayName = record.DisplayName,
                    UserType = record.UserType,
                    CreatedAt = DateTime.UtcNow,
                    AuthorId = authorIdBytes,
                    AuthorIdHex = GenerateAuthorIdHex()
                };

                // Lock if needed
                if (record.LockStatus == "locked")
                {
                    user.LockReason = record.LockReason;
                    user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                }

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    errors.Add(new ImportError(
                        rowNumber,
                        record.Username,
                        $"Creation failed: {string.Join(", ", createResult.Errors.Select(e => e.Description))}"));
                    rowNumber++;
                    continue;
                }

                // Generate temporary password
                var tempPassword = GenerateTemporaryPassword();
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var pwResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);

                if (!pwResult.Succeeded)
                {
                    errors.Add(new ImportError(
                        rowNumber,
                        record.Username,
                        $"Password setup failed: {string.Join(", ", pwResult.Errors.Select(e => e.Description))}"));
                    rowNumber++;
                    continue;
                }

                createdPasswords[record.Username] = tempPassword;

                // Set quota if provided
                if (record.MaxNotebooks.HasValue ||
                    record.MaxEntriesPerNotebook.HasValue ||
                    record.MaxEntrySizeBytes.HasValue ||
                    record.MaxTotalStorageBytes.HasValue)
                {
                    var quota = await _quotaService.GetOrCreateDefaultAsync(user.Id);
                    if (record.MaxNotebooks.HasValue)
                        quota.MaxNotebooks = record.MaxNotebooks.Value;
                    if (record.MaxEntriesPerNotebook.HasValue)
                        quota.MaxEntriesPerNotebook = record.MaxEntriesPerNotebook.Value;
                    if (record.MaxEntrySizeBytes.HasValue)
                        quota.MaxEntrySizeBytes = record.MaxEntrySizeBytes.Value;
                    if (record.MaxTotalStorageBytes.HasValue)
                        quota.MaxTotalStorageBytes = record.MaxTotalStorageBytes.Value;

                    await _quotaService.UpdateQuotaAsync(user.Id, quota);
                }

                _logger.LogInformation("Imported user {Username} with temporary password", record.Username);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new ImportError(
                    rowNumber,
                    record.Username,
                    $"Unexpected error: {ex.Message}"));
                _logger.LogError(ex, "Import error for user {Username}", record.Username);
            }

            rowNumber++;
        }

        return new ImportResult(successCount, errors.Count, errors, createdPasswords);
    }

    private async Task<List<ImportRecord>> ParseCSVAsync(Stream csvStream)
    {
        var records = new List<ImportRecord>();
        csvStream.Position = 0;

        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            records.Add(new ImportRecord(
                csv.GetField("username")?.Trim() ?? "",
                csv.GetField("email")?.Trim(),
                csv.GetField("display_name")?.Trim(),
                csv.GetField("user_type")?.Trim() ?? "",
                csv.GetField("lock_status")?.Trim(),
                csv.GetField("lock_reason")?.Trim(),
                int.TryParse(csv.GetField("max_notebooks"), out var nb) ? nb : null,
                int.TryParse(csv.GetField("max_entries_per_notebook"), out var ep) ? ep : null,
                long.TryParse(csv.GetField("max_entry_size_bytes"), out var es) ? es : null,
                long.TryParse(csv.GetField("max_total_storage_bytes"), out var ts) ? ts : null));
        }

        return records;
    }

    private List<ImportError> ValidateRecord(
        ImportRecord record,
        int rowNumber,
        HashSet<string> seenUsernames)
    {
        var errors = new List<ImportError>();

        // Username validation
        if (string.IsNullOrWhiteSpace(record.Username))
        {
            errors.Add(new ImportError(rowNumber, record.Username, "Username is required"));
            return errors; // Can't continue without username
        }

        if (record.Username.Length < 3 || record.Username.Length > 50)
            errors.Add(new ImportError(rowNumber, record.Username, "Username must be 3-50 characters"));
        else if (!Regex.IsMatch(record.Username, @"^[a-zA-Z0-9_-]+$"))
            errors.Add(new ImportError(rowNumber, record.Username, "Username must contain only alphanumeric characters, underscore, or hyphen"));
        else if (seenUsernames.Contains(record.Username))
            errors.Add(new ImportError(rowNumber, record.Username, "Username already exists in this import"));

        // UserType validation
        if (string.IsNullOrWhiteSpace(record.UserType))
            errors.Add(new ImportError(rowNumber, record.Username, "User type is required"));
        else if (!new[] { "user", "service_account", "bot" }.Contains(record.UserType))
            errors.Add(new ImportError(rowNumber, record.Username, $"Invalid user_type: {record.UserType}. Must be: user, service_account, or bot"));

        // Email validation
        if (!string.IsNullOrEmpty(record.Email))
        {
            try
            {
                new System.Net.Mail.MailAddress(record.Email);
            }
            catch
            {
                errors.Add(new ImportError(rowNumber, record.Username, $"Invalid email format: {record.Email}"));
            }
        }

        // Lock status validation
        if (!string.IsNullOrEmpty(record.LockStatus) &&
            !new[] { "active", "locked" }.Contains(record.LockStatus))
            errors.Add(new ImportError(rowNumber, record.Username, $"Invalid lock_status: {record.LockStatus}. Must be: active or locked"));

        // Quota validation
        if (record.MaxNotebooks.HasValue && record.MaxNotebooks.Value < 1)
            errors.Add(new ImportError(rowNumber, record.Username, "max_notebooks must be >= 1"));

        if (record.MaxEntriesPerNotebook.HasValue && record.MaxEntriesPerNotebook.Value < 1)
            errors.Add(new ImportError(rowNumber, record.Username, "max_entries_per_notebook must be >= 1"));

        if (record.MaxEntrySizeBytes.HasValue && record.MaxEntrySizeBytes.Value < 1)
            errors.Add(new ImportError(rowNumber, record.Username, "max_entry_size_bytes must be >= 1"));

        if (record.MaxTotalStorageBytes.HasValue && record.MaxTotalStorageBytes.Value < 1)
            errors.Add(new ImportError(rowNumber, record.Username, "max_total_storage_bytes must be >= 1"));

        return errors;
    }

    private static byte[] GenerateAuthorIdBytes()
    {
        // Generate random 32-byte author ID (BLAKE3 hash simulation)
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return bytes;
    }

    private static string GenerateAuthorIdHex()
    {
        // Generate random 64-char hex string
        Span<byte> bytes = stackalloc byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateTemporaryPassword()
    {
        // Satisfies: RequireDigit=true, RequireLowercase=true, RequireUppercase=false,
        // RequireNonAlphanumeric=false, RequiredLength=8
        Span<byte> bytes = stackalloc byte[12];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        var chars = new char[12];
        for (int i = 0; i < 8; i++)
            chars[i] = (char)('a' + (bytes[i] % 26));
        for (int i = 8; i < 12; i++)
            chars[i] = (char)('0' + (bytes[i] % 10));

        return new string(chars);
    }
}
