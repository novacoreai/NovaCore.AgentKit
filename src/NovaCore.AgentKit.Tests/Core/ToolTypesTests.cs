using System.Text.Json;
using System.Text.Json.Serialization;
using NovaCore.AgentKit.Core;
using Xunit;

namespace NovaCore.AgentKit.Tests.Core;

/// <summary>
/// Tests for different tool type patterns (Tool, SimpleTool, UITool, ITool)
/// </summary>
public class ToolTypesTests
{
    [Fact]
    public void GenericTool_AutoGeneratesSchema()
    {
        var tool = new TestGenericTool();
        var schema = tool.ParameterSchema;
        var schemaJson = schema.RootElement.GetRawText();
        
        Assert.Contains("input", schemaJson, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public async Task GenericTool_ExecutesWithPOCO()
    {
        var tool = new TestGenericTool();
        var argsJson = JsonSerializer.Serialize(new { input = "test" });
        
        var resultJson = await tool.InvokeAsync(argsJson);
        var result = JsonSerializer.Deserialize<TestGenericResult>(resultJson);
        
        Assert.NotNull(result);
        Assert.Equal("Processed: test", result.Output);
    }
    
    [Fact]
    public async Task SimpleTool_ReturnsStandardResponse()
    {
        var tool = new TestSimpleTool();
        var argsJson = JsonSerializer.Serialize(new { name = "Alice" });
        
        var resultJson = await tool.InvokeAsync(argsJson);
        var result = JsonSerializer.Deserialize<ToolResponse>(resultJson);
        
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Hello, Alice!", result.Message);
    }
    
    [Fact]
    public async Task SimpleTool_HandlesExceptions()
    {
        var tool = new TestThrowingTool();
        var argsJson = JsonSerializer.Serialize(new { input = "test" });
        
        var resultJson = await tool.InvokeAsync(argsJson);
        var result = JsonSerializer.Deserialize<ToolResponse>(resultJson);
        
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Test error", result.Error!);
    }
    
    // Test tools
    private class TestGenericTool : Tool<TestGenericArgs, TestGenericResult>
    {
        public override string Name => "test_generic";
        public override string Description => "Test tool";
        
        protected override Task<TestGenericResult> ExecuteAsync(TestGenericArgs args, CancellationToken ct)
        {
            return Task.FromResult(new TestGenericResult { Output = $"Processed: {args.Input}" });
        }
    }
    
    private class TestSimpleTool : SimpleTool<TestSimpleArgs>
    {
        public override string Name => "test_simple";
        public override string Description => "Simple test tool";
        
        protected override Task<string> RunAsync(TestSimpleArgs args, CancellationToken ct)
        {
            return Task.FromResult($"Hello, {args.Name}!");
        }
    }
    
    private class TestThrowingTool : SimpleTool<TestGenericArgs>
    {
        public override string Name => "test_throwing";
        public override string Description => "Throwing test tool";
        
        protected override Task<string> RunAsync(TestGenericArgs args, CancellationToken ct)
        {
            throw new InvalidOperationException("Test error");
        }
    }
    
    private record TestGenericArgs([property: JsonPropertyName("input")] string Input);
    private record TestGenericResult { [JsonPropertyName("output")] public string Output { get; init; } = ""; }
    private record TestSimpleArgs([property: JsonPropertyName("name")] string Name);
}

