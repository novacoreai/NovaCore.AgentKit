using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NovaCore.AgentKit.Core;
using NovaCore.AgentKit.Core.History;
using NovaCore.AgentKit.EntityFramework.Models;

namespace NovaCore.AgentKit.EntityFramework;

/// <summary>
/// EF Core implementation of history store
/// </summary>
/// <typeparam name="TDbContext">The application's DbContext type</typeparam>
public class EfCoreHistoryStore<TDbContext> : IHistoryStore
    where TDbContext : DbContext
{
    private readonly TDbContext _context;
    private readonly ILogger<EfCoreHistoryStore<TDbContext>> _logger;
    private readonly string? _tenantId;
    private readonly string? _userId;
    
    public EfCoreHistoryStore(
        TDbContext context,
        ILogger<EfCoreHistoryStore<TDbContext>> logger,
        string? tenantId = null,
        string? userId = null)
    {
        _context = context;
        _logger = logger;
        _tenantId = tenantId;
        _userId = userId;
    }
    
    public async Task AppendMessageAsync(string conversationId, ChatMessage message, CancellationToken ct = default)
    {
        await AppendMessagesAsync(conversationId, new List<ChatMessage> { message }, ct);
    }
    
    public async Task AppendMessagesAsync(string conversationId, List<ChatMessage> messages, CancellationToken ct = default)
    {
        if (messages.Count == 0)
        {
            return;
        }
        
        // Get or create session
        var session = await GetOrCreateSessionAsync(conversationId, ct);
        
        // Get the current max turn number for this session
        var maxTurnNumber = await _context.Set<Models.ChatTurn>()
            .Where(t => t.SessionId == session.Id)
            .Select(t => (int?)t.TurnNumber)
            .MaxAsync(ct) ?? -1;
        
        int nextTurnNumber = maxTurnNumber + 1;
        
        // Add new messages incrementally
        foreach (var message in messages)
        {
            var turn = CreateTurnFromMessage(message, session.Id, nextTurnNumber++);
            _context.Set<Models.ChatTurn>().Add(turn);
        }
        
        // Update session activity
        session.LastActivityAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(ct);
        
        _logger.LogDebug(
            "Appended {Count} message(s) to conversation {Id}",
            messages.Count, conversationId);
    }
    
    public async Task SaveAsync(string conversationId, List<ChatMessage> history, CancellationToken ct = default)
    {
        // LEGACY METHOD: For backward compatibility, still supports full replace
        // New code should use AppendMessageAsync/AppendMessagesAsync
        var session = await GetOrCreateSessionAsync(conversationId, ct);
        
        // Remove old turns
        var existingTurns = await _context.Set<Models.ChatTurn>()
            .Where(t => t.SessionId == session.Id)
            .ToListAsync(ct);
        
        if (existingTurns.Any())
        {
            _context.Set<Models.ChatTurn>().RemoveRange(existingTurns);
        }
        
        // Add new turns
        int turnNumber = 0;
        foreach (var message in history)
        {
            var turn = CreateTurnFromMessage(message, session.Id, turnNumber++);
            _context.Set<Models.ChatTurn>().Add(turn);
        }
        
        // Update session activity
        session.LastActivityAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(ct);
        
        _logger.LogDebug(
            "Saved (full replace) conversation {Id} with {Count} turns",
            conversationId, history.Count);
    }
    
    private async Task<ChatSession> GetOrCreateSessionAsync(string conversationId, CancellationToken ct)
    {
        var session = await _context.Set<ChatSession>()
            .FirstOrDefaultAsync(s => s.ConversationId == conversationId, ct);
        
        if (session == null)
        {
            session = new ChatSession
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                TenantId = _tenantId,
                UserId = _userId,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                IsActive = true
            };
            _context.Set<ChatSession>().Add(session);
            await _context.SaveChangesAsync(ct);
        }
        
        return session;
    }
    
    private Models.ChatTurn CreateTurnFromMessage(ChatMessage message, Guid sessionId, int turnNumber)
    {
        string? contentJson = null;
        
        // Serialize multimodal content if present
        if (message.Contents?.Any() == true)
        {
            var serializableContents = new List<Models.SerializableContent>();
            foreach (var content in message.Contents)
            {
                if (content is TextMessageContent textContent)
                {
                    serializableContents.Add(new Models.SerializableContent
                    {
                        Type = "text",
                        Text = textContent.Text
                    });
                }
                else if (content is ImageMessageContent imageContent)
                {
                    serializableContents.Add(new Models.SerializableContent
                    {
                        Type = "data",
                        Base64Data = Convert.ToBase64String(imageContent.Data),
                        MediaType = imageContent.MimeType
                    });
                }
            }
            
            contentJson = System.Text.Json.JsonSerializer.Serialize(serializableContents);
        }
        
        // Serialize tool calls if present
        string? toolCallsJson = null;
        if (message.ToolCalls?.Any() == true)
        {
            toolCallsJson = System.Text.Json.JsonSerializer.Serialize(message.ToolCalls);
        }
        
        return new Models.ChatTurn
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            TurnNumber = turnNumber,
            Role = message.Role,
            Content = message.Text ?? "",
            ContentJson = contentJson,
            ToolCallsJson = toolCallsJson,
            CreatedAt = DateTime.UtcNow,
            ToolCallId = message.ToolCallId
        };
    }
    
    public async Task<List<ChatMessage>?> LoadAsync(string conversationId, CancellationToken ct = default)
    {
        var session = await _context.Set<ChatSession>()
            .Include(s => s.Turns)
            .FirstOrDefaultAsync(s => s.ConversationId == conversationId, ct);
        
        if (session == null)
        {
            return null;
        }
        
        var messages = session.Turns
            .OrderBy(t => t.TurnNumber)
            .Select(t => 
            {
                // Deserialize tool calls if present
                List<ToolCall>? toolCalls = null;
                if (!string.IsNullOrEmpty(t.ToolCallsJson))
                {
                    try
                    {
                        toolCalls = System.Text.Json.JsonSerializer
                            .Deserialize<List<ToolCall>>(t.ToolCallsJson);
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        // Log warning but continue
                        _logger.LogWarning("Failed to deserialize tool calls for turn {TurnId}", t.Id);
                    }
                }
                
                // Deserialize multimodal content if present
                if (!string.IsNullOrEmpty(t.ContentJson))
                {
                    try
                    {
                        var serializableContents = System.Text.Json.JsonSerializer
                            .Deserialize<List<Models.SerializableContent>>(t.ContentJson);
                        
                        if (serializableContents?.Any() == true)
                        {
                            var contents = new List<IMessageContent>();
                            foreach (var sc in serializableContents)
                            {
                                if (sc.Type == "text" && sc.Text != null)
                                {
                                    contents.Add(new TextMessageContent(sc.Text));
                                }
                                else if (sc.Type == "data" && sc.Base64Data != null && sc.MediaType != null)
                                {
                                    var data = Convert.FromBase64String(sc.Base64Data);
                                    contents.Add(new ImageMessageContent(data, sc.MediaType));
                                }
                            }
                            
                            return new ChatMessage(t.Role, contents, t.ToolCallId)
                            {
                                ToolCalls = toolCalls
                            };
                        }
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        // Fall back to text-only message
                    }
                }
                
                // Text-only message (with or without tool calls)
                return new ChatMessage(t.Role, t.Content, toolCalls);
            })
            .ToList();
        
        _logger.LogDebug(
            "Loaded conversation {Id} with {Count} turns",
            conversationId, messages.Count);
        
        return messages;
    }
    
    public async Task<int> GetMessageCountAsync(string conversationId, CancellationToken ct = default)
    {
        var session = await _context.Set<ChatSession>()
            .FirstOrDefaultAsync(s => s.ConversationId == conversationId, ct);
        
        if (session == null)
        {
            return 0;
        }
        
        return await _context.Set<Models.ChatTurn>()
            .Where(t => t.SessionId == session.Id)
            .CountAsync(ct);
    }
    
    public async Task DeleteAsync(string conversationId, CancellationToken ct = default)
    {
        var session = await _context.Set<ChatSession>()
            .FirstOrDefaultAsync(s => s.ConversationId == conversationId, ct);
        
        if (session != null)
        {
            _context.Set<ChatSession>().Remove(session);
            await _context.SaveChangesAsync(ct);
            
            _logger.LogInformation("Deleted conversation {Id}", conversationId);
        }
    }
    
    public async Task<List<string>> ListConversationsAsync(CancellationToken ct = default)
    {
        var query = _context.Set<ChatSession>()
            .Where(s => s.IsActive)
            .AsQueryable();
        
        if (_tenantId != null)
        {
            query = query.Where(s => s.TenantId == _tenantId);
        }
        
        if (_userId != null)
        {
            query = query.Where(s => s.UserId == _userId);
        }
        
        return await query
            .OrderByDescending(s => s.LastActivityAt)
            .Select(s => s.ConversationId)
            .ToListAsync(ct);
    }
    
    public async Task CreateCheckpointAsync(
        string conversationId, 
        Core.History.ConversationCheckpoint checkpoint, 
        CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(conversationId, ct);
        
        var dbCheckpoint = new Models.ConversationCheckpoint
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            UpToTurnNumber = checkpoint.UpToTurnNumber,
            Summary = checkpoint.Summary,
            CreatedAt = checkpoint.CreatedAt,
            Metadata = checkpoint.Metadata != null 
                ? System.Text.Json.JsonSerializer.Serialize(checkpoint.Metadata) 
                : null
        };
        
        _context.Set<Models.ConversationCheckpoint>().Add(dbCheckpoint);
        await _context.SaveChangesAsync(ct);
        
        _logger.LogInformation(
            "Created checkpoint for conversation {Id} at turn {Turn}",
            conversationId, checkpoint.UpToTurnNumber);
    }
    
    public async Task<Core.History.ConversationCheckpoint?> GetLatestCheckpointAsync(
        string conversationId, 
        CancellationToken ct = default)
    {
        var session = await _context.Set<ChatSession>()
            .FirstOrDefaultAsync(s => s.ConversationId == conversationId, ct);
        
        if (session == null)
        {
            return null;
        }
        
        var dbCheckpoint = await _context.Set<Models.ConversationCheckpoint>()
            .Where(c => c.SessionId == session.Id)
            .OrderByDescending(c => c.UpToTurnNumber)
            .FirstOrDefaultAsync(ct);
        
        if (dbCheckpoint == null)
        {
            return null;
        }
        
        Dictionary<string, object>? metadata = null;
        if (!string.IsNullOrEmpty(dbCheckpoint.Metadata))
        {
            try
            {
                metadata = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, object>>(dbCheckpoint.Metadata);
            }
            catch (System.Text.Json.JsonException)
            {
                _logger.LogWarning("Failed to deserialize checkpoint metadata");
            }
        }
        
        return new Core.History.ConversationCheckpoint
        {
            UpToTurnNumber = dbCheckpoint.UpToTurnNumber,
            Summary = dbCheckpoint.Summary,
            CreatedAt = dbCheckpoint.CreatedAt,
            Metadata = metadata
        };
    }
    
    public async Task<(Core.History.ConversationCheckpoint? checkpoint, List<ChatMessage> messages)> LoadFromCheckpointAsync(
        string conversationId, 
        CancellationToken ct = default)
    {
        var checkpoint = await GetLatestCheckpointAsync(conversationId, ct);
        
        var session = await _context.Set<ChatSession>()
            .Include(s => s.Turns)
            .FirstOrDefaultAsync(s => s.ConversationId == conversationId, ct);
        
        if (session == null)
        {
            return (null, new List<ChatMessage>());
        }
        
        // Get messages after the checkpoint (or all if no checkpoint)
        var query = session.Turns.AsQueryable();
        
        if (checkpoint != null)
        {
            query = query.Where(t => t.TurnNumber > checkpoint.UpToTurnNumber);
            
            _logger.LogDebug(
                "Loading conversation {Id} from checkpoint at turn {Turn}",
                conversationId, checkpoint.UpToTurnNumber);
        }
        
        var messages = query
            .OrderBy(t => t.TurnNumber)
            .Select(t => ConvertTurnToMessage(t))
            .ToList();
        
        _logger.LogDebug(
            "Loaded {Count} messages after checkpoint for conversation {Id}",
            messages.Count, conversationId);
        
        return (checkpoint, messages);
    }
    
    private ChatMessage ConvertTurnToMessage(Models.ChatTurn t)
    {
        // Deserialize tool calls if present
        List<ToolCall>? toolCalls = null;
        if (!string.IsNullOrEmpty(t.ToolCallsJson))
        {
            try
            {
                toolCalls = System.Text.Json.JsonSerializer
                    .Deserialize<List<ToolCall>>(t.ToolCallsJson);
            }
            catch (System.Text.Json.JsonException)
            {
                _logger.LogWarning("Failed to deserialize tool calls for turn {TurnId}", t.Id);
            }
        }
        
        // Deserialize multimodal content if present
        if (!string.IsNullOrEmpty(t.ContentJson))
        {
            try
            {
                var serializableContents = System.Text.Json.JsonSerializer
                    .Deserialize<List<Models.SerializableContent>>(t.ContentJson);
                
                if (serializableContents?.Any() == true)
                {
                    var contents = new List<IMessageContent>();
                    foreach (var sc in serializableContents)
                    {
                        if (sc.Type == "text" && sc.Text != null)
                        {
                            contents.Add(new TextMessageContent(sc.Text));
                        }
                        else if (sc.Type == "data" && sc.Base64Data != null && sc.MediaType != null)
                        {
                            var data = Convert.FromBase64String(sc.Base64Data);
                            contents.Add(new ImageMessageContent(data, sc.MediaType));
                        }
                    }
                    
                    return new ChatMessage(t.Role, contents, t.ToolCallId)
                    {
                        ToolCalls = toolCalls
                    };
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Fall back to text-only message
            }
        }
        
        // Text-only message (with or without tool calls)
        return new ChatMessage(t.Role, t.Content, toolCalls);
    }
}

