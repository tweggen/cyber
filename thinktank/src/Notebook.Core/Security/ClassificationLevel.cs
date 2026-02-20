using System.Text.Json.Serialization;

namespace Notebook.Core.Security;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClassificationLevel
{
    Public = 0,
    Internal = 1,
    Confidential = 2,
    Secret = 3,
    TopSecret = 4,
}

public static class ClassificationLevelExtensions
{
    public static string ToDbString(this ClassificationLevel level) => level switch
    {
        ClassificationLevel.Public => "PUBLIC",
        ClassificationLevel.Internal => "INTERNAL",
        ClassificationLevel.Confidential => "CONFIDENTIAL",
        ClassificationLevel.Secret => "SECRET",
        ClassificationLevel.TopSecret => "TOP_SECRET",
        _ => "INTERNAL",
    };

    public static ClassificationLevel ParseClassification(string value) => value.ToUpperInvariant() switch
    {
        "PUBLIC" => ClassificationLevel.Public,
        "INTERNAL" => ClassificationLevel.Internal,
        "CONFIDENTIAL" => ClassificationLevel.Confidential,
        "SECRET" => ClassificationLevel.Secret,
        "TOP_SECRET" => ClassificationLevel.TopSecret,
        _ => ClassificationLevel.Internal,
    };
}
