namespace PulseStack.Abstractions.Runtime.Realization.Validation;

public static class WorkflowGraphValidationCodes
{
    public const string AgentDefinitionUnavailable = "WFG001";
    public const string AgentReferenceUrnConflict = "WFG002";
    public const string CatalogDefinitionMismatch = "WFG003";
    public const string AgentGraphInvalid = "WFG004";
    public const string ConditionBindingUnavailable = "WFG005";
}
