> **Document Type:** Language Specification
> **Audience:** Contributors
> **Status:** Draft
> **Owner:** PulseStackAI Team
> **Last Reviewed:** 2026-08-03

# Tool Language Specification

> **A Tool defines what capability is available, not when or how it should be invoked.**

---

# 1. Vision

The Tool Language defines the vocabulary used to describe reusable capabilities within PulseStackAI.

Rather than treating tools as provider-specific function calls or executable implementations, the Tool Language models tools as reusable engineering assets.

A Tool represents a business capability.

The Runtime is responsible for discovering, authorizing, invoking, and coordinating tool execution.

The Tool Language therefore remains independent of:

- AI providers
- Runtime execution
- Infrastructure technologies
- Implementation details

This enables Tools to remain reusable, portable, composable, and versioned across different AI platforms.

---

# 2. What is a Tool?

A Tool is a reusable AI Asset that describes an external capability available to an AI application.

A Tool defines **what capability exists**, not how it is implemented or executed.

A Tool describes:

- what capability is available
- what inputs are required
- what outputs are produced
- what contract governs the interaction

A Tool never describes:

- when it should be executed
- who should execute it
- how execution should occur

---

# 3. Purpose

The purpose of a Tool is to expose reusable capabilities that extend AI applications beyond language generation.

Instead of embedding business operations directly into prompts or workflows, developers create reusable Tool Assets that represent well-defined business capabilities.

Examples include:

- Search Customer
- Lookup Invoice
- Generate Report
- Send Email
- Create Purchase Order
- Read Document
- Generate Image

These capabilities can then be composed by Agents and Workflows.

---

# 4. Vocabulary

The Tool Language defines the following core vocabulary.

| Concept | Description |
|----------|-------------|
| **Capability** | The business function provided by the Tool. |
| **Contract** | The formal definition of the Tool interface. |
| **Input** | Information required by the Tool. |
| **Output** | Information produced by the Tool. |
| **Authorization** | Permissions required to use the Tool. |
| **Category** | Logical grouping of similar Tools. |
| **Description** | Human-readable explanation of the Tool capability. |

These concepts define the Tool Language independently of any runtime implementation.

---

# 5. Responsibilities

A Tool is responsible for:

- describing a reusable capability
- defining its public contract
- describing required inputs
- describing expected outputs
- remaining reusable across applications
- remaining independent of implementation

A Tool is not responsible for execution.

---

# 6. What a Tool is NOT

A Tool intentionally remains independent of runtime execution.

The following concepts do **not** belong to the Tool Language:

- Tool Invocation
- Tool Execution
- Tool Parameters
- Tool Response
- Retry Policies
- Timeout
- Circuit Breakers
- Parallel Execution
- Scheduling
- Provider Integration
- Observability
- Cost Tracking

These concerns belong to the Runtime.

Likewise, concepts such as:

- Tool Orchestration
- Tool Chains
- Multi-tool Coordination

belong to the Agent or Workflow Language rather than the Tool Language.

---

# 7. Tool Composition

A Tool may be described using multiple reusable language elements.

```
Tool

├── Capability

├── Contract

├── Input

├── Output

├── Authorization

├── Category

└── Description
```

Each element contributes to the Tool definition while remaining independent of execution.

---

# 8. Configuration Boundary

Tools describe **what** capability is available.

Configuration describes **how** that capability is implemented.

Examples of configuration include:

- Local Implementation
- REST API
- Database
- ERP System
- Microsoft Graph
- Azure Service
- MCP Server
- Authentication
- Connection Strings
- Endpoint Configuration

Configuration may vary without requiring changes to the Tool itself.

---

# 9. Runtime Boundary

The Runtime is responsible for realizing the Tool.

Its responsibilities include:

- discovering Tools
- validating contracts
- authorizing access
- resolving implementations
- invoking execution
- collecting results
- handling failures
- tracking usage
- recording observability

The Runtime executes the Tool.

The Tool never executes itself.

---

# 10. Examples

## Customer Lookup

```
Capability
Lookup Customer

Input
Customer Number

Output
Customer Profile

Authorization
Customer.Read

Category
ERP
```

---

## Invoice Search

```
Capability
Search Invoice

Input
Invoice Number

Output
Invoice Details

Authorization
Finance.Read

Category
Finance
```

---

## Web Search

```
Capability
Search the Web

Input
Search Query

Output
Search Results

Authorization
Public

Category
Research
```

---

# Summary

The Tool Language defines a provider-independent vocabulary for expressing reusable business capabilities.

It separates capability definition from implementation and execution, allowing Tools to remain portable, reusable, versioned, and composable.

Tools answer one fundamental question:

> **What capability is available?**

Configuration determines how that capability is implemented.

The Runtime determines when and how that capability is executed.