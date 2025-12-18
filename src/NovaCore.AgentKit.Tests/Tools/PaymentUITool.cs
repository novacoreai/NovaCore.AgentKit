using System.Text.Json.Serialization;
using NovaCore.AgentKit.Core;

namespace NovaCore.AgentKit.Tests.Tools;

/// <summary>
/// UI tool for testing human-in-the-loop interactions (payment flow)
/// </summary>
public class PaymentUITool : UITool<PaymentArgs, PaymentResult>
{
    public override string Name => "show_payment_page";
    
    public override string Description => "Display payment interface to collect payment from user";
}

public record PaymentArgs(
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("description")] string? Description = null
);

public record PaymentResult(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("transaction_id")] string TransactionId,
    [property: JsonPropertyName("amount")] decimal Amount
);

