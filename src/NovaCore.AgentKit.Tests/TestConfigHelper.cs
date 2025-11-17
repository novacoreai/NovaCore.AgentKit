using Microsoft.Extensions.Configuration;

namespace NovaCore.AgentKit.Tests;

/// <summary>
/// Helper to load test configuration
/// </summary>
public static class TestConfigHelper
{
    private static IConfiguration? _configuration;
    private static TestConfig? _testConfig;
    
    public static IConfiguration Configuration
    {
        get
        {
            if (_configuration == null)
            {
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("testconfig.json", optional: false)
                    .Build();
            }
            return _configuration;
        }
    }
    
    public static TestConfig GetConfig()
    {
        if (_testConfig == null)
        {
            _testConfig = new TestConfig
            {
                Providers = new ProvidersConfig
                {
                    Anthropic = new ProviderConfig
                    {
                        ApiKey = Configuration["Providers:Anthropic:ApiKey"]!,
                        Model = Configuration["Providers:Anthropic:Model"]!
                    },
                    Google = new ProviderConfig
                    {
                        ApiKey = Configuration["Providers:Google:ApiKey"]!,
                        Model = Configuration["Providers:Google:Model"]!
                    },
                    XAI = new ProviderConfig
                    {
                        ApiKey = Configuration["Providers:XAI:ApiKey"]!,
                        Model = Configuration["Providers:XAI:Model"]!
                    },
                    OpenAI = new ProviderConfig
                    {
                        ApiKey = Configuration["Providers:OpenAI:ApiKey"]!,
                        Model = Configuration["Providers:OpenAI:Model"]!,
                        ReasoningEffort = Configuration["Providers:OpenAI:ReasoningEffort"]
                    },
                    Groq = new ProviderConfig
                    {
                        ApiKey = Configuration["Providers:Groq:ApiKey"]!,
                        Model = Configuration["Providers:Groq:Model"]!
                    }
                }
            };
        }
        return _testConfig;
    }
}

public class TestConfig
{
    public required ProvidersConfig Providers { get; init; }
}

public class ProvidersConfig
{
    public required ProviderConfig Anthropic { get; init; }
    public required ProviderConfig Google { get; init; }
    public required ProviderConfig XAI { get; init; }
    public required ProviderConfig OpenAI { get; init; }
    public required ProviderConfig Groq { get; init; }
}

public class ProviderConfig
{
    public required string ApiKey { get; init; }
    public required string Model { get; init; }
    public string? ReasoningEffort { get; init; }
}






