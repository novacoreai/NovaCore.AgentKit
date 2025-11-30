using System.Text.Json.Serialization;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Tests.Tools;

/// <summary>
/// Simple calculator tool for testing - uses POCO pattern
/// </summary>
public class CalculatorTool : Tool<CalculatorArgs, CalculatorResult>
{
    public override string Name => "calculator";
    
    public override string Description => "Perform basic math operations (add, subtract, multiply, divide)";
    
    protected override Task<CalculatorResult> ExecuteAsync(CalculatorArgs args, CancellationToken ct)
    {
        double result = args.Operation switch
        {
            "add" => args.A + args.B,
            "subtract" => args.A - args.B,
            "multiply" => args.A * args.B,
            "divide" => args.B != 0 ? args.A / args.B : throw new InvalidOperationException("Division by zero"),
            _ => throw new ArgumentException($"Unknown operation: {args.Operation}")
        };
        
        return Task.FromResult(new CalculatorResult(result, args.Operation));
    }
}

public record CalculatorArgs(
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("a")] double A,
    [property: JsonPropertyName("b")] double B
);

public record CalculatorResult(
    [property: JsonPropertyName("result")] double Result,
    [property: JsonPropertyName("operation")] string Operation
);
