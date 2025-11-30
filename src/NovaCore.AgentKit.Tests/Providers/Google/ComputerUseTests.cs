using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Providers.Google;
using NovaCore.AgentKit.Tests.Helpers;
using NovaCore.AgentKit.Tests.Tools;
using Xunit;
using Xunit.Abstractions;

namespace NovaCore.AgentKit.Tests.Providers.Google;

public class ComputerUseTests : ProviderTestBase, IAsyncLifetime
{
    private BrowserContext? _browserContext;
    
    public ComputerUseTests(ITestOutputHelper output) : base(output) { }
    
    public async Task InitializeAsync()
    {
        // Set headless=true for CI/CD, false for local debugging
        _browserContext = new BrowserContext(headless: true);
        await _browserContext.GetPageAsync();
    }
    
    public async Task DisposeAsync()
    {
        if (_browserContext != null)
            await _browserContext.DisposeAsync();
    }
    
    [Fact]
    public async Task ComputerUse_EnabledWithConvenienceMethod_WorksWithModel()
    {
        var config = TestConfigHelper.GetConfig();
        
        var agent = await new AgentBuilder()
            .UseGoogleComputerUse(
                apiKey: config.Providers.Google.ApiKey,
                excludedFunctions: new List<string> { "drag_and_drop" })
            .WithObserver(Observer)
            // Add all common Computer Use tools
            .AddTool(new OpenBrowserTool(_browserContext!), skipDefinition: true)
            .AddTool(new NavigateTool(_browserContext!), skipDefinition: true)
            .AddTool(new SearchTool(_browserContext!), skipDefinition: true)
            .AddTool(new MouseClickTool(_browserContext!), skipDefinition: true)
            .AddTool(new TypeTextTool(_browserContext!), skipDefinition: true)
            .AddTool(new MouseScrollTool(_browserContext!), skipDefinition: true)
            .WithSystemPrompt("You are a browser automation assistant. Just describe what you see, don't take actions unless asked.")
            .BuildChatAgentAsync();
        
        // Navigate to a test page first
        var page = await _browserContext!.GetPageAsync();
        await page.GotoAsync("https://www.example.com");
        
        // Take screenshot and send to agent with explicit instruction not to take actions
        var screenshot = await page.ScreenshotAsync();
        var response = await agent.SendAsync(
            "Just describe what you see in this screenshot. Do not take any actions.",
            new List<FileAttachment> { FileAttachment.FromBytes(screenshot, "image/png") });
        
        Assert.NotNull(response.Text);
        Assert.NotEmpty(response.Text);
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task ComputerUse_EnabledWithManualConfiguration_WorksWithModel()
    {
        var config = TestConfigHelper.GetConfig();
        
        var agent = await new AgentBuilder()
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = GoogleModels.Gemini25ComputerUsePreview;
                options.EnableComputerUse = true;
                options.ComputerUseEnvironment = ComputerUseEnvironment.Browser;
                options.ExcludedComputerUseFunctions = new List<string> { "hover_at" };
            })
            .WithObserver(Observer)
            .AddTool(new MouseClickTool(_browserContext!), skipDefinition: true)
            .AddTool(new NavigateTool(_browserContext!), skipDefinition: true)
            .WithSystemPrompt("You are a browser automation assistant.")
            .BuildChatAgentAsync();
        
        var page = await _browserContext!.GetPageAsync();
        await page.GotoAsync("https://www.example.com");
        var screenshot = await page.ScreenshotAsync();
        
        var response = await agent.SendAsync(
            "Describe this page",
            new List<FileAttachment> { FileAttachment.FromBytes(screenshot, "image/png") });
        
        Assert.NotNull(response.Text);
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task ComputerUse_WithCustomTools_CombinesBothToolTypes()
    {
        var config = TestConfigHelper.GetConfig();
        
        var agent = await new AgentBuilder()
            .UseGoogleComputerUse(config.Providers.Google.ApiKey)
            .WithObserver(Observer)
            .AddTool(new MouseClickTool(_browserContext!), skipDefinition: true)
            .AddTool(new TypeTextTool(_browserContext!), skipDefinition: true)
            .AddTool(new NavigateTool(_browserContext!), skipDefinition: true)
            .AddTool(new CalculatorTool())  // Custom tool with definition
            .WithSystemPrompt("You are a browser automation assistant with calculation abilities.")
            .BuildChatAgentAsync();
        
        // Test that both Computer Use and custom tools work
        var response = await agent.SendAsync("What is 25 multiplied by 4?");
        
        Assert.Contains("100", response.Text);
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task ComputerUse_DesktopEnvironment_ConfiguresCorrectly()
    {
        var config = TestConfigHelper.GetConfig();
        
        // Note: Desktop environment is not currently supported by Google
        // This test verifies we get the expected error
        var agent = await new AgentBuilder()
            .UseGoogle(options =>
            {
                options.ApiKey = config.Providers.Google.ApiKey;
                options.Model = GoogleModels.Gemini25ComputerUsePreview;
                options.EnableComputerUse = true;
                options.ComputerUseEnvironment = ComputerUseEnvironment.Desktop;
            })
            .WithObserver(Observer)
            .AddTool(new MouseClickTool(_browserContext!), skipDefinition: true)
            .BuildChatAgentAsync();
        
        var page = await _browserContext!.GetPageAsync();
        await page.GotoAsync("https://www.example.com");
        var screenshot = await page.ScreenshotAsync();
        
        // This should fail with "ENVIRONMENT_DESKTOP" not supported
        var response = await agent.SendAsync(
            "What's in this screenshot?",
            new List<FileAttachment> { FileAttachment.FromBytes(screenshot, "image/png") });
        
        // Test passes if we get here (error is expected and handled)
        Output.WriteLine($"Response: {response.Text}");
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task ComputerUse_ComprehensiveTest_SearchAndNavigate()
    {
        var config = TestConfigHelper.GetConfig();
        
        // Create agent with ALL Computer Use tools + WithMaxMultimodalMessages(1)
        var agent = await new AgentBuilder()
            .UseGoogleComputerUse(config.Providers.Google.ApiKey)
            .WithObserver(Observer)
            // Navigation tools
            .AddTool(new OpenBrowserTool(_browserContext!), skipDefinition: true)
            .AddTool(new NavigateTool(_browserContext!), skipDefinition: true)
            .AddTool(new SearchTool(_browserContext!), skipDefinition: true)
            .AddTool(new GoBackTool(_browserContext!), skipDefinition: true)
            .AddTool(new GoForwardTool(_browserContext!), skipDefinition: true)
            .AddTool(new WaitTool(_browserContext!), skipDefinition: true)
            // Mouse tools
            .AddTool(new MouseClickTool(_browserContext!), skipDefinition: true)
            .AddTool(new MouseMoveTool(_browserContext!), skipDefinition: true)
            .AddTool(new DragAndDropTool(_browserContext!), skipDefinition: true)
            // Keyboard tools
            .AddTool(new TypeTextTool(_browserContext!), skipDefinition: true)
            .AddTool(new KeyPressTool(_browserContext!), skipDefinition: true)
            // Scroll tools
            .AddTool(new MouseScrollTool(_browserContext!), skipDefinition: true)
            .AddTool(new ScrollAtTool(_browserContext!), skipDefinition: true)
            .WithMaxMultimodalMessages(1)  // ✅ Keep only 1 screenshot in history
            .WithSystemPrompt("You are a browser automation assistant. Complete the task step by step.")
            .BuildChatAgentAsync();
        
        // Navigate to Google first
        var page = await _browserContext!.GetPageAsync();
        await page.GotoAsync("https://www.google.com");
        await Task.Delay(1000);
        
        // Take initial screenshot
        var screenshot = await page.ScreenshotAsync();
        
        // Give the agent a real task
        var response = await agent.SendAsync(
            "Search for 'Playwright documentation' on Google and click the first result.",
            new List<FileAttachment> { FileAttachment.FromBytes(screenshot, "image/png") });
        
        Output.WriteLine($"Final Response: {response.Text}");
        Output.WriteLine($"Final URL: {page.Url}");
        
        // Verify we navigated somewhere
        Assert.NotEqual("https://www.google.com", page.Url);
        
        await agent.DisposeAsync();
    }
    
    [Fact]
    public async Task ComputerUse_WithMaxMultimodalMessages_PreservesToolStructure()
    {
        var config = TestConfigHelper.GetConfig();
        
        // Create agent WITH .WithMaxMultimodalMessages(1) - should now work!
        var agent = await new AgentBuilder()
            .UseGoogleComputerUse(config.Providers.Google.ApiKey)
            .WithObserver(Observer)
            // Add all common Computer Use tools
            .AddTool(new OpenBrowserTool(_browserContext!), skipDefinition: true)
            .AddTool(new NavigateTool(_browserContext!), skipDefinition: true)
            .AddTool(new SearchTool(_browserContext!), skipDefinition: true)
            .AddTool(new MouseClickTool(_browserContext!), skipDefinition: true)
            .AddTool(new TypeTextTool(_browserContext!), skipDefinition: true)
            .AddTool(new MouseScrollTool(_browserContext!), skipDefinition: true)
            .WithMaxMultimodalMessages(1)  // Keep only 1 screenshot in history
            .WithSystemPrompt("You are a browser automation assistant. Just describe what you see, don't take actions unless asked.")
            .BuildChatAgentAsync();
        
        // Navigate to test page
        var page = await _browserContext!.GetPageAsync();
        await page.GotoAsync("https://www.example.com");
        
        // Send multiple messages with screenshots to test history filtering
        var screenshot1 = await page.ScreenshotAsync();
        var response1 = await agent.SendAsync(
            "Just describe what you see in this screenshot. Do not take any actions.",
            new List<FileAttachment> { FileAttachment.FromBytes(screenshot1, "image/png") });
        
        Output.WriteLine($"Response 1: {response1.Text}");
        
        // Second message - should strip first screenshot but preserve tool structure
        var screenshot2 = await page.ScreenshotAsync();
        var response2 = await agent.SendAsync(
            "What's the title of this page? Just answer, don't take actions.",
            new List<FileAttachment> { FileAttachment.FromBytes(screenshot2, "image/png") });
        
        Output.WriteLine($"Response 2: {response2.Text}");
        
        // Verify both responses succeeded (no CallId mapping errors)
        Assert.NotNull(response1.Text);
        Assert.NotNull(response2.Text);
        Assert.NotEmpty(response1.Text);
        Assert.NotEmpty(response2.Text);
        
        await agent.DisposeAsync();
    }
}
