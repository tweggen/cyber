using System.Text.Json.Serialization;

namespace Notebook.Server.Models;

public class RegisterAuthorRequest
{
    [JsonPropertyName("public_key")]
    public string PublicKey { get; set; } = string.Empty;
}

public class RegisterAuthorResponse
{
    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;
}

public class AuthorResponse
{
    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("public_key")]
    public string PublicKey { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public string Created { get; set; } = string.Empty;
}
