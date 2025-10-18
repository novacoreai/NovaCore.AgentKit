# NovaCore.AgentKit

**Production-ready AI agents for .NET** - Build ChatAgents for conversations or ReActAgents for autonomous tasks. Clean API, full-featured, 6 LLM providers.

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

---

## Quick Start

```bash
dotnet add package NovaCore.AgentKit.Core
dotnet add package NovaCore.AgentKit.Providers.Anthropic
```

```csharp
await using var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, AnthropicModels.ClaudeSonnet45)
    .WithSystemPrompt("You are a helpful assistant.")
    .BuildChatAgentAsync();

var response = await agent.SendAsync("What is 2+2?");
Console.WriteLine(response.Text);  // "4"
```

---

## Agent Types

| **ChatAgent** | **ReActAgent** |
|---------------|----------------|
| Stateful conversations | Ephemeral tasks |
| Persistent across sessions | Autonomous execution |
| Human-in-the-loop (UI tools) | Tool-driven reasoning |
| Chat apps, support bots | Research, automation |

---

## ChatAgent API

### Basic Usage

```csharp
var agent = await new AgentBuilder()
    .UseOpenAI(apiKey, OpenAIModels.GPT4o)
    .WithSystemPrompt("You are a helpful assistant.")
    .ForConversation("user-123")
    .BuildChatAgentAsync();

var response = await agent.SendAsync("Hello!");
```

### Core Methods

```csharp
// Send messages
Task<ChatMessage> SendAsync(ChatMessage message)
Task<ChatMessage> SendAsync(string text)
Task<ChatMessage> SendAsync(string text, List<FileAttachment> files)

// Checkpoints
Task CreateCheckpointAsync(string summary, int? upToTurnNumber = null)
Task<ConversationCheckpoint?> GetLatestCheckpointAsync()

// Stats & Management
HistoryStats GetStats()
void ClearHistory()
string ConversationId { get; }
ValueTask DisposeAsync()
```

### ChatMessage Constructors

```csharp
new ChatMessage(ChatRole.User, "Hello")                      // Text
new ChatMessage(ChatRole.User, List<IMessageContent>)        // Multimodal
new ChatMessage(ChatRole.Tool, resultJson, toolCallId)       // Tool result
new ChatMessage(ChatRole.Assistant, text, List<ToolCall>)    // With tool calls
```

### Persistence

```csharp
// EF Core setup
public class MyDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureAgentKitModels();
    }
}

var historyStore = new EfCoreHistoryStore<MyDbContext>(
    dbContext, logger, 
    tenantId: "tenant-1",  // Multi-tenancy
    userId: "user-123");

var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithHistoryStore(historyStore)
    .ForConversation("chat-123")
    .BuildChatAgentAsync();  // Auto-loads existing history
```

### Automatic Summarization

```csharp
.WithSummarization(cfg =>
{
    cfg.Enabled = true;
    cfg.TriggerAt = 100;         // Summarize when we hit 100 messages
    cfg.KeepRecent = 10;         // Keep last 10 (summarize first 90)
    cfg.SummarizationTool = summaryTool;
})

// How it works:
// At 100 msgs → Summarize first 90 → Keep last 10 in memory
// Database retains ALL messages + checkpoint
```

### Tool Result Filtering

```csharp
.WithToolResultFiltering(cfg =>
{
    cfg.KeepRecent = 5;  // Keep last 5 tool results, replace others with "[Omitted]"
})

// Recommended: Browser agents: 1-3, ReAct: 5-10, Chat: 5
// Reduces tokens by 80-90% while preserving structure
```

### Multimodal

```csharp
var image = await FileAttachment.FromFileAsync("photo.png");
await agent.SendAsync("What's in this image?", new List<FileAttachment> { image });

// From bytes
var image = FileAttachment.FromBytes(data, "image/jpeg");

// From base64
var image = FileAttachment.FromBase64(base64, "image/png");
```

---

## ReActAgent API

### Basic Usage

```csharp
var agent = await new AgentBuilder()
    .UseOpenAI(apiKey, OpenAIModels.GPT4o)
    .AddTool(new SearchTool())
    .WithReActConfig(cfg => cfg.MaxTurns = 20)
    .BuildReActAgentAsync();

var result = await agent.RunAsync("Find Bitcoin price and calculate 10% of it");
Console.WriteLine(result.FinalAnswer);
```

### Core Methods

```csharp
Task<ReActResult> RunAsync(string task)
ValueTask DisposeAsync()
```

### ReActConfig

```csharp
.WithReActConfig(cfg =>
{
    cfg.MaxTurns = 20;               // Stop after N turns
    cfg.DetectStuckAgent = true;     // Detect no progress
    cfg.BreakOnStuck = false;        // Continue even if stuck
})
```

### ReActResult

```csharp
result.Success                // bool
result.FinalAnswer            // string
result.TurnsExecuted          // int
result.TotalLlmCalls          // int
result.Duration               // TimeSpan
result.Error                  // string?
```

---

## Tools

### Tool Types

```csharp
// Strongly-typed tool
public class WeatherTool : Tool<WeatherArgs, WeatherResult>
{
    public override string Name => "get_weather";
    public override string Description => "Get current weather";
    
    protected override async Task<WeatherResult> ExecuteAsync(
        WeatherArgs args, CancellationToken ct)
    {
        var temp = await FetchWeatherAsync(args.Location);
        return new WeatherResult(Temperature: temp, Condition: "Sunny");
    }
}

public record WeatherArgs(string Location);
public record WeatherResult(double Temperature, string Condition);

// Simple tool (auto success/error handling)
public class NotifyTool : SimpleTool<NotifyArgs>
{
    public override string Name => "notify";
    public override string Description => "Send notification";
    
    protected override Task<string> RunAsync(NotifyArgs args, CancellationToken ct)
    {
        return Task.FromResult($"Sent: {args.Message}");
    }
}

// UI tool (human-in-the-loop)
public class ShowPaymentPageTool : UITool<PaymentArgs, PaymentResult>
{
    public override string Name => "show_payment_page";
    public override string Description => "Display payment UI";
}

// Manual ITool (full control)
public class CustomTool : ITool
{
    public string Name => "custom";
    public string Description => "Custom logic";
    public JsonDocument ParameterSchema => JsonDocument.Parse(@"{...}");
    public async Task<string> InvokeAsync(string argsJson, CancellationToken ct) { }
}
```

### UI Tools (Human-in-the-Loop)

```csharp
.AddUITool(new ShowPaymentPageTool())

// Execution pauses at UI tool
var response = await agent.SendAsync("I want to pay");
if (response.ToolCalls?.Any() == true)
{
    var toolCall = response.ToolCalls[0];
    var args = JsonSerializer.Deserialize<PaymentArgs>(toolCall.Arguments);
    
    // Show UI to user
    var result = await ShowPaymentUIAsync(args);
    
    // Send result back
    var toolResult = new ChatMessage(
        ChatRole.Tool,
        JsonSerializer.Serialize(result),
        toolCall.Id);
    
    var finalResponse = await agent.SendAsync(toolResult);
}
```

---

## LLM Providers

### Anthropic Claude

```csharp
.UseAnthropic(options =>
{
    options.ApiKey = apiKey;
    options.Model = AnthropicModels.ClaudeSonnet45;
    options.UseExtendedThinking = true;      // Deep reasoning
    options.EnablePromptCaching = true;      // Cost optimization
    options.ThinkingBudgetTokens = 10000;
    options.MaxTokens = 4096;
    options.Temperature = 1.0;
    options.TopP = 0.9;
    options.TopK = 40;
})

// Models
AnthropicModels.ClaudeSonnet45      // claude-sonnet-4-5-20250929
AnthropicModels.ClaudeSonnet4       // claude-sonnet-4-20250514
AnthropicModels.ClaudeSonnet37      // claude-3-7-sonnet-20250219
AnthropicModels.ClaudeSonnet35      // claude-3-5-sonnet-20241022
AnthropicModels.ClaudeHaiku35       // claude-3-5-haiku-20241022
```

### OpenAI

```csharp
.UseOpenAI(options =>
{
    options.ApiKey = apiKey;
    options.Model = OpenAIModels.GPT4o;
    options.UseStructuredOutputs = true;
    options.Seed = 42;                       // Deterministic
    options.ResponseFormat = "json_object";
    options.MaxTokens = 4096;
    options.Temperature = 0.7;
    options.FrequencyPenalty = 0.5;
})

// Models
OpenAIModels.GPT4o                  // gpt-4o
OpenAIModels.GPT4oMini              // gpt-4o-mini
OpenAIModels.O1                     // o1
OpenAIModels.O1Mini                 // o1-mini
OpenAIModels.GPT4Turbo              // gpt-4-turbo
```

### Google Gemini

```csharp
.UseGoogle(options =>
{
    options.ApiKey = apiKey;                 // Google AI Studio
    options.Model = GoogleModels.Gemini25Flash;
    options.EnableGrounding = true;          // Google Search
    options.SafetyLevel = "BLOCK_MEDIUM_AND_ABOVE";
    
    // OR Vertex AI
    options.UseVertexAI = true;
    options.ProjectId = "my-project";
    options.Location = "us-central1";
    options.CredentialsJson = credentialsJson;
})

// Models
GoogleModels.Gemini25Pro            // gemini-2.5-pro
GoogleModels.Gemini25Flash          // gemini-2.5-flash
GoogleModels.Gemini25FlashLite      // gemini-2.5-flash-lite
GoogleModels.GeminiFlashLatest      // gemini-flash-latest
```

### xAI Grok

```csharp
.UseXAI(options =>
{
    options.ApiKey = apiKey;
    options.Model = XAIModels.Grok4FastNonReasoning;
})

// Models
XAIModels.Grok4FastReasoning        // grok-4-fast-reasoning
XAIModels.Grok4FastNonReasoning     // grok-4-fast-non-reasoning
XAIModels.GrokCodeFast1             // grok-code-fast-1
```

### Groq (Ultra-fast)

```csharp
.UseGroq(options =>
{
    options.ApiKey = apiKey;
    options.Model = GroqModels.Llama3_3_70B;
})

// Models (Recommended: Llama 3.3 or Qwen for tool calling)
GroqModels.Llama3_3_70B             // llama-3.3-70b-versatile (128K, excellent tool calling)
GroqModels.Llama3_1_70B             // llama-3.1-70b-versatile (reliable tool calling)
GroqModels.Qwen3_32B                // qwen/qwen3-32b (128K context)
GroqModels.Llama3_1_8B              // llama-3.1-8b-instant (fast)
```

### OpenRouter (Any Model)

```csharp
.UseOpenRouter(options =>
{
    options.ApiKey = apiKey;
    options.Model = "anthropic/claude-3.5-sonnet";
    options.AllowFallbacks = true;
    options.ProviderPreferences = new List<string> { "Anthropic", "OpenAI" };
    options.HttpReferer = "https://myapp.com";
    options.AppTitle = "MyApp";
})

// Any model string supported by OpenRouter
```

---

## MCP (Model Context Protocol)

Add powerful tools with zero code:

```csharp
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> { "-y", "@playwright/mcp" }
};

var mcpFactory = new McpClientFactory(loggerFactory);

var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithMcpClientFactory(mcpFactory)
    .WithMcp(mcpConfig)  // 21 browser automation tools added
    .BuildChatAgentAsync();

await agent.SendAsync("Go to example.com and take a screenshot");
```

**Popular MCP servers:**
- `@playwright/mcp` - Browser automation
- `@modelcontextprotocol/server-filesystem` - File operations
- `@modelcontextprotocol/server-github` - GitHub API
- `@modelcontextprotocol/server-slack` - Slack integration

---

## Configuration

### AgentConfig

```csharp
.WithConfig(cfg =>
{
    cfg.SystemPrompt = "You are a helpful assistant";
    cfg.MaxToolRoundsPerTurn = 10;
    cfg.EnableTurnValidation = true;        // Auto-fix invalid sequences
    cfg.EnableOutputSanitization = true;    // Clean model outputs
})
```

### SanitizationOptions

```csharp
cfg.Sanitization.RemoveThinkingTags = true;      // Remove <thinking> tags
cfg.Sanitization.UnwrapJsonFromMarkdown = true;  // Extract from ```json
cfg.Sanitization.TrimWhitespace = true;
cfg.Sanitization.RemoveNullCharacters = true;
```

### Observer Pattern (Observability)

Get real-time visibility into agent execution:

```csharp
public class MyObserver : IAgentObserver
{
    public void OnLlmRequest(LlmRequestEvent evt)
    {
        Console.WriteLine($"→ LLM Request: {evt.Messages.Count} messages, {evt.ToolCount} tools");
        
        // Monitor token usage to prevent context overflow
        var estimatedTokens = EstimateTokens(evt.Messages);
        if (estimatedTokens > 100_000)
            _logger.LogWarning("Large context: {Tokens} tokens", estimatedTokens);
    }
    
    public void OnLlmResponse(LlmResponseEvent evt)
    {
        Console.WriteLine($"← LLM Response: {evt.Usage?.TotalTokens} tokens in {evt.Duration.TotalSeconds:F2}s");
        
        // Cost tracking
        if (evt.Usage != null)
            _costTracker.RecordUsage(evt.Usage.InputTokens, evt.Usage.OutputTokens);
    }
    
    public void OnToolExecutionStart(ToolExecutionStartEvent evt)
    {
        Console.WriteLine($"🔧 Tool starting: {evt.ToolName}");
    }
    
    public void OnToolExecutionComplete(ToolExecutionCompleteEvent evt)
    {
        Console.WriteLine($"✓ Tool completed: {evt.ToolName} ({evt.Duration.TotalSeconds:F2}s)");
        
        // Log errors
        if (evt.Error != null)
            _logger.LogError(evt.Error, "Tool {Tool} failed", evt.ToolName);
    }
    
    public void OnTurnStart(TurnStartEvent evt)
    {
        Console.WriteLine($"Turn starting: {evt.UserMessage}");
    }
    
    public void OnTurnComplete(TurnCompleteEvent evt)
    {
        Console.WriteLine($"Turn complete: {evt.Duration.TotalSeconds:F2}s");
    }
    
    public void OnError(ErrorEvent evt)
    {
        _logger.LogError(evt.Exception, "Error in {Phase}", evt.Phase);
    }
}

// Use observer
.WithObserver(new MyObserver())
```

**Available Events:**

| Event | Fires When | Key Data |
|-------|-----------|----------|
| `OnTurnStart` | Turn begins | `UserMessage` |
| `OnTurnComplete` | Turn ends | `AgentTurn`, `Duration` |
| `OnLlmRequest` | Before LLM API call | `Messages`, `ToolCount` |
| `OnLlmResponse` | After LLM responds | `Text`, `ToolCalls`, `Usage`, `Duration` |
| `OnToolExecutionStart` | Tool starts | `ToolName`, `Arguments` |
| `OnToolExecutionComplete` | Tool finishes | `ToolName`, `Result`, `Duration`, `Error?` |
| `OnError` | Any error occurs | `Exception`, `Phase` |

**Event Context (all events):**
```csharp
evt.Context.Timestamp         // DateTime
evt.Context.ConversationId    // string? (null for ReActAgent)
evt.Context.MessageCount      // int
```

**Common Use Cases:**

```csharp
// Token monitoring
public void OnLlmRequest(LlmRequestEvent evt)
{
    if (EstimateTokens(evt.Messages) > 500_000)
        throw new InvalidOperationException("Token limit exceeded!");
}

// Cost tracking
public void OnLlmResponse(LlmResponseEvent evt)
{
    _costs.Add(CalculateCost(evt.Usage));
}

// Progress UI
public void OnToolExecutionStart(ToolExecutionStartEvent evt)
{
    _progressBar.UpdateStatus($"Running {evt.ToolName}...");
}

// Debug trace
public void OnLlmRequest(LlmRequestEvent evt)
{
    File.AppendAllText("trace.log", 
        $"{evt.Context.Timestamp}: LLM call with {evt.Messages.Count} messages\n");
}
```

---

## AgentBuilder API

```csharp
// LLM Provider (required - choose one)
.UseAnthropic(apiKey, model)
.UseAnthropic(options => { })
.UseOpenAI(apiKey, model)
.UseOpenAI(options => { })
.UseGoogle(apiKey, model)
.UseGoogle(options => { })
.UseXAI(apiKey, model)
.UseXAI(options => { })
.UseGroq(apiKey, model)
.UseGroq(options => { })
.UseOpenRouter(apiKey, model)
.UseOpenRouter(options => { })
.UseLlmClient(customClient)

// Tools
.AddTool(tool)                    // Internal execution
.AddTools(tools)
.AddUITool(uiTool)                // Human-in-the-loop (pauses)
.AddUITools(uiTools)
.WithMcp(mcpConfig)               // MCP server (auto-discovery)
.WithMcpClientFactory(factory)

// Configuration
.WithSystemPrompt(prompt)
.ForConversation(id)              // ChatAgent only
.WithHistoryStore(store)          // ChatAgent only
.WithSummarization(cfg)           // ChatAgent only
.WithToolResultFiltering(cfg)     // Tool output filtering
.WithConfig(cfg)                  // Agent behavior
.WithObserver(observer)           // Observability events
.WithReActConfig(cfg)             // ReActAgent settings
.WithHistoryManager(manager)      // Custom history

// Build
.BuildChatAgentAsync()            // Interactive conversations
.BuildReActAgentAsync()           // Autonomous tasks
```

---

## Statistics & Monitoring

### History Statistics

```csharp
var stats = agent.GetStats();
stats.TotalMessages                // int
stats.UserMessages                 // int
stats.AssistantMessages            // int
stats.ToolMessages                 // int
stats.EstimatedTokens              // int
stats.CompressionCount             // int
```

### Real-Time Monitoring (Observer)

```csharp
public class MonitoringObserver : IAgentObserver
{
    private int _totalTokens = 0;
    private decimal _totalCost = 0;
    private readonly Stopwatch _sessionTimer = Stopwatch.StartNew();
    
    public void OnLlmResponse(LlmResponseEvent evt)
    {
        if (evt.Usage != null)
        {
            _totalTokens += evt.Usage.TotalTokens;
            _totalCost += CalculateCost(evt.Usage);
            
            Console.WriteLine($"Session: {_totalTokens} tokens, ${_totalCost:F4}, {_sessionTimer.Elapsed}");
        }
    }
}
```

---

## Advanced Features

### Custom History Store

```csharp
public class RedisHistoryStore : IHistoryStore
{
    public Task AppendMessageAsync(string conversationId, ChatMessage message, CancellationToken ct) { }
    public Task<List<ChatMessage>?> LoadAsync(string conversationId, CancellationToken ct) { }
    public Task DeleteAsync(string conversationId, CancellationToken ct) { }
    public Task<List<string>> ListConversationsAsync(CancellationToken ct) { }
    public Task CreateCheckpointAsync(string conversationId, ConversationCheckpoint checkpoint, CancellationToken ct) { }
    public Task<ConversationCheckpoint?> GetLatestCheckpointAsync(string conversationId, CancellationToken ct) { }
}

.WithHistoryStore(new RedisHistoryStore(redis))
```

### Cost Tracking

Use the observer pattern for real-time cost tracking:

```csharp
public class CostTracker : IAgentObserver
{
    private decimal _totalCost = 0;
    
    public void OnLlmResponse(LlmResponseEvent evt)
    {
        if (evt.Usage != null)
        {
            var cost = CalculateCost(
                evt.Usage.InputTokens, 
                evt.Usage.OutputTokens,
                modelPricing);
            
            _totalCost += cost;
            _logger.LogInformation("Cost this turn: ${Cost:F4}, Total: ${Total:F4}", 
                cost, _totalCost);
        }
    }
    
    public decimal GetTotalCost() => _totalCost;
}

.WithObserver(new CostTracker())
```

### Rate Limiting

Implement rate limiting using the observer pattern:

```csharp
public class RateLimitingObserver : IAgentObserver
{
    private readonly SemaphoreSlim _semaphore = new(10, 10); // 10 concurrent requests
    private readonly Queue<DateTime> _requestTimes = new();
    private readonly int _maxRequestsPerMinute = 60;
    
    public void OnLlmRequest(LlmRequestEvent evt)
    {
        // Wait for rate limit slot
        lock (_requestTimes)
        {
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            while (_requestTimes.Count > 0 && _requestTimes.Peek() < oneMinuteAgo)
                _requestTimes.Dequeue();
            
            if (_requestTimes.Count >= _maxRequestsPerMinute)
            {
                var waitTime = _requestTimes.Peek().AddMinutes(1) - DateTime.UtcNow;
                Thread.Sleep(waitTime);
            }
            
            _requestTimes.Enqueue(DateTime.UtcNow);
        }
    }
}
```

### OpenTelemetry Integration

```csharp
public class TelemetryObserver : IAgentObserver
{
    private readonly ActivitySource _activitySource = new("NovaCore.AgentKit");
    
    public void OnTurnStart(TurnStartEvent evt)
    {
        var activity = _activitySource.StartActivity("AgentTurn");
        activity?.SetTag("conversation_id", evt.Context.ConversationId);
    }
    
    public void OnLlmResponse(LlmResponseEvent evt)
    {
        Activity.Current?.SetTag("tokens", evt.Usage?.TotalTokens);
    }
    
    public void OnError(ErrorEvent evt)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error, evt.Exception.Message);
    }
}
```

---

## Key Types & Interfaces

### Core

```csharp
// Messages
ChatMessage(ChatRole role, string? text, string? toolCallId = null)
ChatMessage(ChatRole role, List<IMessageContent> contents)
ChatMessage(ChatRole role, string? text, List<ToolCall>? toolCalls)
enum ChatRole { System, User, Assistant, Tool }

// Tools
interface ITool { string Name; string Description; JsonDocument ParameterSchema; Task<string> InvokeAsync(string argsJson, CancellationToken ct); }
interface IUITool : ITool { }
class Tool<TArgs, TResult> : ITool
class SimpleTool<TArgs> : Tool<TArgs, ToolResponse>
class UITool<TArgs, TResult> : Tool<TArgs, TResult>, IUITool

// Tool Calls
class ToolCall { string Id; string FunctionName; string Arguments; }

// Attachments
class FileAttachment { byte[] Data; string MediaType; string? FileName; }
static FileAttachment.FromFileAsync(string path)
static FileAttachment.FromBytes(byte[] data, string mediaType)
static FileAttachment.FromBase64(string base64, string mediaType)

// LLM
interface ILlmClient
class LlmMessage { MessageRole Role; string? Text; List<IMessageContent>? Contents; string? ToolCallId; }
class LlmOptions { int? MaxTokens; double? Temperature; double? TopP; Dictionary<string, LlmTool>? Tools; }
class LlmResponse { string? Text; List<LlmToolCall>? ToolCalls; LlmFinishReason? FinishReason; LlmUsage? Usage; }
class LlmStreamingUpdate { string? TextDelta; LlmToolCall? ToolCall; LlmFinishReason? FinishReason; LlmUsage? Usage; }
class LlmUsage { int InputTokens; int OutputTokens; int TotalTokens; }
```

### History

```csharp
interface IHistoryStore { Task AppendMessageAsync(...); Task<List<ChatMessage>?> LoadAsync(...); Task DeleteAsync(...); Task<List<string>> ListConversationsAsync(...); Task CreateCheckpointAsync(...); Task<ConversationCheckpoint?> GetLatestCheckpointAsync(...); }
interface IHistoryManager { void AddMessage(ChatMessage message); List<ChatMessage> GetHistory(); void ReplaceHistory(List<ChatMessage> history); void Clear(); HistoryStats GetStats(); }
interface IHistorySelector { List<ChatMessage> SelectMessagesForContext(List<ChatMessage> fullHistory, ToolResultConfig config); }

class ConversationCheckpoint { int UpToTurnNumber; string Summary; DateTime CreatedAt; Dictionary<string, object>? Metadata; }
class HistoryStats { int TotalMessages; int UserMessages; int AssistantMessages; int ToolMessages; int EstimatedTokens; int CompressionCount; }

class SummarizationConfig { bool Enabled; int TriggerAt; int KeepRecent; ITool? SummarizationTool; ToolResultConfig ToolResults; }
class ToolResultConfig { int KeepRecent; }
```

### MCP

```csharp
interface IMcpClient : IAsyncDisposable { Task ConnectAsync(); Task<List<McpToolDefinition>> DiscoverToolsAsync(); Task<McpToolResult> CallToolAsync(string toolName, Dictionary<string, object?> arguments, CancellationToken ct); }
interface IMcpConfiguration { string Command; List<string> Arguments; Dictionary<string, string> Environment; string? WorkingDirectory; }
class McpConfiguration : IMcpConfiguration
```

### Observer

```csharp
interface IAgentObserver
{
    void OnTurnStart(TurnStartEvent evt);
    void OnTurnComplete(TurnCompleteEvent evt);
    void OnLlmRequest(LlmRequestEvent evt);
    void OnLlmResponse(LlmResponseEvent evt);
    void OnToolExecutionStart(ToolExecutionStartEvent evt);
    void OnToolExecutionComplete(ToolExecutionCompleteEvent evt);
    void OnError(ErrorEvent evt);
}

// Event Types
record AgentEventContext { DateTime Timestamp; string? ConversationId; int MessageCount; }
record TurnStartEvent(AgentEventContext Context, string UserMessage);
record TurnCompleteEvent(AgentEventContext Context, AgentTurn Result, TimeSpan Duration);
record LlmRequestEvent(AgentEventContext Context, IReadOnlyList<LlmMessage> Messages, int ToolCount);
record LlmResponseEvent(AgentEventContext Context, string? Text, List<LlmToolCall>? ToolCalls, LlmUsage? Usage, LlmFinishReason? FinishReason, TimeSpan Duration);
record ToolExecutionStartEvent(AgentEventContext Context, string ToolName, string Arguments);
record ToolExecutionCompleteEvent(AgentEventContext Context, string ToolName, string Result, TimeSpan Duration, Exception? Error);
record ErrorEvent(AgentEventContext Context, Exception Exception, string Phase);
```

### Configuration

```csharp
class AgentConfig { int MaxToolRoundsPerTurn; string? SystemPrompt; SummarizationConfig Summarization; ToolResultConfig ToolResults; SanitizationOptions Sanitization; bool EnableTurnValidation; bool EnableOutputSanitization; }

class SanitizationOptions { bool RemoveThinkingTags; bool UnwrapJsonFromMarkdown; bool TrimWhitespace; bool RemoveNullCharacters; }

class ReActConfig { int MaxTurns; bool DetectStuckAgent; bool BreakOnStuck; }

class ReActResult { bool Success; string FinalAnswer; int TurnsExecuted; int TotalLlmCalls; TimeSpan Duration; string? Error; }

class AgentTurn { string Response; int LlmCallsExecuted; string? CompletionSignal; bool Success; string? Error; }
```

---

## Packages

### Core
- **NovaCore.AgentKit.Core** - Core abstractions (required)
- **NovaCore.AgentKit.MCP** - Model Context Protocol
- **NovaCore.AgentKit.EntityFramework** - Persistence

### Providers
- **NovaCore.AgentKit.Providers.Anthropic** - Claude
- **NovaCore.AgentKit.Providers.OpenAI** - GPT-4o, o1
- **NovaCore.AgentKit.Providers.Google** - Gemini
- **NovaCore.AgentKit.Providers.XAI** - Grok
- **NovaCore.AgentKit.Providers.Groq** - Qwen, Llama
- **NovaCore.AgentKit.Providers.OpenRouter** - Any model

### Extensions
- **NovaCore.AgentKit.Extensions.OpenTelemetry** - Telemetry (interfaces available)

---

## License

MIT License - see LICENSE file

---

**Built with ❤️ by NovaCore AI Team** - 90% LLM generated
