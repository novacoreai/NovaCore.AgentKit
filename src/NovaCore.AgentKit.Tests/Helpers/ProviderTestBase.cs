using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Helpers;

/// <summary>
/// Base class for provider tests with common setup
/// </summary>
public abstract class ProviderTestBase
{
    protected readonly ITestOutputHelper Output;
    protected readonly ILoggerFactory LoggerFactory;
    protected readonly ILogger Logger;
    
    protected ProviderTestBase(ITestOutputHelper output)
    {
        Output = output;
        
        // Create logger that writes to xUnit output
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XUnitLoggerProvider(output));
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        Logger = LoggerFactory.CreateLogger(GetType().Name);
    }
}

/// <summary>
/// xUnit logger provider that writes to test output
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

