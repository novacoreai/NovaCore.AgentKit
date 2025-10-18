# History Management - Simplified Architecture

> **Simple, predictable, powerful** - One mechanism per use case, no overlapping systems.

---

## 🎯 Overview

NovaCore.AgentKit provides **two independent mechanisms** for managing conversation history:

1. **Summarization** (ChatAgent) - For long conversations, compress old messages into checkpoints
2. **Tool Result Filtering** (All Agents) - For verbose tool outputs, replace old results with placeholders

**Key Principle**: These mechanisms don't overlap or fight each other!

---

## 📚 For ChatAgents: Summarization

Use summarization to maintain context in long conversations while reducing memory usage.

### How It Works

```
At TriggerAt messages (e.g., 100):
1. Calculate: MessagesToSummarize = TriggerAt - KeepRecent (100 - 10 = 90)
2. Summarize first 90 messages → Create checkpoint
3. Remove those 90 messages from memory
4. Keep last 10 messages in memory
5. Database retains ALL 100 messages + checkpoint

Next trigger at 110 messages (repeats the process)
```

### Configuration

```csharp
var agent = await new AgentBuilder()
    .UseXAI(apiKey, "grok-4")
    .WithSummarization(cfg =>
    {
        cfg.Enabled = true;
        cfg.TriggerAt = 100;        // Summarize when we hit 100 messages
        cfg.KeepRecent = 10;        // Keep last 10 (summarize first 90)
        cfg.SummarizationTool = summaryTool;
        
        // Optional: Tool result filtering
        cfg.ToolResults.KeepRecent = 5;
    })
    .WithEfCoreHistory(dbContext)  // Database stores ALL messages
    .ForConversation("chat-123")
    .BuildChatAgentAsync();
```

### What Happens

| Stage | In Memory | In Database |
|-------|-----------|-------------|
| At 100 msgs | 10 messages + 1 checkpoint | 100 messages + 1 checkpoint |
| At 110 msgs | 10 messages + 2 checkpoints | 110 messages + 2 checkpoints |
| At 120 msgs | 10 messages + 3 checkpoints | 120 messages + 3 checkpoints |

**Result**: Constant memory usage, complete database history!

---

## 🔧 For ReActAgents: Tool Result Filtering

Use tool result filtering to manage verbose tool outputs (e.g., Playwright browser automation).

### How It Works

```
With KeepRecent = 5:
1. Keep last 5 tool results with full content
2. Replace older tool results with "[Omitted]" placeholder
3. ALL Assistant messages preserved (agent's reasoning intact)
4. Tool call IDs always preserved (structure remains valid)
```

### Configuration

```csharp
var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, "claude-sonnet-4")
    .WithMcp(playwrightConfig)  // Browser automation = verbose outputs
    .WithToolResultFiltering(cfg =>
    {
        cfg.KeepRecent = 1;  // Keep only last tool result (perfect for browser agents)
    })
    .BuildReActAgentAsync();
```

### Example

**Before filtering (10 tool calls)**:
```
User: Search for visa information
Assistant: Let me search [toolCall: search, id: tc_1]
Tool: [5000 chars of search results] [id: tc_1]
Assistant: Let me search more [toolCall: search, id: tc_2]
Tool: [4500 chars of search results] [id: tc_2]
... (8 more tool calls)
```

**After filtering (KeepRecent = 2)**:
```
User: Search for visa information
Assistant: Let me search [toolCall: search, id: tc_1]  ← Preserved!
Tool: [Omitted] [id: tc_1]  ← Placeholder
Assistant: Let me search more [toolCall: search, id: tc_2]  ← Preserved!
Tool: [Omitted] [id: tc_2]  ← Placeholder
...
Assistant: Final search [toolCall: search, id: tc_9]  ← Preserved!
Tool: [4200 chars] [id: tc_9]  ← Full content (last 2)
Assistant: Done [toolCall: search, id: tc_10]  ← Preserved!
Tool: [3800 chars] [id: tc_10]  ← Full content (last 2)
```

**Benefits**:
- ✅ Token count reduced by ~80-90%
- ✅ All Assistant messages preserved (reasoning intact)
- ✅ Conversation structure valid (no orphaned tool calls)
- ✅ Recent context fully available

---

## 🎛️ Configuration Options

### SummarizationConfig

| Property | Default | Description |
|----------|---------|-------------|
| `Enabled` | `false` | Enable automatic summarization |
| `TriggerAt` | `100` | Summarize when history reaches this many messages |
| `KeepRecent` | `10` | Keep this many recent messages after summarization |
| `SummarizationTool` | `null` | Tool for generating summaries (required if Enabled) |
| `ToolResults` | - | Nested tool result filtering config |

**Formula**: `MessagesToSummarize = TriggerAt - KeepRecent`

### ToolResultConfig

| Property | Default | Description |
|----------|---------|-------------|
| `KeepRecent` | `0` | Keep this many recent tool results with full content (0 = unlimited) |

**Recommended Values**:
- Browser agents (Playwright): `1-3`
- ReAct agents: `5-10`
- Chat agents with tools: `5`

---

## 📖 Complete Examples

### Example 1: ChatAgent with Summarization

```csharp
// Create summarization tool
var summaryTool = new MyS ummarizationTool(llmClient);

// Build agent
var agent = await new AgentBuilder()
    .UseOpenAI(apiKey, "gpt-4o")
    .WithSummarization(cfg =>
    {
        cfg.Enabled = true;
        cfg.TriggerAt = 100;
        cfg.KeepRecent = 10;
        cfg.SummarizationTool = summaryTool;
        cfg.ToolResults.KeepRecent = 5;  // Also filter tool results
    })
    .WithEfCoreHistory(dbContext)
    .ForConversation("support-chat-456")
    .BuildChatAgentAsync();

// Use it
await agent.SendAsync("Hello!");
// ... 100+ messages later, automatic summarization triggers
```

### Example 2: Browser Agent (Playwright)

```csharp
var agent = await new AgentBuilder()
    .UseXAI(apiKey, "grok-4-fast")
    .WithMcp(playwrightConfig)
    .WithToolResultFiltering(cfg =>
    {
        cfg.KeepRecent = 1;  // Keep only last browser snapshot
    })
    .BuildReActAgentAsync();

var result = await agent.RunAsync("Navigate to example.com and find the heading");
```

### Example 3: ReAct Agent with Multiple Tools

```csharp
var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, "claude-sonnet-4")
    .AddTool(new SearchTool())
    .AddTool(new CalculatorTool())
    .AddTool(new WeatherTool())
    .WithToolResultFiltering(cfg =>
    {
        cfg.KeepRecent = 5;  // Keep last 5 tool results
    })
    .BuildReActAgentAsync();

var result = await agent.RunAsync("Research visa requirements and calculate costs");
```

### Example 4: Simple ChatAgent (No Special Config)

```csharp
// No summarization, no tool filtering - just works!
var agent = await new AgentBuilder()
    .UseGoogle(apiKey, "gemini-2.5-flash")
    .BuildChatAgentAsync();

await agent.SendAsync("Hello!");
```

---

## 🔄 What Changed from Previous Versions

### ❌ Removed (Overcomplicated)

- `HistoryConfig` - "Dumb" auto-compression (truncation)
- `HistoryRetentionConfig.MaxMessagesToSend` - Redundant with summarization
- `HistoryRetentionConfig.KeepRecentMessagesIntact` - Redundant
- `InMemoryHistoryManager.CompressHistory()` - Replaced by checkpoint-based compression

### ✅ Added (Simplified)

- `SummarizationConfig` - Clear, checkpoint-based compression
- `ToolResultConfig` - Simple tool output filtering
- Placeholder approach for tool results (`"[Omitted]"`)
- No message removal during filtering (preserves structure)

### 🔄 Migration

**Old API** (deprecated but still works):
```csharp
.WithHistoryRetention(cfg => { ... })  // Obsolete warning
```

**New API**:
```csharp
.WithSummarization(cfg => { ... })          // For ChatAgents
.WithToolResultFiltering(cfg => { ... })    // For tool output management
```

---

## 💡 Key Benefits

| Benefit | Description |
|---------|-------------|
| **🧹 Simplicity** | One mechanism per use case, no overlap |
| **📊 Predictability** | Clear formula: TriggerAt - KeepRecent = Summarized |
| **💾 No Data Loss** | Database always has complete history |
| **🧠 Context Preserved** | Summaries maintain conversation context |
| **🏗️ Structure Maintained** | Placeholders keep conversation valid |
| **🎯 Use Case Clarity** | ChatAgent = Summarization, ReAct = Tool Filtering |

---

## 🔍 Architecture

```
┌─────────────────────────────────────┐
│  In-Memory History                  │
│  • Grows to TriggerAt (e.g., 100)   │
│  • Summarize first 90 → Checkpoint  │
│  • Keep last 10 in memory            │
│  • Never "dumb" truncation           │
└────────────┬────────────────────────┘
             │ (persists every message)
             ▼
┌─────────────────────────────────────┐
│  Database (IHistoryStore)           │
│  • ALL messages always stored        │
│  • ALL checkpoints stored            │
│  • Complete audit trail              │
│  • Never removed                     │
└────────────┬────────────────────────┘
             │ (on LLM call)
             ▼
┌─────────────────────────────────────┐
│  LLM Context                        │
│  • Checkpoint summary (if exists)   │
│  • + Last KeepRecent messages       │
│  • Tool results filtered (placeholders) │
└─────────────────────────────────────┘
```

---

## 📝 Best Practices

1. **ChatAgent**: Always use summarization for conversations > 50 messages
   ```csharp
   .WithSummarization(cfg =>
   {
       cfg.Enabled = true;
       cfg.TriggerAt = 100;
       cfg.KeepRecent = 10;
       cfg.SummarizationTool = summaryTool;
   })
   ```

2. **Browser Agent**: Aggressive tool filtering (keep only 1-3 results)
   ```csharp
   .WithToolResultFiltering(cfg => cfg.KeepRecent = 1)
   ```

3. **ReAct Agent**: Moderate tool filtering (keep 5-10 results)
   ```csharp
   .WithToolResultFiltering(cfg => cfg.KeepRecent = 5)
   ```

4. **Short Conversations**: No special config needed!
   ```csharp
   .BuildChatAgentAsync()  // Just works
   ```

---

## ✅ Testing

All tests passing:
- ✅ 6 tool result filtering tests
- ✅ 3 checkpoint/summarization tests  
- ✅ Clean, simple, predictable

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~ToolResultFiltering"
dotnet test --filter "FullyQualifiedName~CheckpointSummarization"
```

---

## 🚀 Summary

**Old system**: 3 overlapping mechanisms, message loss, unpredictable  
**New system**: 2 clear mechanisms, no data loss, simple

**Use summarization** to manage long ChatAgent conversations.  
**Use tool filtering** to manage verbose tool outputs.  
**That's it!** 🎉

