using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Persistence.Mapping;

public interface IWorkflowMapper
{
    WorkflowDocument ToDocument(
        Workflow workflow);

    Workflow FromDocument(
        WorkflowDocument document);
}