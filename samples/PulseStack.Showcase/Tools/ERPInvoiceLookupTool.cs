using PulseStack.Abstractions.Tools;

namespace PulseStack.Showcase.Tools;

internal sealed class ERPInvoiceLookupTool
    : ITool
{
    public string Name =>
        "LookupInvoice";

    public string Description =>
        "Retrieves invoice information from ERP.";

    public string Category =>
        "ERP";

    public IReadOnlyCollection<string> Tags =>
        [
            "invoice",
            "erp",
            "finance"
        ];

   public ToolDescriptor Descriptor => new ToolDescriptor
    {
        Name = Name,
        Description = Description,
        ActionType = ToolActionType.Read,
        RequiredRoles = [],
        RequiredPermissions = [],
        AllowedScopes = [],
        IsDestructive = false,
        RequiresConfirmation = false
    };
    
    public async Task<IToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Simulated ERP lookup delay
        await Task.Delay(
            200,
            cancellationToken);

        var invoiceJson =
            """
            {
              "InvoiceId": "INV-1001",
              "Vendor": "Contoso Ltd",
              "Amount": 4500,
              "Currency": "USD",
              "Status": "Approved"
            }
            """;

        return ToolExecutionResult.Success(
            invoiceJson);
    }
}