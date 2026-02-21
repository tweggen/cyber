namespace YourCyber.Models;

public sealed class ServerProfile
{
    public string Name { get; set; } = "";
    public string ServerUrl { get; set; } = "";
    public Guid NotebookId { get; set; }
    public string Token { get; set; } = "";
}

public sealed class ProfileStore
{
    public string ActiveProfileName { get; set; } = "Default";
    public List<ServerProfile> Profiles { get; set; } = [];
}
