namespace NovaCore.AgentKit.Tests.Tools;

/// <summary>
/// Simple image optimizer for tests (just passes through for now)
/// </summary>
public static class ImageOptimizer
{
    public static Task<byte[]> OptimizeScreenshotAsync(
        byte[] screenshotBytes,
        int sizeReductionPercent = 80,
        int jpegQuality = 80)
    {
        // For tests, just return the original bytes
        // In production, this would compress/resize the image
        return Task.FromResult(screenshotBytes);
    }

    public static string GetOptimizedMimeType()
    {
        return "image/png"; // For tests, keep as PNG
    }
}
