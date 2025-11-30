using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;

namespace NovaCore.AgentKit.Tests.Core;

/// <summary>
/// Unit tests for UI tool functionality
/// </summary>
public class UIToolTests
{
    [Fact]
    public void UITool_ImplementsIUITool()
    {
        // Arrange
        var tool = new PaymentUITool();
        
        // Assert
        Assert.IsAssignableFrom<IUITool>(tool);
        Assert.IsAssignableFrom<ITool>(tool);
        Assert.Equal("show_payment_page", tool.Name);
    }
    
    [Fact]
    public void UITool_GeneratesSchemaAutomatically()
    {
        // Arrange
        var tool = new PaymentUITool();
        
        // Act
        var schema = tool.ParameterSchema;
        var schemaJson = schema.RootElement.GetRawText();
        
        // Assert
        Assert.NotNull(schema);
        Assert.Contains("amount", schemaJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("currency", schemaJson, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public async Task UITool_ReturnsErrorOnExecution()
    {
        // Arrange
        var tool = new PaymentUITool();
        var argsJson = @"{""amount"": 99.99, ""currency"": ""USD""}";
        
        // Act
        var result = await tool.InvokeAsync(argsJson);
        
        // Assert - UI tools should return an error response when executed
        Assert.Contains("should not be executed internally", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("success", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("false", result, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public void UIToolBaseClass_ProvidesCleanAPI()
    {
        // Arrange & Act
        var tool = new PaymentUITool();
        
        // Assert
        Assert.Equal("show_payment_page", tool.Name);
        Assert.NotEmpty(tool.Description);
        Assert.NotNull(tool.ParameterSchema);
    }
}

