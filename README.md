# NovaCore.AgentKit

**Production-ready AI agents for .NET** - Build ChatAgents for conversations or ReActAgents for autonomous tasks. Clean API, full-featured, 6 LLM providers. **Built-in real-time cost tracking.**

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

### Multimodal History Limiting

Limit images/screenshots retained in history (useful for computer use scenarios):

```csharp
// Keep only the most recent screenshot in context
.WithMaxMultimodalMessages(1)

// Or via config
.WithConfig(cfg => cfg.MaxMultimodalMessages = 1)

// Default: null (no limit - all multimodal content retained)
// Older images are stripped but text content is preserved
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
// Run with multimodal content (text + images)
Task<ReActResult> RunAsync(ChatMessage taskMessage)

// Convenience: Text only
Task<ReActResult> RunAsync(string task)

// Convenience: Text + file attachments
Task<ReActResult> RunAsync(string task, List<FileAttachment> files)

ValueTask DisposeAsync()
```

### Multimodal Support

```csharp
// Send images with task
var image = await FileAttachment.FromFileAsync("screenshot.png");
var result = await agent.RunAsync("Analyze this screenshot", new List<FileAttachment> { image });

// Or use ChatMessage for full control
var contents = new List<IMessageContent>
{
    new TextMessageContent("What's in this image?"),
    new ImageMessageContent(imageBytes, "image/png")
};
var result = await agent.RunAsync(new ChatMessage(ChatRole.User, contents));
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

// Multimodal tool (returns images/screenshots alongside text)
// Use MultimodalTool<TArgs> base class for convenience
public class ClickTool : MultimodalTool<ClickArgs>
{
    public override string Name => "click_at";
    public override string Description => "Click at coordinates";
    
    protected override async Task<ToolResult> ExecuteAsync(ClickArgs args, CancellationToken ct)
    {
        // Perform action
        await page.Mouse.ClickAsync(args.X, args.Y);
        
        // Capture screenshot after action
        var screenshot = await page.ScreenshotAsync();
        var url = page.Url;
        
        // Return JSON result + screenshot
        return new ToolResult
        {
            Text = JsonSerializer.Serialize(new { url }),
            AdditionalContent = new ImageMessageContent(screenshot, "image/png")
        };
    }
}

public record ClickArgs(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y
);
```

### Google Computer Use (Browser Automation)

Build browser control agents with Gemini 2.5 Computer Use model:

```csharp
// 1. Setup agent with Computer Use
var agent = await new AgentBuilder()
    .UseGoogleComputerUse(
        apiKey: "your-google-api-key",
        excludedFunctions: new List<string> { "drag_and_drop" }) // Optional
    .AddTool(new ClickTool(browserContext), skipDefinition: true)
    .AddTool(new TypeTextTool(browserContext), skipDefinition: true)
    .AddTool(new NavigateTool(browserContext), skipDefinition: true)
    .BuildChatAgentAsync();

// 2. Send task with screenshot
var screenshot = await page.ScreenshotAsync();
var response = await agent.SendAsync(
    "Search for smart fridges under $4000 on Google Shopping",
    new List<FileAttachment> { FileAttachment.FromBytes(screenshot, "image/png") });
```

**Implementation Requirements:**
- Inherit from `MultimodalTool<TArgs>` (not `Tool<TArgs, TResult>`)
- Always return `ToolResult` with:
  - `Text`: JSON object containing `url` field
  - `AdditionalContent`: Screenshot as `ImageMessageContent`
- Use `skipDefinition: true` for Computer Use predefined tools
- Normalize coordinates: Model outputs 0-999, convert to actual pixels
- Recommended screen size: 1440x900

**Supported Actions:** `click_at`, `type_text_at`, `navigate`, `scroll_document`, `go_back`, `go_forward`, `key_combination`, `drag_and_drop`, `hover_at`, `scroll_at`

See [GOOGLE_COMPUTER_USE.md](GOOGLE_COMPUTER_USE.md) for full documentation.

### Hidden Tools (Skip Definition)

For models pre-trained on specific tools (e.g., Gemini computer use), you can add tools without sending their definitions to the model:

```csharp
// Tool is available for execution but NOT sent to the model
// Use case: Model is pre-trained on this tool's interface
.AddTool(new ComputerUseTool(), skipDefinition: true)

// Normal behavior (default) - tool definition sent to model
.AddTool(new MyCustomTool())
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
    
    // GPT-5.1 specific options
    options.ReasoningEffort = "high";        // none, low, medium, high
    options.PromptCacheRetention = "24h";    // Extended caching
})

// Models
OpenAIModels.GPT51                  // gpt-5.1 (advanced reasoning, 400K context)
OpenAIModels.GPT51Mini              // gpt-5.1-mini (faster, cost-effective)
OpenAIModels.GPT51Nano              // gpt-5.1-nano (most economical)
OpenAIModels.GPT4o                  // gpt-4o
OpenAIModels.GPT4oMini              // gpt-4o-mini
OpenAIModels.O1                     // o1 (reasoning model)
OpenAIModels.O1Mini                 // o1-mini (reasoning model)
OpenAIModels.O1Preview              // o1-preview (reasoning model)
OpenAIModels.GPT4Turbo              // gpt-4-turbo
OpenAIModels.GPT4TurboPreview       // gpt-4-turbo-preview
OpenAIModels.GPT4                   // gpt-4
OpenAIModels.GPT35Turbo             // gpt-3.5-turbo
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
GoogleModels.GeminiFlashLiteLatest  // gemini-flash-lite-latest
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
GroqModels.Llama3_1_70B             // llama-3.1-70b-versatile (128K, reliable tool calling)
GroqModels.Llama3_1_8B              // llama-3.1-8b-instant (fast)
GroqModels.Llama4Maverick17B        // meta-llama/llama-4-maverick-17b-128e-instruct (128K context, 8K max completion)
GroqModels.Qwen3_32B                // qwen/qwen3-32b (128K context, 40K max completion)
GroqModels.GptOss120B               // openai/gpt-oss-120b (UNRELIABLE tool calling)
GroqModels.GptOss20B                // openai/gpt-oss-20b (UNRELIABLE tool calling)
GroqModels.KimiK2Instruct           // moonshotai/kimi-k2-instruct-0905 (256K context, 16K max completion)
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

**Filter tools to reduce context size:**

```csharp
// Only expose specific tools (reduces tokens & confusion)
var allowedTools = new List<string> 
{ 
    "playwright_navigate",
    "playwright_screenshot",
    "playwright_click"
};

var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithMcpClientFactory(mcpFactory)
    .WithMcp(mcpConfig, allowedTools)  // Only 3 tools exposed
    .BuildChatAgentAsync();
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
            
        // New: Built-in cost tracking
        Console.WriteLine($"Model: {evt.ModelName}");
        Console.WriteLine($"Cost: ${evt.Usage?.TotalCost:F6}");
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
        
        // New: Built-in cost and token tracking
        Console.WriteLine($"Total cost: ${evt.Result.TotalCost:F4}");
        Console.WriteLine($"Total tokens: {evt.Result.TotalInputTokens + evt.Result.TotalOutputTokens}");
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
| `OnTurnComplete` | Turn ends | `AgentTurn`, `Duration`, **Cost & Tokens** |
| `OnLlmRequest` | Before LLM API call | `Messages`, `ToolCount` |
| `OnLlmResponse` | After LLM responds | **`ModelName`**, `Text`, `ToolCalls`, **`Usage`**, `Duration`, **Cost** |
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
    _costs.Add(evt.Usage?.TotalCost ?? 0);  // New: Built-in cost tracking
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
.AddTool(tool, skipDefinition: true)  // NEW: Execute without sending definition to model
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
.WithMaxMultimodalMessages(n)     // NEW: Limit images in history (computer use)
.WithConfig(cfg)                  // Agent behavior
.WithObserver(observer)           // Observability events
.WithReActConfig(cfg)             // ReActAgent settings
.WithHistoryManager(manager)      // Custom history
.WithCostCalculator(calculator)   // Custom cost calculator
.WithModel(modelName)             // Set model name for cost tracking

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
            _totalCost += evt.Usage.TotalCost;  // New: Built-in cost tracking
            
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

**Built-in real-time cost tracking** - Monitor LLM API costs automatically:

```csharp
public class CostObserver : IAgentObserver
{
    public void OnLlmResponse(LlmResponseEvent evt)
    {
        // Per-response cost tracking
        Console.WriteLine($"Model: {evt.ModelName}");
        Console.WriteLine($"Cost: ${evt.Usage.TotalCost:F6}");
        Console.WriteLine($"  Input: ${evt.Usage.InputCost:F6} ({evt.Usage.InputTokens} tokens)");
        Console.WriteLine($"  Output: ${evt.Usage.OutputCost:F6} ({evt.Usage.OutputTokens} tokens)");
    }

    public void OnTurnComplete(TurnCompleteEvent evt)
    {
        // Turn-level summary
        Console.WriteLine($"Turn cost: ${evt.Result.TotalCost:F4}");
        Console.WriteLine($"Total tokens: {evt.Result.TotalInputTokens + evt.Result.TotalOutputTokens}");
    }
}

.WithObserver(new CostObserver())
```

**Automatic pricing for all major providers:**
- **OpenAI:** GPT-5.1 series, GPT-4o, GPT-4o-mini, o1 series, GPT-4, GPT-3.5-turbo
- **Anthropic:** Claude 4.5, 4, 3.7 series
- **Google:** Gemini 2.5 Pro, Flash, Flash Lite
- **XAI:** Grok 4 variants
- **Unknown models:** Return $0.00 (as requested)

**Cost tracking events:**
- `OnLlmResponse`: Per LLM call (granular)
- `OnTurnComplete`: Per turn (summary)
- `OnError`: Zero cost on errors

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
// Chat Messages
class ChatMessage
{
    ChatRole Role { get; init; }
    string? Text { get; init; }
    List<IMessageContent>? Contents { get; init; }
    List<ToolCall>? ToolCalls { get; init; }
    string? ToolCallId { get; init; }
    
    // Constructors
    ChatMessage(ChatRole role, string? text, string? toolCallId = null)
    ChatMessage(ChatRole role, string? text, List<ToolCall>? toolCalls)
    ChatMessage(ChatRole role, List<IMessageContent> contents, string? toolCallId = null)
}

enum ChatRole { System, User, Assistant, Tool }

// Message Content (for multimodal messages)
interface IMessageContent { string ContentType { get; } }

record TextMessageContent(string Text) : IMessageContent
{
    string ContentType => "text";
}

record ImageMessageContent(byte[] Data, string MimeType) : IMessageContent
{
    string ContentType => "image";
}

record ToolCallMessageContent(string CallId, string ToolName, string ArgumentsJson) : IMessageContent
{
    string ContentType => "tool_call";
}

record ToolResultMessageContent(string CallId, string Result, bool IsError = false) : IMessageContent
{
    string ContentType => "tool_result";
}

// File Attachments
class FileAttachment
{
    byte[] Data { get; init; }
    string MediaType { get; init; }
    string? FileName { get; init; }
    bool IsImage { get; }                            // Check if image type
    long Size { get; }                               // File size in bytes
    
    // Factory methods
    static FileAttachment FromBytes(byte[] data, string mediaType, string? fileName = null)
    static FileAttachment FromBase64(string base64Data, string mediaType, string? fileName = null)
    static Task<FileAttachment> FromFileAsync(string filePath, CancellationToken ct = default)
    
    // Utility methods
    ImageMessageContent ToMessageContent()
    string ToBase64()
}

// Tool Calls
class ToolCall
{
    string Id { get; init; }                         // Unique tool call ID
    string FunctionName { get; init; }               // Tool/function name
    string Arguments { get; init; }                  // Arguments as JSON string
}

// Tools
interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonDocument ParameterSchema { get; }
    Task<string> InvokeAsync(string argsJson, CancellationToken ct);
}

interface IUITool : ITool { }  // Marker for human-in-the-loop tools

// NEW: Multimodal tool (returns images/screenshots alongside text)
interface IMultimodalTool : ITool
{
    Task<ToolResult> InvokeWithResultAsync(string argsJson, CancellationToken ct);
}

// NEW: Result from multimodal tool
class ToolResult
{
    string Text { get; init; }                        // Text result (for tool call response)
    IMessageContent? AdditionalContent { get; init; } // Optional image/audio content
    
    // Implicit conversion from string for backwards compatibility
    static implicit operator ToolResult(string text);
}

abstract class Tool<TArgs, TResult> : ITool
{
    abstract string Name { get; }
    abstract string Description { get; }
    protected abstract Task<TResult> ExecuteAsync(TArgs args, CancellationToken ct);
}

abstract class SimpleTool<TArgs> : Tool<TArgs, ToolResponse>
{
    protected abstract Task<string> RunAsync(TArgs args, CancellationToken ct);
}

abstract class UITool<TArgs, TResult> : Tool<TArgs, TResult>, IUITool { }

// LLM Client
interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(List<LlmMessage> messages, LlmOptions? options = null, CancellationToken ct = default);
    IAsyncEnumerable<LlmStreamingUpdate> StreamAsync(List<LlmMessage> messages, LlmOptions? options = null, CancellationToken ct = default);
}

class LlmMessage
{
    MessageRole Role { get; init; }
    string? Text { get; init; }
    List<IMessageContent>? Contents { get; init; }
    string? ToolCallId { get; init; }
}

enum MessageRole { System, User, Assistant, Tool }

class LlmOptions
{
    int? MaxTokens { get; set; }
    double? Temperature { get; set; }
    double? TopP { get; set; }
    List<string>? StopSequences { get; set; }
    Dictionary<string, LlmTool>? Tools { get; set; }
    Dictionary<string, object>? AdditionalProperties { get; set; }
}

class LlmTool
{
    string Name { get; init; }
    string Description { get; init; }
    JsonElement ParameterSchema { get; init; }
}

class LlmResponse
{
    string? Text { get; init; }
    List<LlmToolCall>? ToolCalls { get; init; }
    LlmFinishReason? FinishReason { get; init; }
    LlmUsage? Usage { get; init; }
}

class LlmStreamingUpdate
{
    string? TextDelta { get; init; }
    LlmToolCall? ToolCall { get; init; }
    LlmFinishReason? FinishReason { get; init; }
    LlmUsage? Usage { get; init; }
}

class LlmUsage
{
    int InputTokens { get; init; }
    int OutputTokens { get; init; }
    int TotalTokens { get; }  // Computed: InputTokens + OutputTokens
}
```

### History

```csharp
// History Store (persistent storage)
interface IHistoryStore
{
    Task AppendMessageAsync(string conversationId, ChatMessage message, CancellationToken ct = default);
    Task AppendMessagesAsync(string conversationId, List<ChatMessage> messages, CancellationToken ct = default);
    Task SaveAsync(string conversationId, List<ChatMessage> history, CancellationToken ct = default);
    Task<List<ChatMessage>?> LoadAsync(string conversationId, CancellationToken ct = default);
    Task DeleteAsync(string conversationId, CancellationToken ct = default);
    Task<List<string>> ListConversationsAsync(CancellationToken ct = default);
    Task<int> GetMessageCountAsync(string conversationId, CancellationToken ct = default);
    Task CreateCheckpointAsync(string conversationId, ConversationCheckpoint checkpoint, CancellationToken ct = default);
    Task<ConversationCheckpoint?> GetLatestCheckpointAsync(string conversationId, CancellationToken ct = default);
    Task<(ConversationCheckpoint? checkpoint, List<ChatMessage> messages)> LoadFromCheckpointAsync(string conversationId, CancellationToken ct = default);
}

// History Manager (in-memory)
interface IHistoryManager
{
    void AddMessage(ChatMessage message);
    List<ChatMessage> GetHistory();
    void ReplaceHistory(List<ChatMessage> history);
    void Clear();
    HistoryStats GetStats();
}

// History Selector (context selection)
interface IHistorySelector
{
    List<ChatMessage> SelectMessagesForContext(List<ChatMessage> fullHistory, ToolResultConfig config);
    List<ChatMessage> SelectMessagesForContext(List<ChatMessage> fullHistory, ConversationCheckpoint? checkpoint, ToolResultConfig config);
}

// Conversation Checkpoint (summarization point)
class ConversationCheckpoint
{
    int UpToTurnNumber { get; init; }               // Turn number summarized up to (inclusive)
    string Summary { get; init; }                   // Summary text
    DateTime CreatedAt { get; init; }               // When checkpoint was created
    Dictionary<string, object>? Metadata { get; init; }  // Optional metadata
}

// History Statistics
class HistoryStats
{
    int TotalMessages { get; init; }                // Total message count
    int UserMessages { get; init; }                 // User message count
    int AssistantMessages { get; init; }            // Assistant message count
    int ToolMessages { get; init; }                 // Tool message count
    int EstimatedTokens { get; init; }              // Estimated token count
    int CompressionCount { get; init; }             // Compression count (deprecated, always 0)
}
```

### MCP

```csharp
// MCP Client (Model Context Protocol)
interface IMcpClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct = default);
    Task<List<McpToolDefinition>> DiscoverToolsAsync(CancellationToken ct = default);
    Task<McpToolResult> CallToolAsync(string toolName, Dictionary<string, object?> arguments, CancellationToken ct = default);
    McpConnectionStatus ConnectionStatus { get; }
}

// MCP Configuration
interface IMcpConfiguration
{
    string Command { get; }                         // Command to execute (e.g., "npx")
    List<string> Arguments { get; }                 // Command arguments
    Dictionary<string, string> Environment { get; } // Environment variables
    string? WorkingDirectory { get; }               // Working directory
}

class McpConfiguration : IMcpConfiguration
{
    string Command { get; set; }
    List<string> Arguments { get; set; }
    Dictionary<string, string> Environment { get; set; }
    string? WorkingDirectory { get; set; }
}

// MCP Tool Definition
class McpToolDefinition
{
    string Name { get; init; }                      // Tool name
    string Description { get; init; }               // Tool description
    JsonElement InputSchema { get; init; }          // JSON schema for parameters
}

// MCP Tool Result
class McpToolResult
{
    bool Success { get; init; }                     // Whether execution succeeded
    JsonElement? Data { get; init; }                // Result data
    string? Error { get; init; }                    // Error message if failed
}

// MCP Connection Status
enum McpConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Failed
}
```

### Observer

```csharp
// Observer Interface (all methods have default no-op implementations)
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

// Shared Event Context (present in all events)
record AgentEventContext
{
    DateTime Timestamp { get; init; }        // When event occurred (UTC)
    string? ConversationId { get; init; }    // Conversation ID (null for ReActAgent)
    int MessageCount { get; init; }          // Current message count in history
}

// Turn Start Event
record TurnStartEvent(
    AgentEventContext Context,
    string UserMessage                       // User's input message
);

// Turn Complete Event
record TurnCompleteEvent(
    AgentEventContext Context,
    AgentTurn Result,                        // Turn result (see AgentTurn below)
    TimeSpan Duration                        // Total turn duration
);

// LLM Request Event (before API call)
record LlmRequestEvent(
    AgentEventContext Context,
    IReadOnlyList<LlmMessage> Messages,      // Messages being sent to LLM
    int ToolCount                            // Number of tools available
);

// LLM Response Event (after API call)
record LlmResponseEvent(
    AgentEventContext Context,
    string ModelName,                        // NEW: Which model was used
    string? Text,                            // Generated text
    List<LlmToolCall>? ToolCalls,           // Tool calls requested (see LlmToolCall below)
    LlmUsage? Usage,                        // Token usage (see LlmUsage below)
    LlmFinishReason? FinishReason,          // Why generation stopped (Stop/Length/ToolCalls/ContentFilter)
    TimeSpan Duration                        // LLM API call duration
);

// Tool Execution Start Event
record ToolExecutionStartEvent(
    AgentEventContext Context,
    string ToolName,                         // Name of tool being executed
    string Arguments                         // JSON arguments
);

// Tool Execution Complete Event
record ToolExecutionCompleteEvent(
    AgentEventContext Context,
    string ToolName,                         // Name of tool executed
    string Result,                           // Tool result (JSON string)
    TimeSpan Duration,                       // Tool execution duration
    Exception? Error                         // Exception if tool failed (null if success)
);

// Error Event
record ErrorEvent(
    AgentEventContext Context,
    Exception Exception,                     // The exception that occurred
    string Phase                             // Phase where error occurred (e.g., "LLM", "Tool", "Turn")
);

// Supporting Types

class AgentTurn
{
    string Response { get; init; }           // Agent's response text
    int LlmCallsExecuted { get; init; }      // Number of LLM calls in this turn
    string? CompletionSignal { get; init; }  // Completion signal (ReActAgent only)
    bool Success { get; init; }              // Whether turn succeeded
    string? Error { get; init; }             // Error message if failed
    
    // NEW: Built-in cost and token tracking
    int TotalInputTokens { get; init; }      // Total input tokens used in turn
    int TotalOutputTokens { get; init; }     // Total output tokens used in turn
    decimal TotalCost { get; init; }         // Total cost for the turn (USD)
}

class LlmToolCall
{
    string Id { get; init; }                 // Unique tool call ID
    string Name { get; init; }               // Tool name
    string ArgumentsJson { get; init; }      // Arguments as JSON string
}

class LlmUsage
{
    int InputTokens { get; init; }           // Input/prompt tokens
    int OutputTokens { get; init; }          // Output/completion tokens
    int TotalTokens { get; }                 // InputTokens + OutputTokens
    
    // NEW: Built-in cost tracking
    decimal InputCost { get; init; }         // Cost for input tokens (USD)
    decimal OutputCost { get; init; }        // Cost for output tokens (USD)
    decimal TotalCost { get; }               // InputCost + OutputCost
}

enum LlmFinishReason
{
    Stop,                                    // Natural stop
    Length,                                  // Max tokens reached
    ToolCalls,                               // Stopped to execute tools
    ContentFilter                            // Content policy violation
}

class LlmMessage
{
    MessageRole Role { get; init; }          // System/User/Assistant/Tool
    string? Text { get; init; }              // Text content
    List<IMessageContent>? Contents { get; init; }  // Multimodal content
    string? ToolCallId { get; init; }        // Tool call ID (for Tool role)
}

enum MessageRole { System, User, Assistant, Tool }
```

### Configuration

```csharp
// Agent Configuration
class AgentConfig
{
    int MaxToolRoundsPerTurn { get; set; }              // Default: 10
    string? SystemPrompt { get; set; }                  // System instructions
    SummarizationConfig Summarization { get; set; }     // Auto-summarization (ChatAgent)
    ToolResultConfig ToolResults { get; set; }          // Tool result filtering
    int? MaxMultimodalMessages { get; set; }            // NEW: Limit images in history (null = unlimited)
    SanitizationOptions Sanitization { get; set; }      // Output cleaning
    bool EnableTurnValidation { get; set; }             // Default: true
    bool EnableOutputSanitization { get; set; }         // Default: true
}

// Summarization Configuration (ChatAgent)
class SummarizationConfig
{
    bool Enabled { get; set; }                          // Default: false
    int TriggerAt { get; set; }                         // Default: 100
    int KeepRecent { get; set; }                        // Default: 10
    ITool? SummarizationTool { get; set; }              // Required if Enabled
    ToolResultConfig ToolResults { get; set; }          // Nested tool filtering
}

// Tool Result Filtering Configuration
class ToolResultConfig
{
    int KeepRecent { get; set; }                        // Default: 0 (unlimited)
    // Recommended: Browser=1-3, ReAct=5-10, Chat=5
}

// Sanitization Options
class SanitizationOptions
{
    bool RemoveThinkingTags { get; set; }               // Default: true
    bool UnwrapJsonFromMarkdown { get; set; }           // Default: true
    bool TrimWhitespace { get; set; }                   // Default: true
    bool RemoveNullCharacters { get; set; }             // Default: true
}

// ReAct Agent Configuration
class ReActConfig
{
    int MaxTurns { get; set; }                          // Default: 20
    bool DetectStuckAgent { get; set; }                 // Default: true
    bool BreakOnStuck { get; set; }                     // Default: false
}

// ReAct Agent Result
class ReActResult
{
    bool Success { get; init; }                         // Task completed successfully
    string FinalAnswer { get; init; }                   // Final answer/result
    int TurnsExecuted { get; init; }                    // Turns executed
    int TotalLlmCalls { get; init; }                    // Total LLM calls
    TimeSpan Duration { get; init; }                    // Execution time
    string? Error { get; init; }                        // Error if failed
}
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

**Built with ❤️ by NovaCore AI Team**
