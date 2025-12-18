namespace NovaCore.AgentKit.Core;

/// <summary>
/// Factory for creating MCP client instances
/// </summary>
public interface IMcpClientFactory
{
    /// <summary>
    /// Create isolated MCP client instance
    /// </summary>
    Task<IMcpClient> CreateClientAsync(
        IMcpConfiguration config, 
        CancellationToken ct = default);
}

