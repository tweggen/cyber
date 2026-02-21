using System.Text.Json.Serialization;

namespace Notebook.Core.Security;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccessTier
{
    Existence = 0,
    Read = 1,
    ReadWrite = 2,
    Admin = 3,
}

public static class AccessTierExtensions
{
    public static string ToDbString(this AccessTier tier) => tier switch
    {
        AccessTier.Existence => "existence",
        AccessTier.Read => "read",
        AccessTier.ReadWrite => "read_write",
        AccessTier.Admin => "admin",
        _ => "read",
    };

    public static AccessTier ParseAccessTier(string value) => value.ToLowerInvariant() switch
    {
        "existence" => AccessTier.Existence,
        "read" => AccessTier.Read,
        "read_write" => AccessTier.ReadWrite,
        "admin" => AccessTier.Admin,
        _ => AccessTier.Read,
    };
}
