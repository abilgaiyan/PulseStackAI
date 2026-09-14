using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;

namespace PulseStack.Abstractions.Runtime.Realization.Application;

/// <summary>
/// Creates the existing Workflow realization chain bound to one explicit
/// operation-specific declarative Asset resolver.
/// </summary>
public interface IApplicationRealizationChainFactory
{
    IWorkflowComposer Create(IAssetResolver assetResolver);
}
