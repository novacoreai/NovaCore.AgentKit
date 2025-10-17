using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.MCP;

/// <summary>
/// Factory for creating MCP clients using the official ModelContextProtocol SDK
/// </summary>
public class McpClientFactory : IMcpClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    
    public McpClientFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }
    
    public async Task<IMcpClient> CreateClientAsync(
        IMcpConfiguration config, 
        CancellationToken ct = default)
    {
        var logger = _loggerFactory.CreateLogger<McpClient>();
        var client = await McpClient.CreateAsync(config, logger, ct);
        
        return client;
    }
}

