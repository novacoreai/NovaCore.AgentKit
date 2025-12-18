namespace NovaCore.AgentKit.EntityFramework.Models;

/// <summary>
/// Serializable representation of multimodal content for storage
/// </summary>
public class SerializableContent
{
    public string Type { get; set; } = null!; // "text", "data"
    public string? Text { get; set; }
    public string? Base64Data { get; set; }
    public string? MediaType { get; set; }
}

