PulseStackAI Roadmap

══════════════════════════════════════════════
Foundation Phase
══════════════════════════════════════════════

✅ MS-001 Core Foundation

✅ MS-002 Agent Runtime

✅ MS-003 Workflow Runtime

✅ MS-004 Workflow Persistence

✅ MS-005 Workflow Packages


══════════════════════════════════════════════
Platform Phase
══════════════════════════════════════════════

MS-006 — AI Asset Model & Application Language

MS-007 Planner

MS-008 Human Approval

MS-009 Scheduling

MS-010 Distributed Runtime

MS-011 Workflow Registry


══════════════════════════════════════════════
Documentation Phase
══════════════════════════════════════════════

MS-DOC-001 Architecture Documentation

MS-DOC-002 Developer Guide

MS-DOC-003 Public API Guide


══════════════════════════════════════════════
Infrastructure Phase
══════════════════════════════════════════════

MS-INFRA-001 CI/CD

MS-INFRA-002 Benchmark Suite

MS-INFRA-003 Packaging & Release


══════════════════════════════════════════════
Ecosystem Phase
══════════════════════════════════════════════

MS-ECO-001 Official Workflow Packages

MS-ECO-002 Samples Library

MS-ECO-003 Project Templates

MS-ECO-004 Visual Designer

MS-ECO-005 Marketplace



Future Architectural Enhancement

Reference Resolution Layer

Status:
Planned

Description:
Introduce a resolver layer responsible for reconstructing runtime objects
from persisted workflow references.

Initial Components

• IAgentResolver
• IToolResolver
• IPromptResolver
• IWorkflowResolver

Goals

• Environment-independent workflow documents
• Portable workflow packages
• Dependency Injection integration
• Runtime composition
• Reference validation

### Future Design Candidate: Reference Resolution Layer

The current persistence model stores runtime component identifiers (for example, AgentId) rather than executable objects.

A future Resolver Layer will be introduced to reconstruct runtime objects from these references during workflow loading.

This capability is expected to support:

- Workflow Packages
- Shared Agent Libraries
- Prompt Libraries
- Tool Catalogs
- Runtime Composition
- Environment-specific registrations

This design will be addressed as part of the Workflow Packages milestone.

MS-006 transformed PulseStackAI from an orchestration framework into a language-driven AI Application Engineering Platform by establishing its Vision, Philosophy, Engineering Principles, Application Language, and AI Asset Model.