using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SdkMcpClient = ModelContextProtocol.Client.McpClient;
using ModelContextProtocol.Client;
using ModelContextProtocol;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.MCP;

/// <summary>
/// MCP client implementation using the official ModelContextProtocol SDK
/// </summary>
public class McpClient : Core.IMcpClient
{
    private readonly SdkMcpClient _client;
    private readonly ILogger<McpClient> _logger;
    private McpConnectionStatus _status = McpConnectionStatus.Connected;
    
    private McpClient(SdkMcpClient client, ILogger<McpClient> logger)
    {
        _client = client;
        _logger = logger;
    }
    
    public static async Task<McpClient> CreateAsync(
        IMcpConfiguration config,
        ILogger<McpClient> logger,
        CancellationToken ct = default)
    {
        logger.LogInformation("Creating MCP client: {Command} {Args}", 
            config.Command, string.Join(" ", config.Arguments));
        
        var transportOptions = new StdioClientTransportOptions
        {
            Name = "NovaCore.AgentKit",
            Command = config.Command,
            Arguments = config.Arguments.ToArray(),
        };
        
        // Note: StdioClientTransportOptions doesn't expose Environment or WorkingDirectory
        // The SDK handles environment and working directory internally
        // If these are critical, we may need to use ProcessStartInfo directly
        
        var transport = new StdioClientTransport(transportOptions);
        var mcpClient = await SdkMcpClient.CreateAsync(transport, cancellationToken: ct);
        
        logger.LogInformation("MCP client connected successfully");
        
        return new McpClient(mcpClient, logger);
    }
    
    public Task ConnectAsync(CancellationToken ct = default)
    {
        // Connection is already established in CreateAsync
        return Task.CompletedTask;
    }
    
    public async Task<List<McpToolDefinition>> DiscoverToolsAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Discovering MCP tools with proper schemas");
        
        var mcpTools = await _client.ListToolsAsync(cancellationToken: ct);
        
        var result = new List<McpToolDefinition>();
        
        foreach (var tool in mcpTools)
        {
            // Access the JsonSchema property via reflection to get the actual input schema
            var toolType = tool.GetType();
            var jsonSchemaProperty = toolType.GetProperty("JsonSchema", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            JsonDocument schemaDoc;
            
            if (jsonSchemaProperty != null)
            {
                var schemaValue = jsonSchemaProperty.GetValue(tool);
                if (schemaValue is JsonElement jsonElement)
                {
                    // Got the actual schema from the MCP protocol!
                    var schemaJson = jsonElement.GetRawText();
                    schemaDoc = JsonDocument.Parse(schemaJson);
                    _logger.LogDebug("Tool '{Name}' has schema with properties", tool.Name);
                }
                else
                {
                    // Fallback
                    _logger.LogWarning("Tool '{Name}' JsonSchema property returned unexpected type: {Type}", 
                        tool.Name, schemaValue?.GetType().Name ?? "null");
                    schemaDoc = CreateFallbackSchema(tool.Description);
                }
            }
            else
            {
                _logger.LogWarning("Tool '{Name}' does not have JsonSchema property", tool.Name);
                schemaDoc = CreateFallbackSchema(tool.Description);
            }
            
            result.Add(new McpToolDefinition
            {
                Name = tool.Name,
                Description = tool.Description ?? string.Empty,
                InputSchema = schemaDoc
            });
        }
        
        _logger.LogInformation("Discovered {Count} MCP tools with full schemas", result.Count);
        return result;
    }
    
    private static JsonDocument CreateFallbackSchema(string? description)
    {
        var fallbackJson = JsonSerializer.Serialize(new
        {
            type = "object",
            description = description ?? string.Empty,
            properties = new Dictionary<string, object>(),
            additionalProperties = true
        });
        return JsonDocument.Parse(fallbackJson);
    }
    
    public async Task<McpToolResult> CallToolAsync(
        string toolName, 
        Dictionary<string, object?> arguments, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Calling MCP tool: {Tool} with args: {Args}", 
            toolName, JsonSerializer.Serialize(arguments));
        
        try
        {
            var result = await _client.CallToolAsync(toolName, arguments, progress: null, cancellationToken: ct);
            
            _logger.LogDebug("Tool {Tool} returned result", toolName);
            
            // Convert the result content to JSON WITHOUT unicode escaping
            // This is important to save tokens and improve model readability
            var serializerOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            };
            
            var resultJson = JsonSerializer.Serialize(result, serializerOptions);
            var resultDoc = JsonDocument.Parse(resultJson);
            
            return new McpToolResult
            {
                Success = true,
                Data = resultDoc.RootElement
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP tool call failed: {Tool} with args {Args}", 
                toolName, JsonSerializer.Serialize(arguments));
            
            return new McpToolResult
            {
                Success = false,
                Error = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
    }
    
    public McpConnectionStatus GetStatus() => _status;
    
    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing MCP client");
        
        try
        {
            await _client.DisposeAsync();
            _status = McpConnectionStatus.Disconnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing MCP client");
            _status = McpConnectionStatus.Error;
        }
        
        GC.SuppressFinalize(this);
    }
}

