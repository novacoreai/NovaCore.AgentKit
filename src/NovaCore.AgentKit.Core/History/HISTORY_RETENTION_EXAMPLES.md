# History Retention Examples

The history retention system controls **what gets sent to the LLM** while keeping full history in storage/memory.

## Basic Usage

### 1. Simple Message Limit
```csharp
var agent = await new AgentBuilder()
    .UseOpenAI(apiKey, "gpt-4o")
    .WithHistoryRetention(cfg => 
    {
        cfg.MaxMessagesToSend = 30;  // Only send last 30 messages to model
    })
    .BuildChatAgentAsync();
```

### 2. Browser Agent (Minimal Tool Results)
```csharp
var agent = await new AgentBuilder()
    .UseXAI(apiKey, XAIModels.Grok4FastNonReasoning)
    .WithMcp(playwrightConfig)
    .WithHistoryRetention(cfg => 
    {
        cfg.MaxMessagesToSend = 20;
        cfg.ToolResults.MaxToolResults = 1;           // Keep only last tool result
        cfg.ToolResults.Strategy = ToolResultStrategy.KeepOne;
    })
    .BuildChatAgentAsync();
```

### 3. Keep Recent Context Protected
```csharp
var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, AnthropicModels.ClaudeSonnet45)
    .WithHistoryRetention(cfg => 
    {
        cfg.MaxMessagesToSend = 40;
        cfg.KeepRecentMessagesIntact = 10;  // Always keep last 10 messages
    })
    .BuildChatAgentAsync();
```

### 4. Drop All Tool Results (Extreme Reduction)
```csharp
var agent = await new AgentBuilder()
    .UseGoogle(apiKey, GoogleModels.Gemini25Flash)
    .WithHistoryRetention(cfg => 
    {
        cfg.ToolResults.Strategy = ToolResultStrategy.DropAll;  // No tool results in context
    })
    .BuildChatAgentAsync();
```

### 5. Keep Only Successful Tool Results
```csharp
var agent = await new AgentBuilder()
    .UseGroq(apiKey, GroqModels.Qwen3_32B)
    .WithHistoryRetention(cfg => 
    {
        cfg.MaxMessagesToSend = 50;
        cfg.ToolResults.MaxToolResults = 5;
        cfg.ToolResults.Strategy = ToolResultStrategy.KeepSuccessful;
    })
    .BuildChatAgentAsync();
```

### 6. Using WithConfig (Alternative Approach)
```csharp
var agent = await new AgentBuilder()
    .UseOpenAI(apiKey, "gpt-4o")
    .WithConfig(cfg => 
    {
        // Access retention config via cfg.HistoryRetention
        cfg.HistoryRetention.MaxMessagesToSend = 30;
        cfg.HistoryRetention.KeepRecentMessagesIntact = 5;
        
        // Can also configure other settings
        cfg.MaxToolRoundsPerTurn = 15;
        cfg.EnableOutputSanitization = true;
    })
    .BuildChatAgentAsync();
```

## Configuration Options

### HistoryRetentionConfig
| Property | Default | Description |
|----------|---------|-------------|
| `MaxMessagesToSend` | 50 | Maximum total messages to send to model (0 = unlimited) |
| `KeepRecentMessagesIntact` | 5 | Always keep last N messages (not subject to trimming) |
| `AlwaysIncludeSystemMessage` | true | Include system message even if over limit |

### ToolResultRetentionConfig
| Property | Default | Description |
|----------|---------|-------------|
| `MaxToolResults` | 0 | Maximum tool result messages (0 = unlimited) |
| `Strategy` | KeepRecent | Which tool results to keep |

### ToolResultStrategy Options
- `KeepRecent` - Keep most recent tool results (within limit)
- `KeepSuccessful` - Keep only successful results (no errors)
- `KeepOne` - Keep only the last tool result (great for browser agents)
- `DropAll` - Remove all tool results from context

## How It Works

```
┌─────────────────────────────────┐
│  Full History (DB/Memory)       │
│  - All messages stored          │
│  - Never truncated              │
│  - Audit trail preserved        │
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│  History Selector               │
│  - Apply retention rules        │
│  - Filter tool results          │
│  - Keep recent messages         │
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│  Context Window (to LLM)        │
│  - Filtered messages            │
│  - Within token budget          │
│  - Relevant context only        │
└─────────────────────────────────┘
```

## Benefits

✅ **Reduced Costs** - Fewer tokens sent to LLM  
✅ **Better Performance** - Faster response times  
✅ **Context Control** - Keep relevant information only  
✅ **Full Audit Trail** - Complete history still stored  
✅ **Browser Agent Optimization** - Limit verbose tool outputs  
✅ **Easy Configuration** - Simple, intuitive API  

## Notes

- Full history is **always** preserved in storage (DB) and memory (IHistoryManager)
- Retention only affects what gets sent to the LLM on each turn
- System messages are always included (configurable)
- Recent messages are protected from trimming
- Tool results can be filtered separately for fine-grained control

