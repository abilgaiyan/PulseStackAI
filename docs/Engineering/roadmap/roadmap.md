PulseStackAI Roadmap

══════════════════════════════════════════════
Foundation Phase
══════════════════════════════════════════════

✅ MS-001 Core Foundation

✅ MS-002 Agent Runtime

✅ MS-003 Workflow Runtime

✅ MS-004 Workflow Persistence


══════════════════════════════════════════════
Platform Phase
══════════════════════════════════════════════

MS-005 Workflow Packages

MS-006 Planner

MS-007 Human Approval

MS-008 Scheduling

MS-009 Distributed Runtime

MS-010 Workflow Registry


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