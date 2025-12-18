using Microsoft.EntityFrameworkCore;
using NovaCore.AgentKit.EntityFramework;

namespace NovaCore.AgentKit.Tests.Helpers;

/// <summary>
/// In-memory database context for testing
/// </summary>
public class TestDbContext : DbContext
{
    public TestDbContext() : base(new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        .Options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureAgentKitModels();
    }
}

