using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Evaluation;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Tools;
using PulseStack.Core.Runtime.Realization;
using PulseStack.Core.Runtime.Realization.Composition;

namespace PulseStack.Agents.Runtime.Realization;

public sealed class ApplicationRealizationChainFactory : IApplicationRealizationChainFactory
{
    private readonly ModelRealizer _modelRealizer;
    private readonly PromptRealizer _promptRealizer;
    private readonly IToolBindingResolver _toolBindingResolver;
    private readonly IKnowledgeBindingResolver _knowledgeBindingResolver;
    private readonly IMemoryBindingResolver _memoryBindingResolver;
    private readonly IPolicyBindingResolver _policyBindingResolver;
    private readonly IToolExecutor _toolExecutor;
    private readonly IConditionBindingResolver _conditionBindingResolver;
    private readonly IWorkflowValueEvaluator _workflowValueEvaluator;

    public ApplicationRealizationChainFactory(
        ModelRealizer modelRealizer,
        PromptRealizer promptRealizer,
        IToolBindingResolver toolBindingResolver,
        IKnowledgeBindingResolver knowledgeBindingResolver,
        IMemoryBindingResolver memoryBindingResolver,
        IPolicyBindingResolver policyBindingResolver,
        IToolExecutor toolExecutor,
        IConditionBindingResolver conditionBindingResolver,
        IWorkflowValueEvaluator workflowValueEvaluator)
    {
        ArgumentNullException.ThrowIfNull(modelRealizer);
        ArgumentNullException.ThrowIfNull(promptRealizer);
        ArgumentNullException.ThrowIfNull(toolBindingResolver);
        ArgumentNullException.ThrowIfNull(knowledgeBindingResolver);
        ArgumentNullException.ThrowIfNull(memoryBindingResolver);
        ArgumentNullException.ThrowIfNull(policyBindingResolver);
        ArgumentNullException.ThrowIfNull(toolExecutor);
        ArgumentNullException.ThrowIfNull(conditionBindingResolver);
        ArgumentNullException.ThrowIfNull(workflowValueEvaluator);

        _modelRealizer = modelRealizer;
        _promptRealizer = promptRealizer;
        _toolBindingResolver = toolBindingResolver;
        _knowledgeBindingResolver = knowledgeBindingResolver;
        _memoryBindingResolver = memoryBindingResolver;
        _policyBindingResolver = policyBindingResolver;
        _toolExecutor = toolExecutor;
        _conditionBindingResolver = conditionBindingResolver;
        _workflowValueEvaluator = workflowValueEvaluator;
    }

    public IWorkflowComposer Create(IAssetResolver assetResolver)
    {
        ArgumentNullException.ThrowIfNull(assetResolver);

        var agentComposer = new AgentComposer(
            assetResolver,
            _modelRealizer,
            _promptRealizer,
            _toolBindingResolver,
            _knowledgeBindingResolver,
            _memoryBindingResolver,
            _policyBindingResolver,
            _toolExecutor);

        return new WorkflowComposer(
            assetResolver,
            agentComposer,
            _conditionBindingResolver,
            _workflowValueEvaluator);
    }
}
