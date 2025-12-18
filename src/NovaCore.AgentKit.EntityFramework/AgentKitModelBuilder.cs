using Microsoft.EntityFrameworkCore;
using NovaCore.AgentKit.EntityFramework.Models;

namespace NovaCore.AgentKit.EntityFramework;

/// <summary>
/// Helper for configuring AgentKit entities in the hosting app's DbContext
/// </summary>
public static class AgentKitModelBuilder
{
    /// <summary>
    /// Configure AgentKit entity models
    /// </summary>
    public static ModelBuilder ConfigureAgentKitModels(this ModelBuilder modelBuilder)
    {
        // ChatSession
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConversationId).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.UserId });
            entity.HasIndex(e => e.LastActivityAt);
            
            entity.Property(e => e.ConversationId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TenantId).HasMaxLength(100);
            entity.Property(e => e.UserId).HasMaxLength(200);
            
            entity.HasMany(e => e.Turns)
                .WithOne(e => e.Session)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // ChatTurn
        modelBuilder.Entity<ChatTurn>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SessionId, e.TurnNumber });
            
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Role).HasConversion<int>();
            entity.Property(e => e.ToolCallId).HasMaxLength(100);
            
            entity.HasMany(e => e.ToolExecutions)
                .WithOne(e => e.Turn)
                .HasForeignKey(e => e.TurnId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // ToolExecution
        modelBuilder.Entity<ToolExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TurnId);
            entity.HasIndex(e => e.ToolName);
            entity.HasIndex(e => e.ExecutedAt);
            
            entity.Property(e => e.ToolName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Arguments).IsRequired();
        });
        
        // ConversationCheckpoint
        modelBuilder.Entity<ConversationCheckpoint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SessionId, e.UpToTurnNumber });
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.Summary).IsRequired();
            
            entity.HasOne(e => e.Session)
                .WithMany(e => e.Checkpoints)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        return modelBuilder;
    }
}

