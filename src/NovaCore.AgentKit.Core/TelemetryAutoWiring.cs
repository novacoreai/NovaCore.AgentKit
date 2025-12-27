namespace NovaCore.AgentKit.Core;

/// <summary>
/// Best-effort auto-wiring for optional telemetry packages.
/// If the OpenTelemetry extension package is referenced by the host app, Core will
/// automatically attach its metrics observer without requiring explicit host code.
/// </summary>
internal static class TelemetryAutoWiring
{
    private const string MetricsObserverTypeName =
        "NovaCore.AgentKit.Extensions.OpenTelemetry.AgentKitMetricsObserver, NovaCore.AgentKit.Extensions.OpenTelemetry";
    
    private const string MetricsObserverFullName =
        "NovaCore.AgentKit.Extensions.OpenTelemetry.AgentKitMetricsObserver";
    
    public static IAgentObserver? AttachMetricsObserverIfAvailable(IAgentObserver? existing)
    {
        // Avoid double-wiring if host already provided the metrics observer.
        if (ContainsObserver(existing, MetricsObserverFullName))
        {
            return existing;
        }
        
        var metricsObserver = TryCreateMetricsObserver();
        if (metricsObserver == null)
        {
            return existing;
        }
        
        if (existing == null)
        {
            return metricsObserver;
        }
        
        return new CompositeAgentObserver(new[] { existing, metricsObserver });
    }
    
    private static bool ContainsObserver(IAgentObserver? observer, string fullTypeName)
    {
        if (observer == null) return false;
        
        if (observer.GetType().FullName == fullTypeName) return true;
        
        if (observer is CompositeAgentObserver composite)
        {
            return composite.Observers.Any(o => o.GetType().FullName == fullTypeName);
        }
        
        return false;
    }
    
    private static IAgentObserver? TryCreateMetricsObserver()
    {
        try
        {
            // Type.GetType with an assembly-qualified name will attempt to load the assembly if present.
            var type = Type.GetType(MetricsObserverTypeName, throwOnError: false);
            if (type == null)
            {
                return null;
            }
            
            var instance = Activator.CreateInstance(type);
            return instance as IAgentObserver;
        }
        catch
        {
            // Telemetry is always optional; never fail agent construction.
            return null;
        }
    }
}


