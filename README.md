# NovaCore.AgentKit

> **Production-ready AI agents for .NET with a clean, modern API**

Build intelligent agents that chat with users or autonomously complete tasks. Focus on capabilities, not infrastructure.

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

---

## 📑 Table of Contents

### Getting Started
- [Quick Start](#quick-start)
- [Two Agent Types](#two-agent-types)

### ChatAgent
- [Basic Usage](#️-chatagent-conversational-ai)
- [Message-Based API](#message-based-api)
- [Persistent Conversations](#persistent-conversations)
- [Automatic Summarization](#automatic-summarization)
- [UI Tools (Human-in-the-Loop)](#ui-tools-human-in-the-loop)
- [Automatic Summarization](#automatic-summarization-chatagent)
- [Tool Result Filtering](#tool-result-filtering-all-agents)
- [Tools with POCO Arguments](#tools-with-poco-arguments)
- [Logging](#logging)
- [Output Sanitization](#output-sanitization)
- [Turn Validation](#turn-validation)
- [Full Configuration](#full-configuration-reference)
- [Multi-Tenancy](#multi-tenancy)
- [Statistics & Monitoring](#statistics--monitoring)
- [Error Handling](#error-handling)
- [Disposal](#disposal)

### ReActAgent
- [Basic Usage](#-reactagent-autonomous-tasks)
- [Configuration](#configuration)
- [Key Differences](#key-differences-from-chatagent)

### Providers & Tools
- [LLM Providers](#-llm-providers)
- [Available Model Constants](#available-model-constants)
- [MCP Integration](#-mcp-instant-tool-discovery)
- [Multimodal Support](#️-multimodal-support)

### Advanced
- [Custom History Manager](#custom-history-manager)
- [Rate Limiting](#rate-limiting)
- [Cost Tracking](#cost-tracking)
- [OpenTelemetry](#opentelemetry)

### Reference
- [Packages](#-packages)
- [Key Features](#-key-features)
- [Complete API Reference](#-complete-api-reference)
- [AgentBuilder Methods](#agentbuilder-methods)
- [ChatAgent Methods](#chatagent-methods)
- [ReActAgent Methods](#reactagent-methods)
- [ChatMessage Constructors](#chatmessage-constructors)
- [Configuration Objects](#configuration-objects)
- [Logging Setup Examples](#logging-setup-examples)

### Resources
- [Documentation](#-documentation)
- [FAQ](#-faq)
- [License](#-license)

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
Console.WriteLine(response.Text);  // "2+2 equals 4"
```

---

## Two Agent Types

| **ChatAgent** | **ReActAgent** |
|---------------|----------------|
| Stateful, persistent conversations | Ephemeral, task-focused |
| Chat apps, customer service | Research, automation |
| Resume across sessions | Lives only for task duration |
| Immediate responses | Iterative reasoning |
| Human-in-the-loop | Autonomous execution |

---

## 🗨️ ChatAgent: Conversational AI

For building chat applications, customer service bots, and interactive assistants.

### Basic Usage

```csharp
var agent = await new AgentBuilder()
    .UseOpenAI(apiKey, "gpt-4o")
    .WithSystemPrompt("You are a helpful assistant.")
    .ForConversation("user-123")
    .BuildChatAgentAsync();

// Send messages, get responses
var response = await agent.SendAsync("Hello!");
Console.WriteLine(response.Text);

// Check for tool calls (e.g., UI tools)
if (response.ToolCalls?.Any() == true)
{
    // Handle UI interaction
}
```

### Message-Based API

ChatAgent uses `ChatMessage` for all interactions - clean and symmetric:

```csharp
// Text message
var response = await agent.SendAsync("What's the weather?");

// Multimodal (images/files)
var image = await FileAttachment.FromFileAsync("screenshot.png");
var response = await agent.SendAsync("What's in this image?", [image]);

// Tool result (UI tool response)
var toolResult = new ChatMessage(ChatRole.Tool, resultJson, toolCallId);
var response = await agent.SendAsync(toolResult);
```

### Persistent Conversations

Conversations automatically save and resume:

```csharp
// Session 1
var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithHistoryStore(historyStore)
    .ForConversation("chat-123")
    .BuildChatAgentAsync();  // Auto-loads existing history

await agent.SendAsync("My name is Alice");

// Session 2 (later, different process)
var agent = await new AgentBuilder()
    .WithHistoryStore(historyStore)
    .ForConversation("chat-123")
    .BuildChatAgentAsync();  // Resumes from database

await agent.SendAsync("What's my name?");  // "Your name is Alice"
```

**Setup:**

```csharp
// Configure DbContext
public class MyDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureAgentKitModels();
    }
}

// Create history store
var historyStore = new EfCoreHistoryStore<MyDbContext>(
    dbContext, logger, 
    tenantId: "tenant-1", 
    userId: "user-123");
```

### Automatic Summarization

For long conversations, automatically create checkpoints to reduce context size:

```csharp
// Define summarization tool (calls your LLM to generate summaries)
public class ConversationSummarizationTool : ITool
{
    private readonly ILlmClient _summarizerLlm;
    
    public ConversationSummarizationTool(ILlmClient summarizerLlm)
    {
        _summarizerLlm = summarizerLlm;
    }
    
    public string Name => "summarize_conversation";
    public string Description => "Summarizes conversation history";
    
    public JsonDocument ParameterSchema => JsonDocument.Parse(@"{
        ""type"": ""object"",
        ""properties"": {
            ""conversation_id"": { ""type"": ""string"" },
            ""from_turn"": { ""type"": ""integer"" },
            ""to_turn"": { ""type"": ""integer"" },
            ""original_message_count"": { ""type"": ""integer"" },
            ""filtered_message_count"": { ""type"": ""integer"" },
            ""messages"": {
                ""type"": ""array"",
                ""items"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""role"": { ""type"": ""string"" },
                        ""text"": { ""type"": ""string"" },
                        ""has_tool_calls"": { ""type"": ""boolean"" },
                        ""is_tool_result"": { ""type"": ""boolean"" }
                    }
                }
            }
        }
    }");
    
    public async Task<string> InvokeAsync(string argsJson, CancellationToken ct = default)
    {
        // Parse arguments
        var args = JsonSerializer.Deserialize<JsonElement>(argsJson);
        var messages = args.GetProperty("messages");
        
        // Build prompt from conversation messages
        var prompt = "Summarize this conversation segment concisely:\n\n";
        
        foreach (var msg in messages.EnumerateArray())
        {
            var text = msg.GetProperty("text").GetString();
            var role = msg.GetProperty("role").GetString();
            
            if (!string.IsNullOrEmpty(text))
            {
                prompt += $"{role}: {text}\n\n";
            }
        }
        
        prompt += "\nProvide a 2-3 sentence summary:";
        
        // Call your LLM for summarization
        var llmMessages = new List<LlmMessage>
        {
            new LlmMessage { Role = MessageRole.User, Text = prompt }
        };
        
        var response = await _summarizerLlm.CompleteAsync(llmMessages, null, ct);
        var summary = response.Message.Text ?? "";
        
        // Return result as JSON
        return JsonSerializer.Serialize(new { summary });
    }
}

// Configure auto-checkpointing
var summarizerLlm = /* Your ILlmClient for summarization (e.g., from any provider) */;

var agent = await new AgentBuilder()
    .UseOpenAI(apiKey, model)
    .WithHistoryStore(historyStore)
    .WithCheckpointing(config =>
    {
        config.EnableAutoCheckpointing = true;
        config.SummarizeEveryNMessages = 50;   // Checkpoint every 50 messages
        config.KeepRecentMessages = 10;         // Keep last 10 uncompressed
        config.SummarizationTool = new ConversationSummarizationTool(summarizerLlm);
    })
    .BuildChatAgentAsync();

// After 50 messages: system automatically creates checkpoint
// LLM receives: [summary of first 40] + [last 10 messages in full]
```

### UI Tools (Human-in-the-Loop)

Create tools that pause execution for user interaction:

```csharp
// Define UI tool
public class ShowPaymentPageTool : UITool<PaymentArgs, PaymentResult>
{
    public override string Name => "show_payment_page";
    public override string Description => "Display payment interface to user";
}

public record PaymentArgs(decimal Amount, string Currency);
public record PaymentResult(bool Success, string TransactionId);

// Register UI tools
var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .AddTool(new WeatherTool())          // Internal execution
    .AddUITool(new ShowPaymentPageTool()) // Host handles (pauses)
    .BuildChatAgentAsync();

// Conversation flow
var response = await agent.SendAsync("I want to pay for this");

if (response.ToolCalls?.Any() == true)
{
    var toolCall = response.ToolCalls[0];
    
    // Parse arguments
    var args = JsonSerializer.Deserialize<PaymentArgs>(toolCall.Arguments);
    
    // Show UI to user
    var result = await ShowPaymentUIAsync(args);
    
    // Send result back
    var toolResult = new ChatMessage(
        ChatRole.Tool,
        JsonSerializer.Serialize(result),
        toolCall.Id);
    
    var finalResponse = await agent.SendAsync(toolResult);
    Console.WriteLine(finalResponse.Text);  // "Payment complete!"
}
```

**Use Cases:** Login dialogs, payment forms, file uploads, confirmations, settings panels

### Automatic Summarization (ChatAgent)

For long conversations, automatically summarize older messages into checkpoints to maintain context while reducing memory usage.

**How it works:**
1. Conversation grows to `TriggerAt` messages (e.g., 100)
2. Calculate: `MessagesToSummarize = TriggerAt - KeepRecent` (e.g., 100 - 10 = 90)
3. Summarize first 90 messages → Create checkpoint
4. Remove those 90 from memory, keep last 10
5. Database retains ALL messages + checkpoint

**Configuration:**

```csharp
.WithSummarization(cfg =>
{
    cfg.Enabled = true;
    cfg.TriggerAt = 100;         // Summarize when we hit 100 messages
    cfg.KeepRecent = 10;         // Keep last 10 (summarize first 90)
    cfg.SummarizationTool = summaryTool;
    cfg.ToolResults.KeepRecent = 5;  // Also filter verbose tool outputs
})
```

### Tool Result Filtering (All Agents)

For verbose tool outputs (e.g., browser automation), replace old results with placeholders while preserving structure.

**For ReAct/Browser agents:**

```csharp
.WithToolResultFiltering(cfg =>
{
    cfg.KeepRecent = 1;  // Keep only last tool result (perfect for browser agents!)
})
```

**Example output:**
```
Browser navigate → Tool result: [Omitted]
Browser click → Tool result: [Omitted]
Browser snapshot → Tool result: [5000 chars of HTML]  ← Only this one has full content!
```

**Recommended values:**
- Browser agents (Playwright): `KeepRecent = 1-3`
- ReAct agents: `KeepRecent = 5-10`
- ChatAgent with tools: Use `Summarization.ToolResults.KeepRecent = 5`

**Benefits:**
- ✅ Token reduction: 80-90% for browser agents
- ✅ Structure preserved: All Assistant messages kept
- ✅ No orphaned tool calls: IDs always preserved
- ✅ Recent context: Full detail for latest results

**Example logs:**
```
[INF] Tool result filtering: keep 5 recent, replace others with placeholders
[DBG] Tool result filtering: 20 tool messages, 5 kept full, 15 replaced with placeholders
```

> **💡 Tip:** Use placeholder filtering instead of removing messages - it preserves conversation structure!

### Tools with POCO Arguments

No manual JSON schemas - just define C# types:

```csharp
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

// JSON schema auto-generated from WeatherArgs
// Case-insensitive argument matching
// Type-safe at compile time
```

**Simpler option (SimpleTool):**

```csharp
public class NotifyTool : SimpleTool<NotifyArgs>
{
    public override string Name => "notify";
    public override string Description => "Send notification";
    
    protected override Task<string> RunAsync(NotifyArgs args, CancellationToken ct)
    {
        return Task.FromResult($"Sent: {args.Message}");
    }
}

public record NotifyArgs(string Message);

// Returns: { "success": true, "message": "Sent: Hello!" }
```

**Full control (ITool interface):**

```csharp
public class CustomTool : ITool
{
    public string Name => "custom";
    public string Description => "Custom logic";
    public JsonDocument ParameterSchema => JsonDocument.Parse(@"{
        ""type"": ""object"",
        ""properties"": {
            ""input"": { ""type"": ""string"" }
        }
    }");
    
    public async Task<string> InvokeAsync(string argsJson, CancellationToken ct)
    {
        // Full manual control over everything
        var args = JsonSerializer.Deserialize<CustomArgs>(argsJson);
        var result = await ProcessAsync(args);
        return JsonSerializer.Serialize(result);
    }
}
```

**Summary of tool types:**

| Type | Use Case | Schema | Error Handling |
|------|----------|--------|----------------|
| `Tool<TArgs, TResult>` | Standard tools | Auto-generated | Manual |
| `SimpleTool<TArgs>` | Basic tools | Auto-generated | Auto (returns success/error) |
| `UITool<TArgs, TResult>` | Human-in-the-loop | Auto-generated | N/A (host handles) |
| `ITool` | Full control | Manual | Manual |

### Logging

Full control over what gets logged at each agent turn:

```csharp
var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

var agent = await new AgentBuilder()
    .UseOpenAI(apiKey, model)
    .WithLogger(loggerFactory.CreateLogger("AgentKit"))
    .WithLogging(log =>
    {
        // What to log
        log.LogUserInput = LogVerbosity.Full;              // Full user messages
        log.LogAgentOutput = LogVerbosity.Truncated;       // Truncated responses
        log.LogToolCallRequests = LogVerbosity.Truncated;  // Tool calls with args
        log.LogToolCallResponses = LogVerbosity.None;      // Don't log results
        
        // Truncation settings
        log.TruncationLength = 500;                        // Characters to keep
        log.UseStructuredLogging = true;                   // JSON-style logs
    })
    .BuildChatAgentAsync();

// Output:
// [Turn] User Input | content="What is 2+2?" hasFiles=false
// [Turn] Agent Output | content="The answer is 4." hasToolCalls=false
// [Turn] Tool Call Request | toolName=calculator arguments={"expr":"2+2"}
```

**Verbosity Levels:**
- `LogVerbosity.None` - Don't log (default)
- `LogVerbosity.Truncated` - Log with truncation
- `LogVerbosity.Full` - Log complete content

Works with any .NET logger (Console, Serilog, Application Insights, etc.)

### Output Sanitization

Clean up model outputs automatically:

```csharp
.WithConfig(config =>
{
    config.EnableOutputSanitization = true;
    config.Sanitization.RemoveThinkingTags = true;      // Remove <thinking> tags
    config.Sanitization.UnwrapJsonFromMarkdown = true;  // Extract from ```json
    config.Sanitization.TrimWhitespace = true;          // Clean whitespace
})
```

### Turn Validation

Automatically fix invalid conversation sequences:

```csharp
.WithConfig(config =>
    {
    config.EnableTurnValidation = true;  // Auto-fix invalid sequences
    })

// Example fix:
// Invalid: User → User → Assistant
// Fixed:   User → Assistant(empty) → User → Assistant
```

### Full Configuration Reference

```csharp
.WithConfig(config =>
{
    // Core settings
    config.SystemPrompt = "You are a helpful assistant";
    config.MaxToolRoundsPerTurn = 10;              // Safety limit per turn
    config.EnableTurnValidation = true;            // Fix invalid sequences
    config.EnableOutputSanitization = true;        // Clean model outputs
    
    // History (in-memory management)
    config.History.CompressThreshold = 100;        // Auto-compress after N messages
    config.History.KeepRecentMessages = 20;        // Messages to keep on compress
    config.History.LogTruncation = true;           // Log when compression happens
    
    // Sanitization options
    config.Sanitization.RemoveThinkingTags = true;
    config.Sanitization.UnwrapJsonFromMarkdown = true;
    config.Sanitization.TrimWhitespace = true;
})
```

### Multi-Tenancy

Isolate conversations by tenant and user:

```csharp
var historyStore = new EfCoreHistoryStore<MyDbContext>(
    dbContext,
    logger,
    tenantId: "tenant-abc",  // Tenant isolation
    userId: "user-xyz");     // User isolation

// List conversations for this tenant/user only
var conversations = await historyStore.ListConversationsAsync();

// Delete conversation
await historyStore.DeleteAsync("chat-123");
```

### Statistics & Monitoring

```csharp
// Conversation statistics
var stats = agent.GetStats();
Console.WriteLine($"Total messages: {stats.TotalMessages}");
Console.WriteLine($"User messages: {stats.UserMessages}");
Console.WriteLine($"Assistant messages: {stats.AssistantMessages}");
Console.WriteLine($"Tool messages: {stats.ToolMessages}");
Console.WriteLine($"Estimated tokens: ~{stats.EstimatedTokens}");
Console.WriteLine($"Compressions: {stats.CompressionCount}");

// Checkpoint status
var checkpoint = await agent.GetLatestCheckpointAsync();
if (checkpoint != null)
{
    Console.WriteLine($"Last checkpoint at turn: {checkpoint.UpToTurnNumber}");
    Console.WriteLine($"Summary: {checkpoint.Summary}");
}

// Clear history
agent.ClearHistory();
```

### Error Handling

```csharp
try
{
    var response = await agent.SendAsync("Hello");
    
    // Check response (errors are rare with message-based API)
    if (response.Role != ChatRole.Assistant)
    {
        Console.WriteLine("Unexpected response role");
    }
}
catch (HttpRequestException ex)
{
    // Network/API errors
    Console.WriteLine($"API error: {ex.Message}");
}
catch (JsonException ex)
{
    // Parsing errors
    Console.WriteLine($"Parse error: {ex.Message}");
}
```

### Disposal

Always dispose agents to clean up resources:

```csharp
// Preferred: using statement
await using var agent = await builder.BuildChatAgentAsync();
// Agent and MCP clients automatically disposed

// Manual disposal
var agent = await builder.BuildChatAgentAsync();
try
{
    // Use agent
}
finally
{
    await agent.DisposeAsync();  // Cleans up MCP clients
}
```

---

## 🤖 ReActAgent: Autonomous Tasks

For research, data gathering, automation, and complex workflows.

### Basic Usage

```csharp
var agent = await new AgentBuilder()
    .UseOpenAI(apiKey, "gpt-4o")
    .AddTool(new SearchTool())
    .AddTool(new CalculatorTool())
    .WithReActConfig(cfg => cfg.MaxIterations = 20)
    .BuildReActAgentAsync();

var result = await agent.RunAsync(
    "Find the current Bitcoin price and calculate 10% of it");

Console.WriteLine(result.FinalAnswer);
Console.WriteLine($"Iterations: {result.Iterations.Count}");
Console.WriteLine($"Tool calls: {result.TotalToolCalls}");
```

### Key Differences from ChatAgent

- **No persistence**: ReActAgent doesn't store history (ephemeral by design)
- **Task-focused**: Runs until completion or max iterations
- **No WithHistoryStore**: Not applicable
- **No UI tools**: Autonomous execution only
- **Complete signal**: Uses built-in `complete_task` tool

### Configuration

```csharp
.WithReActConfig(config =>
{
    config.MaxIterations = 20;              // Stop after N iterations
    config.DetectStuckAgent = true;         // Detect when agent makes no progress
    config.BreakOnStuck = false;            // Continue even if stuck
})
```

---

## 🔌 LLM Providers

Six providers, same API. Switch with one line:

```csharp
// Anthropic Claude
.UseAnthropic(options =>
{
    options.ApiKey = apiKey;
    options.Model = AnthropicModels.ClaudeSonnet45;  // "claude-sonnet-4-5-20250929"
    options.UseExtendedThinking = true;     // Deep reasoning
    options.EnablePromptCaching = true;     // Cost optimization
})

// OpenAI
.UseOpenAI(apiKey, OpenAIModels.GPT4o)

// Google Gemini
.UseGoogle(apiKey, GoogleModels.Gemini25Flash)

// xAI Grok
.UseXAI(apiKey, XAIModels.Grok4FastNonReasoning)

// Groq (ultra-fast)
.UseGroq(apiKey, GroqModels.Llama3_3_70B)  // or GroqModels.Qwen3_32B

// OpenRouter (any model)
.UseOpenRouter(apiKey, "anthropic/claude-3.5-sonnet")
```

### Available Model Constants

**Anthropic Claude:**
- `AnthropicModels.ClaudeSonnet45` - Latest (Sep 2025)
- `AnthropicModels.ClaudeSonnet4` - Powerful reasoning
- `AnthropicModels.ClaudeSonnet37` - Enhanced (Feb 2025)
- `AnthropicModels.ClaudeSonnet35` - Previous gen
- `AnthropicModels.ClaudeHaiku35` - Fast & cost-effective

**OpenAI:**
- `OpenAIModels.GPT4o` - Latest multimodal
- `OpenAIModels.GPT4oMini` - Faster & cheaper
- `OpenAIModels.O1` - Advanced reasoning
- `OpenAIModels.O1Mini` - Smaller reasoning
- `OpenAIModels.GPT4Turbo` - Latest GPT-4 Turbo

**Google Gemini:**
- `GoogleModels.Gemini25Pro` - Most capable
- `GoogleModels.Gemini25Flash` - Fast & efficient
- `GoogleModels.Gemini25FlashLite` - Lightweight
- `GoogleModels.GeminiFlashLatest` - Auto-updated

**xAI Grok:**
- `XAIModels.Grok4FastReasoning` - With reasoning
- `XAIModels.Grok4FastNonReasoning` - Without reasoning
- `XAIModels.GrokCodeFast1` - Code specialized

**Groq:**
- `GroqModels.Llama3_3_70B` - Recommended, 128K context
- `GroqModels.Llama3_1_70B` - Reliable tool calling
- `GroqModels.Qwen3_32B` - 128K context
- `GroqModels.Llama3_1_8B` - Fast, smaller model

**OpenRouter:** Use any model string (e.g., `"anthropic/claude-3.5-sonnet"`)

---

## 🌐 MCP: Instant Tool Discovery

[Model Context Protocol](https://modelcontextprotocol.io) - add powerful tools with zero code:

```csharp
var mcpConfig = new McpConfiguration
{
    Command = "npx",
    Arguments = new List<string> { "-y", "@playwright/mcp" }
};

var mcpFactory = new McpClientFactory(loggerFactory);

var agent = await new AgentBuilder()
    .UseXAI(apiKey, model)
    .WithMcpClientFactory(mcpFactory)
    .WithMcp(mcpConfig)  // 21 browser automation tools added
    .BuildChatAgentAsync();

await agent.SendAsync("Go to example.com and take a screenshot");
```

**Popular servers:**
- `@playwright/mcp` - Browser automation
- `@modelcontextprotocol/server-filesystem` - File operations
- `@modelcontextprotocol/server-github` - GitHub API
- `@modelcontextprotocol/server-slack` - Slack integration

---

## 🖼️ Multimodal Support

Send images and files to vision models:

```csharp
// From file
var image = await FileAttachment.FromFileAsync("screenshot.png");
await agent.SendAsync("What's in this image?", new List<FileAttachment> { image });

// From bytes
var imageData = await httpClient.GetByteArrayAsync(url);
var image = FileAttachment.FromBytes(imageData, "image/jpeg");
await agent.SendAsync("Describe this", new List<FileAttachment> { image });

// Multiple files
var files = new List<FileAttachment>
{
    await FileAttachment.FromFileAsync("photo1.jpg"),
    await FileAttachment.FromFileAsync("photo2.jpg")
};
await agent.SendAsync("Compare these images", files);
```

**Supported:** Claude Sonnet 4.5, GPT-4o, Gemini 2.5, Grok 4

---

## 🔧 Advanced Topics

### Custom History Manager

Implement your own history storage:

```csharp
public class RedisHistoryManager : IHistoryManager
{
    public void AddMessage(ChatMessage message) { /* Store in Redis */ }
    public List<ChatMessage> GetHistory() { /* Load from Redis */ }
    public void ReplaceHistory(List<ChatMessage> history) { /* Replace */ }
    public void CompressHistory() { /* Apply compression */ }
    public void Clear() { /* Clear Redis */ }
    public HistoryStats GetStats() { /* Return stats */ }
}

// Use it
.WithHistoryManager(new RedisHistoryManager(redis))
```

### Rate Limiting

Coming soon - prevent runaway costs:

```csharp
// Future API
services.AddAgentKit(options =>
{
    options.RateLimit = new RateLimitConfig
    {
        MaxConcurrent = 5,
        MaxRequestsPerMinute = 60,
        Timeout = TimeSpan.FromSeconds(30)
    };
});
```

### Cost Tracking

Coming soon - track token usage and costs:

```csharp
// Future API
var costTracker = serviceProvider.GetRequiredService<ICostTracker>();
var summary = costTracker.GetSummary();

Console.WriteLine($"Total cost: ${summary.TotalCost:F4}");
foreach (var item in summary.Items)
{
    Console.WriteLine($"{item.Model}: {item.Tokens} tokens (${item.Cost:F4})");
}
```

### OpenTelemetry

Coming soon - production metrics:

```csharp
// Future API
services.AddAgentKit(options =>
{
    options.Telemetry.EnableOpenTelemetry = true;
});

// Metrics:
// - agent_turns_total
// - agent_turn_duration_seconds
// - tool_executions_total
// - agent_errors_total
```

---

## 📦 Packages

### Core
- `NovaCore.AgentKit.Core` - Core abstractions (required)
- `NovaCore.AgentKit.MCP` - Model Context Protocol
- `NovaCore.AgentKit.EntityFramework` - Persistence

### Providers
- `NovaCore.AgentKit.Providers.Anthropic` - Claude
- `NovaCore.AgentKit.Providers.OpenAI` - GPT-4o, o1
- `NovaCore.AgentKit.Providers.Google` - Gemini
- `NovaCore.AgentKit.Providers.XAI` - Grok
- `NovaCore.AgentKit.Providers.Groq` - Qwen, Llama
- `NovaCore.AgentKit.Providers.OpenRouter` - Any model

---

## 🎯 Key Features

### ChatAgent
✅ Message-based API (ChatMessage in/out)
✅ Persistent conversations (auto-save/resume)
✅ Automatic summarization/checkpointing
✅ UI tools (human-in-the-loop)
✅ History retention (cost control)
✅ Multi-tenancy support

### ReActAgent
✅ Autonomous task execution
✅ Iterative reasoning (ReAct pattern)
✅ Built-in completion signaling
✅ Ephemeral (no storage overhead)

### Both Agents
✅ 6 LLM providers (OpenAI, Anthropic, Google, xAI, Groq, OpenRouter)
✅ POCO-based tools (auto-schema generation)
✅ MCP integration (instant tool discovery)
✅ Multimodal support (images/files)
✅ Automatic conversation repair (maintains LLM API compatibility)
✅ Configuration validation (proactive warnings)
✅ Production-ready (logging, metrics, sanitization)
✅ Fluent API (readable configuration)

---

## 🎓 Complete API Reference

### AgentBuilder Methods

```csharp
// LLM Provider (required - choose one)
.UseAnthropic(apiKey, model)
.UseOpenAI(apiKey, model)
.UseGoogle(apiKey, model)
.UseXAI(apiKey, model)
.UseGroq(apiKey, model)
.UseOpenRouter(apiKey, model)
.UseLlmClient(customClient)  // Or custom ILlmClient

// Tools
.AddTool(tool)                // Internal execution
.AddTools(tools)              // Multiple internal tools
.AddUITool(uiTool)            // Human-in-the-loop (pauses)
.AddUITools(uiTools)          // Multiple UI tools
.WithMcp(mcpConfig)           // MCP server (auto-discovery)

// Configuration
.WithSystemPrompt(prompt)
.ForConversation(id)          // Conversation ID (ChatAgent only)
.WithHistoryStore(store)         // Persistence (ChatAgent only)
.WithSummarization(config)       // Auto-summarization (ChatAgent only)
.WithToolResultFiltering(config) // Reduce verbose tool outputs
.WithConfig(config)              // Agent behavior
.WithLogging(config)             // Logging settings
.WithReActConfig(config)      // ReActAgent settings
.WithLogger(logger)           // Custom logger
.WithLoggerFactory(factory)   // Logger factory (for MCP)
.WithMcpClientFactory(factory)// MCP client factory
.WithHistoryManager(manager)  // Custom history implementation

// Build
.BuildChatAgentAsync()        // Interactive conversations
.BuildReActAgentAsync()       // Autonomous tasks
```

### ChatAgent Methods

```csharp
// Send messages (main API)
Task<ChatMessage> SendAsync(ChatMessage message)
Task<ChatMessage> SendAsync(string text)  // Convenience
Task<ChatMessage> SendAsync(string text, List<FileAttachment> files)  // Convenience

// Checkpoints
Task CreateCheckpointAsync(string summary, int? upToTurnNumber = null)
Task<ConversationCheckpoint?> GetLatestCheckpointAsync()

// Statistics & management
HistoryStats GetStats()
void ClearHistory()
string ConversationId { get; }

// Disposal
ValueTask DisposeAsync()
```

### ReActAgent Methods

```csharp
// Execute task
Task<ReActResult> RunAsync(string task)

// Disposal
ValueTask DisposeAsync()
```

### ChatMessage Constructors

```csharp
// Text message
new ChatMessage(ChatRole.User, "Hello")
new ChatMessage(ChatRole.Assistant, "Hi there")

// Multimodal message
new ChatMessage(ChatRole.User, List<IMessageContent> contents)

// Tool result
new ChatMessage(ChatRole.Tool, resultJson, toolCallId)

// With tool calls (from LLM)
new ChatMessage(ChatRole.Assistant, text, List<ToolCall> toolCalls)
```

### Configuration Objects

```csharp
// AgentConfig
config.SystemPrompt
config.MaxToolRoundsPerTurn
config.EnableTurnValidation
config.EnableOutputSanitization
config.History          // HistoryConfig
config.HistoryRetention // HistoryRetentionConfig
config.Sanitization     // SanitizationOptions
config.Logging          // AgentLoggingConfig
config.Checkpointing    // CheckpointConfig

// HistoryRetentionConfig
config.MaxMessagesToSend
config.KeepRecentMessagesIntact
config.AlwaysIncludeSystemMessage
config.UseCheckpointSummary
config.ToolResults      // ToolResultRetentionConfig
config.Validate()       // Returns List<string> of configuration warnings
config.GetSummary()     // Returns readable configuration summary

// CheckpointConfig
config.EnableAutoCheckpointing
config.SummarizeEveryNMessages
config.KeepRecentMessages
config.SummarizationTool

// ReActConfig
config.MaxIterations
config.DetectStuckAgent
config.BreakOnStuck

// AgentLoggingConfig
config.LogUserInput          // LogVerbosity: None, Truncated, Full
config.LogAgentOutput        // LogVerbosity: None, Truncated, Full
config.LogToolCallRequests   // LogVerbosity: None, Truncated, Full
config.LogToolCallResponses  // LogVerbosity: None, Truncated, Full
config.TruncationLength      // Default: 200 characters
config.UseStructuredLogging  // Default: true

// SanitizationOptions
config.RemoveThinkingTags      // Default: true
config.UnwrapJsonFromMarkdown  // Default: true
config.TrimWhitespace          // Default: true
config.RemoveNullCharacters    // Default: true

// ToolResultRetentionConfig
config.MaxToolResults  // 0 = unlimited
config.Strategy        // KeepRecent, KeepSuccessful, KeepOne, DropAll
```

### Logging Setup Examples

**Complete logging configuration:**

```csharp
using Microsoft.Extensions.Logging;

// Create logger factory with any provider (Console, Serilog, Application Insights, etc.)
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .AddDebug()
        .SetMinimumLevel(LogLevel.Debug);
});

var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithLogger(loggerFactory.CreateLogger("AgentKit"))
    .WithLoggerFactory(loggerFactory)  // For MCP clients
    .WithLogging(log =>
    {
        // What to log
        log.LogUserInput = LogVerbosity.Full;              // Full user messages
        log.LogAgentOutput = LogVerbosity.Truncated;       // Truncated responses
        log.LogToolCallRequests = LogVerbosity.Truncated;  // Tool calls with args
        log.LogToolCallResponses = LogVerbosity.None;      // Don't log results
        
        // Truncation settings
        log.TruncationLength = 500;                        // Characters to keep
        log.UseStructuredLogging = true;                   // JSON-style logs
    })
    .BuildChatAgentAsync();
```

**Logging verbosity levels:**

```csharp
LogVerbosity.None      // Don't log this item (default)
LogVerbosity.Truncated // Log with truncation (respects TruncationLength)
LogVerbosity.Full      // Log complete content
```

**Example log output (with structured logging):**

```
[Debug] AgentKit: Persisted incoming message (role: User) for conversation chat-123
[Debug] AgentKit: History selection: 5 → 5 messages (System: 1, Conversation: 4, Checkpoint: False)
[Debug] AgentKit: Sending 2 tools to LLM
[Turn] User Input | content="What's the weather in Paris?" hasFiles=false
[Debug] AgentKit: Executing tool: get_weather
[Turn] Tool Call Request | toolName=get_weather arguments={"location":"Paris"}
[Debug] AgentKit: Tool get_weather executed successfully
[Turn] Agent Output | content="The current weather in Paris is 18°C..." hasToolCalls=false
[Debug] AgentKit: Persisted 4 new message(s) for conversation chat-123
```

**Integration with Serilog:**

```csharp
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/agent-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var loggerFactory = new SerilogLoggerFactory(Log.Logger);

var agent = await new AgentBuilder()
    .UseOpenAI(apiKey, model)
    .WithLoggerFactory(loggerFactory)
    .WithLogger(loggerFactory.CreateLogger("AgentKit"))
    .WithLogging(log =>
    {
        log.LogUserInput = LogVerbosity.Full;
        log.LogAgentOutput = LogVerbosity.Truncated;
        log.LogToolCallRequests = LogVerbosity.Truncated;
        log.TruncationLength = 1000;
    })
    .BuildChatAgentAsync();
```

**Minimal logging (production):**

```csharp
var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithLogger(logger)
    .WithLogging(log =>
    {
        // Only log errors and warnings (via standard ILogger)
        // Turn-specific logging all disabled
        log.LogUserInput = LogVerbosity.None;
        log.LogAgentOutput = LogVerbosity.None;
        log.LogToolCallRequests = LogVerbosity.None;
        log.LogToolCallResponses = LogVerbosity.None;
    })
    .BuildChatAgentAsync();
```

**Debug/Development logging:**

```csharp
var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, model)
    .WithLogger(logger)
    .WithLogging(log =>
    {
        // Log everything for debugging
        log.LogUserInput = LogVerbosity.Full;
        log.LogAgentOutput = LogVerbosity.Full;
        log.LogToolCallRequests = LogVerbosity.Full;
        log.LogToolCallResponses = LogVerbosity.Full;
        log.UseStructuredLogging = true;
    })
    .BuildChatAgentAsync();
```

---

## 📚 Documentation

- **Architecture Guide**: `ARCHITECTURE_IMPROVEMENTS.md` - Design decisions and improvements
- **Automatic Summarization**: `AUTOMATIC_SUMMARIZATION_GUIDE.md` - Detailed checkpoint guide
- **History Management**: `HISTORY_MANAGEMENT_FIX_SUMMARY.md` - History retention improvements
- **Browser Agent Guide**: `BROWSER_AGENT_RECOMMENDATIONS.md` - Configuration guide for browser automation
- **Tool Arguments**: `TOOL_ARGUMENT_HANDLING.md` - Tool argument handling
- **Examples**: `src/NovaCore.AgentKit.Tests/` - Comprehensive tests

---

## ❓ FAQ

**Q: ChatAgent vs ReActAgent - which should I use?**
A: ChatAgent for conversations with users (chat apps, support). ReActAgent for autonomous tasks (research, automation).

**Q: Do I need a database for ChatAgent?**
A: No - it works without persistence. Add `WithHistoryStore()` for resume capability.

**Q: How do UI tools work?**
A: LLM calls the tool → execution pauses → you show UI to user → send result back → execution continues.

**Q: What's the difference between AddTool and AddUITool?**
A: `AddTool()` = agent executes internally. `AddUITool()` = pauses for host to handle.

**Q: Can I use multiple LLM providers?**
A: Not in same agent, but you can create multiple agents with different providers.

**Q: How do I reduce costs?**
A: Use `WithSummarization()` for long ChatAgent conversations and `WithToolResultFiltering()` for verbose tool outputs. For browser agents, set `KeepRecent = 1`. This can reduce token usage by 80-90%.

**Q: Do checkpoints delete old messages?**
A: No - full history stays in database. Checkpoints only affect what's sent to the LLM.

**Q: Can I resume a conversation after app restart?**
A: Yes - `BuildChatAgentAsync()` automatically loads history if you configure a history store.

**Q: What's the "ONE tool call at a time" behavior?**
A: System prompts LLM to make single tool calls for predictable execution (especially important for UI tools).

**Q: I'm seeing "KeepRecentMessagesIntact is more than 50%" warnings - is this bad?**
A: It's a configuration warning, not an error. Your agent will work fine, but you're not leaving much room for older context. Recommended: Keep it at 20-30% of `MaxMessagesToSend`.

**Q: What does "Conversation structure repaired" mean in the logs?**
A: History filtering sometimes creates invalid message sequences. The system automatically detects and fixes this by dropping orphaned messages. This is normal and ensures LLM API compatibility.

---

## 📄 License

MIT License - see LICENSE file

---

**Built with ❤️ BY NovaCore AI Team** - 90% LLM generated. (proof that llm generated code can become production ready)

