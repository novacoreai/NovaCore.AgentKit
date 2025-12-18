# NovaCore.AgentKit.Providers.Anthropic

**Status:** ✅ Complete - Direct REST API Implementation

## Overview

This provider implements direct integration with the Anthropic Messages API using REST calls, bypassing SDK limitations. This approach provides:

- **Full Control**: Direct access to all Anthropic API features
- **No SDK Dependencies**: Uses only `HttpClient` and JSON serialization
- **Complete Feature Support**: Extended thinking, prompt caching, tool calling, streaming
- **Microsoft.Extensions.AI Compliance**: Implements `IChatClient` interface perfectly

## Architecture

### Components

1. **AnthropicChatClient** - Main `IChatClient` implementation
2. **AnthropicRestClient** - HTTP client for API communication
3. **MessageConverter** - Bidirectional conversion between Microsoft.Extensions.AI and Anthropic formats
4. **Models** - Complete DTO definitions for requests, responses, and streaming events

### Key Features

- ✅ Synchronous and streaming responses
- ✅ Tool calling (function calling)
- ✅ Extended thinking mode support
- ✅ Prompt caching configuration
- ✅ Proper error handling with typed exceptions
- ✅ Full token usage tracking including cache metrics

## Usage

### Basic Usage

```csharp
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Anthropic;

var agent = new AgentBuilder()
    .UseAnthropic(options =>
    {
        options.ApiKey = "your-api-key";
        options.Model = AnthropicModels.ClaudeSonnet45;
        options.MaxTokens = 4096;
    })
    .BuildChatAgent();

var turn = await agent.SendAsync("Hello, Claude!");
Console.WriteLine(turn.AgentResponse);
```

### Quick Start

```csharp
var agent = new AgentBuilder()
    .UseAnthropic("your-api-key", AnthropicModels.ClaudeSonnet45)
    .BuildChatAgent();
```

### With Extended Thinking

```csharp
var agent = new AgentBuilder()
    .UseAnthropic(options =>
    {
        options.ApiKey = apiKey;
        options.Model = AnthropicModels.ClaudeSonnet45;
        options.UseExtendedThinking = true;
        options.ThinkingBudgetTokens = 10000;
        options.MaxTokens = 8192;
    })
    .BuildChatAgent();
```

### With Prompt Caching

```csharp
var agent = new AgentBuilder()
    .UseAnthropic(options =>
    {
        options.ApiKey = apiKey;
        options.Model = AnthropicModels.ClaudeSonnet45;
        options.EnablePromptCaching = true;
    })
    .WithSystemPrompt("You are a helpful assistant...")
    .BuildChatAgent();
```

### With Tools

```csharp
var agent = new AgentBuilder()
    .UseAnthropic(apiKey, AnthropicModels.ClaudeSonnet45)
    .AddTool(new WeatherTool())
    .AddTool(new CalculatorTool())
    .BuildChatAgent();

var turn = await agent.SendAsync("What's the weather like?");
// Claude will automatically call the WeatherTool
```

### Streaming

```csharp
var agent = new AgentBuilder()
    .UseAnthropic(apiKey)
    .BuildChatAgent();

await foreach (var update in agent.SendStreamingAsync("Tell me a story"))
{
    Console.Write(update.Text);
}
```

## API Features Supported

### Request Features
- ✅ Model selection
- ✅ System prompts
- ✅ Message history (user/assistant/tool)
- ✅ Temperature, Top P, Top K
- ✅ Max tokens
- ✅ Stop sequences
- ✅ Tool definitions
- ✅ Streaming mode
- ✅ Metadata (user_id, etc.)

### Response Features
- ✅ Text content
- ✅ Tool use content
- ✅ Thinking content (extended thinking)
- ✅ Multiple content blocks
- ✅ Stop reasons (end_turn, max_tokens, tool_use, etc.)
- ✅ Token usage (input, output, cache metrics)

### Streaming Features
- ✅ Server-sent events (SSE)
- ✅ Incremental text deltas
- ✅ Tool call streaming
- ✅ Message start/stop events
- ✅ Ping events (keepalive)
- ✅ Error events

## Available Models

```csharp
AnthropicModels.ClaudeSonnet45      // claude-sonnet-4-5-20250929 (latest, most capable)
AnthropicModels.ClaudeSonnet4       // claude-sonnet-4-20250514 (powerful reasoning)
AnthropicModels.ClaudeSonnet37      // claude-3-7-sonnet-20250219 (enhanced capabilities)
AnthropicModels.ClaudeSonnet35      // claude-3-5-sonnet-20241022 (previous generation)
AnthropicModels.ClaudeHaiku35       // claude-3-5-haiku-20241022 (fast, cost-effective)
```

## Configuration Options

### AnthropicOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ApiKey` | `string` | *required* | Your Anthropic API key |
| `Model` | `string` | `claude-sonnet-4-5-20250929` | Model to use |
| `MaxTokens` | `int` | `4096` | Maximum tokens to generate |
| `Temperature` | `double` | `1.0` | Randomness (0.0 - 1.0) |
| `TopP` | `double?` | `null` | Nucleus sampling |
| `TopK` | `int?` | `null` | Top-K sampling |
| `UseExtendedThinking` | `bool` | `false` | Enable extended thinking |
| `ThinkingBudgetTokens` | `int?` | `null` | Thinking token budget |
| `EnablePromptCaching` | `bool` | `false` | Enable prompt caching |
| `BaseUrl` | `string?` | `null` | Custom API endpoint |
| `Timeout` | `TimeSpan` | `2 minutes` | HTTP timeout |

## Error Handling

The provider throws `AnthropicApiException` for API errors:

```csharp
try
{
    var turn = await agent.SendAsync("Hello");
}
catch (AnthropicApiException ex)
{
    Console.WriteLine($"Error Type: {ex.ErrorType}");
    Console.WriteLine($"Status Code: {ex.StatusCode}");
    Console.WriteLine($"Message: {ex.Message}");
}
```

## Implementation Details

### Why REST API Instead of SDK?

The official Anthropic SDK (`Anthropic` NuGet package) has limitations:

1. **Complex Type Mapping**: SDK types don't cleanly map to `Microsoft.Extensions.AI` types
2. **Missing Extensions**: No `.AsIChatClient()` extension method
3. **Translation Overhead**: Requires custom adapters with complex conversion logic
4. **Feature Lag**: SDK might lag behind API features

Our REST implementation:

1. **Direct Control**: We control all type conversions
2. **No Intermediary**: Direct REST → Microsoft.Extensions.AI conversion
3. **Full Features**: Access to all API capabilities
4. **Maintainable**: Easier to update when API changes

### Performance

- Zero SDK overhead
- Efficient streaming with `HttpCompletionOption.ResponseHeadersRead`
- Minimal allocations with proper buffer usage
- JSON serialization optimized with `System.Text.Json`

## Project Structure

```
NovaCore.AgentKit.Providers.Anthropic/
├── AnthropicChatClient.cs           # Main IChatClient implementation
├── AnthropicRestClient.cs           # HTTP client wrapper
├── AnthropicOptions.cs              # Configuration options
├── AnthropicModels.cs               # Model constants
├── AnthropicAgentBuilderExtensions.cs  # Builder extensions
├── Converters/
│   └── MessageConverter.cs          # Message format conversion
└── Models/
    ├── AnthropicRequest.cs          # Request DTO
    ├── AnthropicResponse.cs         # Response DTO
    ├── AnthropicMessage.cs          # Message DTO
    ├── AnthropicContentBlock.cs     # Content block DTO
    ├── AnthropicTool.cs             # Tool definition DTO
    ├── AnthropicStreamingEvent.cs   # Streaming event DTOs
    └── AnthropicError.cs            # Error DTOs
```

## Testing

See `NovaCore.AgentKit.Tests/Providers/AnthropicProviderTests.cs` for examples.

## References

- [Anthropic Messages API Documentation](https://docs.anthropic.com/en/api/messages)
- [Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/en-us/dotnet/ai/ai-extensions)

## License

MIT License - see LICENSE file for details
