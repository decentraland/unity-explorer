# Decentraland ParticleSystem Component — Multi-Agent Spec → Plan → Tasks → Execution Workflow

This document defines the workflow for implementing a **ParticleSystem component** across multiple Decentraland repositories using an AI code assistant.

The assistant must operate as a **Lead Architect Agent** coordinating several **specialized sub-agents**.

The workflow consists of six phases:

1. Spec Understanding
2. Architecture Planning
3. Task Breakdown
4. Sub-Agent Delegation
5. Execution Loop
6. Self-Correction Verification Loop

The assistant **must not skip phases**.

---

# Workspace Context

Add the following directories to the context/workspace so that they can be read and written (as if `/add-dir` was used):


/Users/pravus/git/protocol
/Users/pravus/git/js-sdk-toolchain
/Users/pravus/git/sdk7-test-scenes


Repositories represent:

| Repo | Responsibility |
|-----|-----|
| protocol | protobuf definitions |
| js-sdk-toolchain | Decentraland SDK implementation |
| sdk7-test-scenes | scenes used for testing SDK components |
| explorer (Unity) | runtime implementation of components |

---

# Agent Skills

All agents must **load and use the reusable skills located at:**


.claude/skills/


Agents should consult these skills whenever performing tasks such as:[settings.json](..%2F..%2F.claude%2Fsettings.json)

- code editing
- repository navigation
- dependency management
- testing
- build verification
- cross-repo integration

Skills should be reused whenever applicable instead of inventing new procedures.

---

# Feature Specification

Implement a **ParticleSystem component** for Decentraland.

The implementation spans four layers:

1. Protocol layer (protobuf definitions)
2. SDK layer (TypeScript component implementation)
3. Test scene demonstrating the component
4. Explorer runtime implementation (Unity)

---

# ParticleSystem Component Requirements

The component must expose at minimum the following properties.

## Emission

- `rate` (particles per second)

---

## Particle Lifetime

- `lifetime`

---

## Size

- `initialSize`
- `sizeOverTime`

---

## Rotation

- `initialRotation`
- `rotationOverTime`

---

## Color

- `initialColor`
- `colorOverTime`

---

## Emitter Shape

Shapes should map cleanly to Unity:

- point
- sphere
- cone
- plane

Ignore custom mesh emitters.

---

## Motion

- `initialVelocitySpeed`
- `gravity` or `additionalForceVector`

---

## Rendering

- `texture`
- `blendMode`
- `billboard` (boolean)

---

## Sprite Sheet Animation

Define properties equivalent to Unity ParticleSystem sprite sheet animation settings.

---

## Over-Time Properties

For the following properties:

- size
- rotation
- color

Use **Start + End values only**.

Example concept:


startValue
endValue


No curves are required.

---

## Lifecycle Controls

The component must support lifecycle/playback controls:

- play
- pause
- stop
- restart

The exact structure may be proposed by the assistant.

---

# Protocol Constraints

When modifying protobuf:

- All new messages must live inside **PBParticleSystem**
- Use **decentraland.common protobuf types whenever possible**
- Avoid unnecessary custom messages
- Ensure compatibility with Unity ParticleSystem settings

---

# SDK Constraints

The SDK work must include:

- component definition
- helper methods
- optional extension helpers to reduce boilerplate in scene code
- test coverage
- local build support

Important constraint:

**Do NOT include `make update-snapshots` in the plan.**

This will be executed manually later.

---

# Test Scene Requirements

Inside `sdk7-test-scenes`, create a scene demonstrating:

- multiple particle systems
- different emitter shapes
- color transitions
- size transitions
- sprite sheet animation
- gravity / force
- lifecycle controls

The scene must install the **locally built SDK package** rather than a published one.

---

# Explorer Implementation Requirements

The Explorer must implement runtime support for the component.

---

## Instantiation

The system should:

- create a **GameObject child** under the Entity transform
- attach a **Unity ParticleSystem**

Later this will be replaced with prefab injection.

---

## Prefab Support

The plugin must define:


IDCLPluginSettings


with a **Prefab reference**.

Implementation requirements:

- include code to use the prefab
- comment it out
- instantiate a GameObject manually for now

---

## Runtime Updates

The system must support:

- runtime updates to component properties
- reacting to component changes

---

## System Constraints

Follow patterns used by existing Explorer systems.

Requirements:

- avoid creating a new **SystemGroup unless strictly necessary**
- use **pooling whenever possible**
- particle systems must run **only when the scene is current**

---

## Lifecycle Handling

The system must handle:

- component instantiation
- component updates
- component removal
- entity destruction
- scene enter / exit

Use patterns such as:

- `ISceneIsCurrentListener`
- `IWorldSceneListener`

Also implement:


IFinalizeWorldSystem


---

## Explorer Tests

Use:

- **Editor tests**
- **ASMREF where possible**

Only create a new **ASMDEF if strictly necessary**.

---

## Verification

Do **NOT include steps about connecting Explorer to the test scene**.

Runtime verification will be done manually later.

---

# Local Package Linking Rules

When connecting packages between repositories (for example protocol → SDK):

**Always use local relative paths.**

Do not use:

- published package versions
- registry installs
- absolute paths

Example concept:


../protocol


Relative local paths must be used so changes can be tested without publishing packages.

---~~~~

# Sub-Agent Roles

The Lead Architect Agent may delegate work to the following specialized agents.

---

## Protocol Agent

Responsible for:

- protobuf schema design
- message compatibility
- reuse of decentraland.common types
- clean mapping to Unity ParticleSystem

This agent should only modify files in:


/Users/pravus/git/protocol


---

## SDK Agent

Responsible for:

- TypeScript component implementation
- SDK helper methods
- developer ergonomics
- SDK tests
- local build compatibility

This agent should only modify files in:


/Users/pravus/git/js-sdk-toolchain


---

## Scene Agent

Responsible for:

- creating test scenes
- showcasing component usage
- installing locally built SDK versions

This agent should only modify files in:


/Users/pravus/git/sdk7-test-scenes


---

## Explorer Agent

Responsible for:

- Unity runtime implementation
- ECS integration
- component lifecycle management
- pooling
- runtime updates
- Editor tests

This agent operates in the **Explorer repository**.

---

# Phase 1 — Spec Understanding

Analyze the specification and output:

- feature goal
- repositories involved
- technical challenges
- possible design decisions

Do **not produce a plan yet**.

---

# Phase 2 — Architecture Plan

Produce a comprehensive plan structured as:


Architecture Overview

Protocol Changes

SDK Implementation

Test Scene Implementation

Explorer Implementation

Local Package Linking Strategy

Testing Strategy

Parallel Workstreams

Risks / Design Decisions


Requirements:

- ordered steps
- file locations where applicable
- dependencies between tasks
- identification of parallel workstreams

Do **not write code**.

---

# Phase 3 — Task Breakdown

Convert the plan into **atomic tasks**.

Organize tasks by agent:


Protocol Agent Tasks
SDK Agent Tasks
Scene Agent Tasks
Explorer Agent Tasks


Each task must include:

- Task ID
- Responsible agent
- Objective
- Files likely to change
- Expected outcome

---

# Phase 4 — Sub-Agent Delegation

The Lead Architect Agent must assign tasks to the appropriate sub-agent.

Delegation rules:

- Each task must belong to **exactly one agent**
- Agents should **not modify other repositories**
- Cross-repo dependencies must be explicitly declared

Example:


Task P1 — Protocol Agent
Add PBParticleSystem protobuf message

Task S3 — SDK Agent
Implement ParticleSystem component wrapper


---

# Phase 5 — Execution Loop

For implementation, follow this cycle:

1. Select the next unfinished task
2. Activate the responsible sub-agent
3. Implement the task
4. Verify changes
5. Update task status

Each cycle must output:


Task ID
Responsible Agent
Task Objective
Implementation Plan
Code Changes
Verification
Task Status


Only **one task may be executed per cycle**.

---

# Phase 6 — Self-Correction Verification Loop

After each task, perform a verification pass.

The assistant must check:

### Repository Scope

- Only the intended repository was modified
- No unrelated files changed

---

### Cross-Layer Compatibility

Verify:

- protobuf messages align with SDK types
- SDK serialization matches protocol
- Explorer runtime matches SDK component structure

---

### API Consistency

Check:

- naming conventions match Decentraland patterns
- property names remain consistent across layers
- lifecycle controls behave consistently

---

### Runtime Behavior

Evaluate whether:

- the component can be instantiated
- runtime updates are supported
- lifecycle behavior is correct

---

### Test Coverage

Confirm:

- relevant tests exist
- tests follow repository testing patterns

---

If issues are detected:

1. Identify the problem
2. Propose a correction
3. Apply the fix
4. Re-verify the task

---

# Parallelization Strategy

Once **Protocol changes are implemented**, the following agents may work **in parallel**:

- SDK Agent
- Explorer Agent
- Scene Agent

The Lead Architect Agent should identify opportunities for parallel execution.

---

# Implementation Rules

The assistant must:

- avoid unnecessary refactors
- preserve compatibility across protocol, SDK, and Explorer
- follow Decentraland architectural patterns
- prefer minimal, safe changes
- maintain long-term maintainability

---

# Optional Improvements

The assistant may suggest **small improvements to the ParticleSystem API design**, provided they remain compatible with this specification.