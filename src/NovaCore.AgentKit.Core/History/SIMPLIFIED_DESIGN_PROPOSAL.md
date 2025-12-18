# Simplified History Management - Design Proposal

## 🎯 Problem

**Current system has 3 overlapping mechanisms:**

1. **HistoryConfig.CompressThreshold** - In-memory compression (removes messages)
2. **HistoryRetentionConfig.MaxMessagesToSend** - LLM context limiting (filters messages)
3. **CheckpointConfig** - Summarization (creates checkpoints)

**Result**: Confusion, message loss, complexity!

Example from test:
- 30 user messages + 30 agent responses = 60 messages expected
- **Database has 55** (5 lost somewhere!)
- **Memory has 21** (compressed)

---

## ✨ Simplified Design

### Core Principle
**One mechanism per use case** - no overlap!

### For ChatAgent (Long conversations)

**Single config: Summarization-based compression**

```csharp
.WithConfig(cfg =>
{
    cfg.Summarization = new SummarizationConfig
    {
        TriggerAt = 100,        // Summarize when we hit 100 messages
        KeepRecent = 10,        // Keep last 10 messages after summary
        SummarizationTool = tool,
        
        // Tool result filtering (uses placeholders)
        ToolResults = new ToolResultConfig
        {
            KeepRecent = 5      // Keep last 5 tool results with full content
        }
    };
})
```

**How it works:**
1. Conversation grows to 100 messages → **Trigger!**
2. Calculate: `MessagesToSummarize = TriggerAt - KeepRecent = 100 - 10 = 90`
3. Summarize first **90 messages** → Create checkpoint
4. Remove those **90 messages** from memory
5. Keep last **10 messages** in memory
6. Database still has **ALL 100 messages**!
7. Next trigger at 110 messages (100 + 10)

**Example timeline:**
- At 100 msgs: Summarize msgs 1-90, keep 91-100 → Memory has 10 msgs
- At 110 msgs: Summarize msgs 91-100, keep 101-110 → Memory has 10 msgs + 2 checkpoints
- Database always has: ALL messages + ALL checkpoints

**Tool results**: Always use placeholder approach, never remove messages

### For ReActAgent (Short, tool-heavy conversations)

**Simple config: Just tool result filtering**

```csharp
.WithConfig(cfg =>
{
    cfg.ToolResults = new ToolResultConfig
    {
        KeepRecent = 5  // Keep last 5 tool results with full content, rest are "[Omitted]"
    };
})
```

**No summarization needed** - ReAct agents typically have shorter conversations

---

## 🔄 Migration from Current System

### What to Remove

1. ❌ `HistoryConfig` - Delete entirely (CompressThreshold, KeepRecentMessages, etc.)
2. ❌ `HistoryRetentionConfig.MaxMessagesToSend` - Redundant with summarization
3. ❌ `HistoryRetentionConfig.KeepRecentMessagesIntact` - Redundant with KeepRecent
4. ❌ `InMemoryHistoryManager.CompressHistory()` - Replace with checkpoint-based compression

### What to Keep & Rename

1. ✅ `HistoryRetentionConfig.ToolResults` → `ToolResultConfig` (standalone)
2. ✅ `CheckpointConfig` → `SummarizationConfig` (clearer name)
3. ✅ Tool result placeholder approach (just implemented!)

---

## 📊 New Architecture

```
┌─────────────────────────────────────┐
│  In-Memory History                  │
│  • Grows up to TriggerAt (100)      │
│  • When hit, summarize → checkpoint │
│  • Keep last KeepRecent (10)        │
│  • Never "dumb" compression          │
└────────────┬────────────────────────┘
             │ (persists every message)
             ▼
┌─────────────────────────────────────┐
│  Database (EF Core / IHistoryStore) │
│  • ALL messages always stored        │
│  • Checkpoints stored                │
│  • Never removed                     │
└─────────────────────────────────────┘
             │ (on send to LLM)
             ▼
┌─────────────────────────────────────┐
│  LLM Context                        │
│  • Checkpoint summary (if exists)   │
│  • + Last KeepRecent messages       │
│  • Tool results filtered (placeholders) │
└─────────────────────────────────────┘
```

---

## 🎯 Simplified Config Classes

### 1. SummarizationConfig (for ChatAgent)

```csharp
public class SummarizationConfig
{
    /// <summary>
    /// Enable automatic summarization.
    /// Default: false
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Trigger summarization when history reaches this many messages.
    /// Example: 100 means summarize when we hit 100 messages
    /// Default: 100
    /// </summary>
    public int TriggerAt { get; set; } = 100;
    
    /// <summary>
    /// Keep this many recent messages after summarization.
    /// Calculation: MessagesToSummarize = TriggerAt - KeepRecent
    /// 
    /// Example: TriggerAt=100, KeepRecent=10
    /// - At 100 messages: Summarize first 90, keep last 10
    /// - Result: 10 messages in memory, 90 in checkpoint summary
    /// - Database: Still has all 100 messages
    /// 
    /// Default: 10
    /// </summary>
    public int KeepRecent { get; set; } = 10;
    
    /// <summary>
    /// Tool to use for generating summaries.
    /// If null, summarization is disabled even if Enabled is true.
    /// </summary>
    public ITool? SummarizationTool { get; set; }
    
    /// <summary>
    /// Tool result filtering configuration.
    /// Uses placeholder "[Omitted]" approach to maintain conversation structure.
    /// </summary>
    public ToolResultConfig ToolResults { get; set; } = new();
}
```

### 2. ToolResultConfig (for both ChatAgent and ReActAgent)

```csharp
public class ToolResultConfig
{
    /// <summary>
    /// Number of recent tool results to keep with full content.
    /// Older tool results are replaced with "[Omitted]" placeholder.
    /// Set to 0 for unlimited (all tool results have full content).
    /// Default: 0 (unlimited)
    /// 
    /// Recommended:
    /// - Browser agents: 3-5
    /// - ReAct agents: 5-10
    /// - Chat agents: 5
    /// </summary>
    public int KeepRecent { get; set; } = 0;
}
```

### 3. AgentConfig (simplified)

```csharp
public class AgentConfig
{
    public int MaxToolRoundsPerTurn { get; set; } = 10;
    public string? SystemPrompt { get; set; }
    
    // REMOVED: HistoryConfig (no more manual compression)
    // REMOVED: HistoryRetentionConfig (replaced by Summarization)
    
    // NEW: Simplified configs
    public SummarizationConfig Summarization { get; set; } = new();
    public ToolResultConfig ToolResults { get; set; } = new();
    
    // Other configs remain
    public SanitizationOptions Sanitization { get; set; } = new();
    public bool EnableTurnValidation { get; set; } = true;
    public bool EnableOutputSanitization { get; set; } = true;
    public AgentLoggingConfig Logging { get; set; } = new();
}
```

---

## 💡 Benefits

| Before | After |
|--------|-------|
| ❌ 3 overlapping systems | ✅ 1 clear system |
| ❌ Message loss (55/60) | ✅ All messages in DB |
| ❌ CompressThreshold vs MaxMessages confusion | ✅ TriggerAt (clear intent) |
| ❌ "Dumb" compression (truncate) | ✅ Smart summarization (context preserved) |
| ❌ Multiple tool result configs | ✅ Single tool result config |

---

## 🚀 Usage Examples

### ChatAgent with Summarization
```csharp
var agent = await new AgentBuilder()
    .UseXAI(apiKey, "grok-4")
    .WithSummarization(cfg =>
    {
        cfg.Enabled = true;
        cfg.TriggerAt = 100;
        cfg.KeepRecent = 10;
        cfg.SummarizationTool = summaryTool;
        cfg.ToolResults.KeepRecent = 5;
    })
    .BuildChatAgentAsync();
```

### ReActAgent (No summarization, just tool filtering)
```csharp
var agent = await new AgentBuilder()
    .UseAnthropic(apiKey, "claude-sonnet-4")
    .AddTool(weatherTool)
    .WithToolResultFiltering(cfg =>
    {
        cfg.KeepRecent = 5;  // Keep last 5 tool results with full content
    })
    .BuildReActAgentAsync();
```

### Browser Agent (Aggressive tool filtering)
```csharp
var agent = await new AgentBuilder()
    .UseXAI(apiKey, "grok-4-fast")
    .WithMcp(playwrightConfig)
    .WithToolResultFiltering(cfg =>
    {
        cfg.KeepRecent = 1;  // Keep only last tool result
    })
    .BuildReActAgentAsync();
```

---

## ✅ What This Achieves

1. **Clear separation of concerns**
   - Summarization = For long ChatAgent conversations
   - Tool filtering = For managing verbose tool outputs
   
2. **No message loss**
   - Database = Always has everything
   - Memory = Optimized with summaries
   - LLM = Gets summary + recent context
   
3. **Simple mental model**
   - ChatAgent: "Summarize old stuff, keep recent"
   - ReActAgent: "Filter tool outputs only"
   
4. **No conflicts**
   - One mechanism, one purpose
   - No overlapping thresholds
   - Predictable behavior

---

## 🔧 Implementation Tasks

1. Delete `HistoryConfig` class
2. Delete `HistoryRetentionConfig` class (or rename to `SummarizationConfig`)
3. Delete `InMemoryHistoryManager.CompressHistory()` (dumb compression)
4. Update `CheckpointConfig` → `SummarizationConfig`
5. Create standalone `ToolResultConfig`
6. Update `SmartHistorySelector` to work with checkpoint summaries only
7. Update `AgentBuilder` fluent API
8. Update all tests

---

## 🤔 Decision Point

Should we:
- **Option A**: Delete everything and rebuild (clean but breaks existing code)
- **Option B**: Deprecate old configs, add new ones (migration path)

Recommendation: **Option B** with clear migration guide.

