# PulseStackAI

Modern .NET AI framework for building agents, tools, workflows, and enterprise AI applications.

Built on top of `Microsoft.Extensions.AI`, PulseStackAI provides a clean, extensible architecture for integrating AI into real-world .NET systems.

---

# Why PulseStackAI?

PulseStackAI helps developers build production-ready AI systems without dealing with provider complexity, fragmented SDKs, or orchestration boilerplate.

Use it to create:

* AI copilots
* Multi-agent workflows
* Tool-calling assistants
* ERP / CRM AI integrations
* Enterprise knowledge assistants
* AI-powered automation systems

---

# Current Features (v0.2)

## Providers

* OpenAI provider
* Unified `IChatClient` abstraction

## Agents

* `AgentBuilder` fluent API
* Configurable instructions and temperature
* Streaming responses
* Structured tool calling

## Tools

* Tool registry
* Dependency Injection integration
* Built-in tool support
* JSON-based tool execution loop

## Pipelines

* Sequential multi-agent pipelines

---

# Installation

## Clone Repository

```bash
git clone https://github.com/abilgaiyan/PulseStackAI.git
cd PulseStackAI
```

---

# Quick Start

## Configure Services

```csharp
services.AddPulseStack()
    .UseOpenAI(apiKey, "gpt-4o-mini");
```

---

## Basic Chat

```csharp
var client = sp.GetRequiredService<IChatClient>();

var response = await client.AskAsync(
    "Explain dependency injection.");

Console.WriteLine(response);
```

---

## Build an Agent

```csharp
var agent = new AgentBuilder("Assistant", client)
    .WithInstructions("You are concise and helpful.")
    .WithTemperature(0.3f)
    .Build();

var result = await agent.RunAsync(
    "Explain async/await.");

Console.WriteLine(result.Text);
```

---

## Register Tools

```csharp
services.AddPulseStack()
    .UseOpenAI(apiKey, "gpt-4o-mini")
    .AddTool<CalculatorTool>();
```

---

## Tool-Enabled Agent

```csharp
var registry = sp.GetRequiredService<IToolRegistry>();

var agent = new AgentBuilder("Assistant", client)
    .WithInstructions("Use tools when required.")
    .WithTools(registry)
    .Build();

var result = await agent.RunAsync(
    "What is 250 * 45?");

Console.WriteLine(result.Text);
```

---

## Multi-Agent Pipeline

```csharp
var researcher = new AgentBuilder("Researcher", client)
    .WithInstructions("Research the topic and provide key findings.")
    .Build();

var writer = new AgentBuilder("Writer", client)
    .WithInstructions("Convert findings into an executive summary.")
    .Build();

var pipeline = new AgentPipeline("ResearchPipeline")
    .AddAgent(researcher)
    .AddAgent(writer);

var result = await pipeline.RunAsync(
    "AI adoption in enterprise ERP systems.");

Console.WriteLine(result.FinalOutput);
```

---

# Streaming Responses

```csharp
await foreach (var chunk in agent.StreamAsync(
    "Explain dependency injection in simple terms."))
{
    Console.Write(chunk);
}
```

---

# Solution Structure

```text
src/
  PulseStack.Abstractions
  PulseStack.Core
  PulseStack.Agents
  PulseStack.Tools
  PulseStack.Providers.OpenAI

samples/
  BasicChat
  BasicAgent
  MultiAgentPipeline
  StreamingChat

tests/
  PulseStack.Tests
```

---

# Architecture

```text
Providers
    ↓
IChatClient
    ↓
Agents
    ↓
Tools
    ↓
Pipelines
```

---

# Samples

## Run Basic Chat

```bash
dotnet run --project samples/BasicChat
```

## Run Basic Agent

```bash
dotnet run --project samples/BasicAgent
```

## Run Multi-Agent Pipeline

```bash
dotnet run --project samples/MultiAgentPipeline
```

## Run Streaming Chat

```bash
dotnet run --project samples/StreamingChat
```

---

# Roadmap

## v0.3

* Azure OpenAI provider
* Ollama provider
* Conversation memory
* Improved tool metadata
* Structured tool schemas

## v0.4

* Vector storage
* RAG support
* Document indexing
* Semantic search

## v1.0

* Enterprise workflow engine
* MCP integration
* Document intelligence
* AI governance / audit logging
* Plugin ecosystem

---

# Current Status

PulseStackAI is currently in active early development.

The framework is stable enough for experimentation and prototyping while core architecture and APIs continue to evolve.

---

# Design Philosophy

PulseStackAI focuses on:

* Clean Architecture
* Extensibility
* Provider abstraction
* Enterprise AI integration
* Developer productivity
* Incremental complexity

---

# License

MIT License
