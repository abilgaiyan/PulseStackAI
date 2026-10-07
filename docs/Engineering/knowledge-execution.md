# Bound Knowledge execution

Realized agents retrieve their authored, bound `IKnowledgeSource` sources sequentially once per concrete call. The query snapshots the presented input; source and item order are preserved. Tool iterations reuse that call's contribution. A retry is another concrete call and can observe changed input or source material.

The runtime adds one User-role message labelled "Retrieved reference material" after conversation history and before the final user input. Source names label material; labels are not citations. Retrieved material is call-local and is never added to conversation memory.

Null results, null Items, null/empty/whitespace items, retrieval failures and contribution overflow stop preparation before memory mutation or a provider request. An empty collection contributes nothing. The received cancellation token passes unchanged to retrieval and provider calls. Streaming performs the same preparation before its first provider call or yield.

## Consumer composition

`KnowledgeExecutionOptions` belongs to `PulseStack.Abstractions.Knowledge`. Register the consumer-owned options before or after `AddPulseStackAgents()` using the ordinary DI singleton override:

```csharp
services.AddSingleton(new KnowledgeExecutionOptions
{
    MaxContributionBytes = 64 * 1024
});
services.AddPulseStackAgents();
```

The positive limit is validated and snapshotted by agent/application composition and direct runtime construction. The default is 65536 UTF-8 bytes for the complete contribution, including heading, source labels, separators and content. Overflow fails without truncation. Configuration is independent of persisted Knowledge assets and pipeline execution policy.

This slice adds no retrieval provider, index, vector store, cache, citation protocol or document edition identity. Existing binding remains authoritative.

## Validation

Focused tests: `KnowledgeExecutionTests`. Build and execution validation must be performed with the repository's .NET SDK; remote source review alone does not establish a passing build.
