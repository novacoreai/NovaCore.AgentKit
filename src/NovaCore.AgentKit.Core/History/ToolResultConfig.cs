namespace NovaCore.AgentKit.Core.History;

/// <summary>
/// Configuration for managing verbose tool result outputs.
/// Uses placeholder "[Omitted]" approach to maintain conversation structure while reducing token count.
/// 
/// <para><b>Use Case:</b> Browser agents and automation agents generate very verbose tool outputs.
/// This config dramatically reduces context size while keeping conversation structure intact.</para>
/// 
/// <para><b>How it works:</b></para>
/// <list type="bullet">
/// <item>Keep last KeepRecent tool results with full content</item>
/// <item>Replace older tool results with "[Omitted]" placeholder</item>
/// <item>Assistant messages are ALWAYS preserved (maintains reasoning context)</item>
/// <item>Tool call IDs are ALWAYS preserved (maintains structure validity)</item>
/// </list>
/// 
/// <para><b>Example:</b> KeepRecent = 3, and you have 10 tool calls</para>
/// <list type="bullet">
/// <item>Tool results 1-7: Replaced with "[Omitted]" (preserves structure, minimal tokens)</item>
/// <item>Tool results 8-10: Full content (relevant recent context)</item>
/// <item>All 10 Assistant messages: Fully preserved (agent's reasoning intact)</item>
/// </list>
/// </summary>
public class ToolResultConfig
{
    /// <summary>
    /// Number of recent tool results to keep with full content.
    /// Older tool results are replaced with "[Omitted]" placeholder.
    /// 
    /// <para><b>Set to 0 for unlimited</b> (all tool results have full content).</para>
    /// 
    /// <para><b>Recommended values:</b></para>
    /// <list type="bullet">
    /// <item>Browser agents (Playwright): 1-3</item>
    /// <item>ReAct agents: 5-10</item>
    /// <item>Chat agents with tools: 5</item>
    /// </list>
    /// 
    /// <para><b>Note:</b> This uses placeholders to maintain structure. Unlike the old approach,
    /// Assistant messages are never removed, ensuring the agent's reasoning context is preserved.</para>
    /// 
    /// Default: 0 (unlimited, no filtering)
    /// </summary>
    public int KeepRecent { get; set; } = 0;
    
    /// <summary>
    /// Gets a human-readable summary of the configuration.
    /// </summary>
    public string GetSummary()
    {
        if (KeepRecent == 0)
        {
            return "unlimited (no filtering)";
        }
        
        return $"keep {KeepRecent} recent, replace others with placeholders";
    }
}

