# PulseStackAI

Modern .NET AI framework for building agents, tools, workflows, document intelligence, and enterprise automation.

## Why PulseStackAI?

PulseStackAI helps .NET developers build production-ready AI systems without wrestling with provider complexity or fragmented tooling.

Use it to create:

* AI copilots
* Multi-agent workflows
* Tool-calling assistants
* Document intelligence systems
* ERP / CRM AI integrations
* Approval insight engines
* Enterprise knowledge assistants

## Core Features

* Unified `IChatClient` abstraction
* OpenAI / Azure OpenAI / local model providers
* AgentBuilder fluent API
* Multi-agent pipelines (sequential / parallel / routed)
* Tool registry with DI integration
* Streaming responses
* Logging / telemetry
* Clean architecture
* Extensible plugin model

## Quick Start

```csharp
builder.Services.AddPulseStack()
    .UseOpenAI("your-key")
    .AddTools();

var ai = sp.GetRequiredService<IChatClient>();

var result = await ai.AskAsync("Hello world");
```

## Solution Structure

```text
src/
  PulseStack.Abstractions
  PulseStack.Core
  PulseStack.Agents
  PulseStack.Tools
  PulseStack.Providers.OpenAI
  PulseStack.Providers.AzureOpenAI
  PulseStack.AspNetCore
```

## Roadmap

* v0.1 Core chat + tools + agents
* v0.2 MCP + documents
* v0.3 Vector search + RAG
* v1.0 Enterprise platform

## License

MIT
