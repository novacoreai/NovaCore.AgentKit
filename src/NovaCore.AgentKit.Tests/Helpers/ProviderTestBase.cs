using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Helpers;

/// <summary>
/// Base class for provider tests with common setup
/// </summary>
public abstract class ProviderTestBase
{
    protected readonly ITestOutputHelper Output;
    protected readonly TestObserver Observer;
    protected readonly ILoggerFactory LoggerFactory;  // For MCP clients only
    
    protected ProviderTestBase(ITestOutputHelper output)
    {
        Output = output;
        Observer = new TestObserver(output);
        
        // Create logger factory for MCP clients (they still need logging)
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XUnitLoggerProvider(output));
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }
}

/// <summary>
/// Observer that writes concise logs to xUnit test output
/// </summary>
public class TestObserver : IAgentObserver
{
    private readonly ITestOutputHelper _output;
    private int _turnCount = 0;
    
    public TestObserver(ITestOutputHelper output)
    {
        _output = output;
    }
    
    public void OnTurnStart(TurnStartEvent evt)
    {
        _turnCount++;
        WriteLine($"🔵 Turn {_turnCount} | {Truncate(evt.UserMessage, 80)}");
    }
    
    public void OnTurnComplete(TurnCompleteEvent evt)
    {
        var status = evt.Result.Success ? "✓" : "✗";
        WriteLine($"{status} Turn {_turnCount} | {evt.Duration.TotalSeconds:F2}s | {evt.Result.LlmCallsExecuted} LLM calls");
    }
    
    public void OnLlmRequest(LlmRequestEvent evt)
    {
        WriteLine($"  → LLM | {evt.Messages.Count} msgs, {evt.ToolCount} tools");
    }
    
    public void OnLlmResponse(LlmResponseEvent evt)
    {
        var tokens = evt.Usage != null ? $"{evt.Usage.TotalTokens}tok" : "?tok";
        var toolCalls = evt.ToolCalls?.Count ?? 0;
        var tools = toolCalls > 0 ? $", {toolCalls} tool calls" : "";
        WriteLine($"  ← LLM | {tokens}, {evt.Duration.TotalMilliseconds:F0}ms{tools}");
    }
    
    public void OnToolExecutionStart(ToolExecutionStartEvent evt)
    {
        WriteLine($"    🔧 {evt.ToolName}");
    }
    
    public void OnToolExecutionComplete(ToolExecutionCompleteEvent evt)
    {
        var status = evt.Error == null ? "✓" : "✗";
        var result = evt.Error == null ? Truncate(evt.Result, 50) : evt.Error.Message;
        WriteLine($"    {status} {evt.ToolName} | {evt.Duration.TotalMilliseconds:F0}ms | {result}");
    }
    
    public void OnError(ErrorEvent evt)
    {
        WriteLine($"❌ ERROR in {evt.Phase} | {evt.Exception.Message}");
        WriteLine($"   {evt.Exception.GetType().Name}: {evt.Exception.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
    }
    
    private void WriteLine(string message)
    {
        try
        {
            _output.WriteLine(message);
        }
        catch
        {
            // Ignore - test output may be disposed
        }
    }
    
    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength - 3) + "...";
    }
}

/// <summary>
/// xUnit logger provider for MCP clients
/// </summary>
internal class XUnitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;
    
    public XUnitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }
    
    public ILogger CreateLogger(string categoryName)
    {
        return new XUnitLogger(_output, categoryName);
    }
    
    public void Dispose() { }
}

internal class XUnitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;
    
    public XUnitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }
    
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        try
        {
            _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
            if (exception != null)
            {
                _output.WriteLine(exception.ToString());
            }
        }
        catch
        {
            // Ignore - test output may be disposed
        }
    }
}

