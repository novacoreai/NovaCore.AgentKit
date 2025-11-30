namespace NovaCore.AgentKit.Core;

/// <summary>
/// Marker interface for UI tools.
/// UI tools are returned to the host for user interaction instead of being executed internally.
/// Use AgentBuilder.WithUITool() to register UI tools.
/// </summary>
public interface IUITool : ITool
{
    // Marker interface - no additional members needed for now
    // Can add UI-specific metadata in future if needed
}

