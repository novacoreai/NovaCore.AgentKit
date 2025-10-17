namespace NovaCore.AgentKit.Core.CostTracking;

/// <summary>
/// Tracks LLM usage costs
/// </summary>
public class CostTracker : ICostTracker
{
    private readonly Dictionary<string, TokenMetrics> _metrics = new();
    private readonly ICostCalculator _calculator;
    private readonly object _lock = new();
    
    public CostTracker(ICostCalculator calculator)
    {
        _calculator = calculator;
    }
    
    public void TrackTokenUsage(string model, int inputTokens, int outputTokens)
    {
        lock (_lock)
        {
            if (!_metrics.ContainsKey(model))
            {
                _metrics[model] = new TokenMetrics();
            }
            
            _metrics[model].InputTokens += inputTokens;
            _metrics[model].OutputTokens += outputTokens;
        }
    }
    
    public CostSummary GetSummary()
    {
        lock (_lock)
        {
            var summary = new CostSummary();
            
            foreach (var (model, metrics) in _metrics)
            {
                var cost = _calculator.Calculate(model, metrics.InputTokens, metrics.OutputTokens);
                
                summary.Items.Add(new CostItem
                {
                    Model = model,
                    InputTokens = metrics.InputTokens,
                    OutputTokens = metrics.OutputTokens,
                    TotalCost = cost
                });
            }
            
            summary.TotalCost = summary.Items.Sum(i => i.TotalCost);
            return summary;
        }
    }
    
    public void Reset()
    {
        lock (_lock)
        {
            _metrics.Clear();
        }
    }
}

/// <summary>
/// Token usage metrics
/// </summary>
public class TokenMetrics
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

/// <summary>
/// Cost summary
/// </summary>
public class CostSummary
{
    public List<CostItem> Items { get; } = new();
    public decimal TotalCost { get; set; }
}

/// <summary>
/// Cost item for a model
/// </summary>
public class CostItem
{
    public required string Model { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public decimal TotalCost { get; init; }
}

