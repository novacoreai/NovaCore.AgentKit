using Microsoft.Extensions.AI;

namespace NovaCore.AgentKit.Core;

/// <summary>
/// Represents a file attachment (image, document, etc.) for multimodal messages
/// </summary>
public class FileAttachment
{
    /// <summary>
    /// File data as bytes
    /// </summary>
    public required byte[] Data { get; init; }
    
    /// <summary>
    /// Media type (MIME type) - e.g., "image/png", "image/jpeg", "application/pdf"
    /// </summary>
    public required string MediaType { get; init; }
    
    /// <summary>
    /// Optional filename for context
    /// </summary>
    public string? FileName { get; init; }
    
    /// <summary>
    /// Create a file attachment from byte array
    /// </summary>
    public static FileAttachment FromBytes(byte[] data, string mediaType, string? fileName = null)
    {
        return new FileAttachment
        {
            Data = data,
            MediaType = mediaType,
            FileName = fileName
        };
    }
    
    /// <summary>
    /// Create a file attachment from base64 string
    /// </summary>
    public static FileAttachment FromBase64(string base64Data, string mediaType, string? fileName = null)
    {
        var data = Convert.FromBase64String(base64Data);
        return new FileAttachment
        {
            Data = data,
            MediaType = mediaType,
            FileName = fileName
        };
    }
    
    /// <summary>
    /// Create a file attachment from a file path
    /// </summary>
    public static async Task<FileAttachment> FromFileAsync(string filePath, CancellationToken ct = default)
    {
        var data = await File.ReadAllBytesAsync(filePath, ct);
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        var mediaType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
        
        return new FileAttachment
        {
            Data = data,
            MediaType = mediaType,
            FileName = fileName
        };
    }
    
    /// <summary>
    /// Convert to message content
    /// </summary>
    public ImageMessageContent ToMessageContent()
    {
        return new ImageMessageContent(Data, MediaType);
    }
    
    /// <summary>
    /// Check if this is an image file
    /// </summary>
    public bool IsImage => MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Get file size in bytes
    /// </summary>
    public long Size => Data.Length;
    
    /// <summary>
    /// Get base64 representation
    /// </summary>
    public string ToBase64() => Convert.ToBase64String(Data);
}

