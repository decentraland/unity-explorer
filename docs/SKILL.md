---
name: docs
description: Unity Explorer project documentation — architecture, ECS patterns, development guides, and system references
---

# Unity Explorer Documentation

## Description

Comprehensive reference documentation for the Decentraland Unity Explorer client. Covers ECS architecture, development standards, SDK component implementation, asset loading, avatar rendering, chat, UI patterns, Web3 authentication, CI/CD, and more.

**Total Documentation Files:** 49
**Language:** C# (Unity 2022.3)

## Implementation Skills

Skills are automatically loaded by Claude Code from `.claude/skills/`. Each skill contains focused implementation procedures with behavioral constraints and real code examples:

| Skill | Domain |
|-------|--------|
| [code-standards](../.claude/skills/code-standards/SKILL.md) | Naming, formatting, ordering, tests, PRs |
| [ecs-system-and-component-design](../.claude/skills/ecs-system-and-component-design/SKILL.md) | Systems, components, queries, cleanup, singletons |
| [plugin-architecture](../.claude/skills/plugin-architecture/SKILL.md) | Plugins, settings, containers, assemblies |
| [sdk-component-implementation](../.claude/skills/sdk-component-implementation/SKILL.md) | New SDK7 components end-to-end |
| [asset-promise-lifecycle](../.claude/skills/asset-promise-lifecycle/SKILL.md) | Async ECS asset loading pipeline |
| [async-programming](../.claude/skills/async-programming/SKILL.md) | UniTask, cancellation, exception handling |
| [mvc-and-ui-architecture](../.claude/skills/mvc-and-ui-architecture/SKILL.md) | Controllers, views, window stacking, context menus |
| [web-requests](../.claude/skills/web-requests/SKILL.md) | HTTP requests, response parsing, signing, retries |
| [diagnostics-and-logging](../.claude/skills/diagnostics-and-logging/SKILL.md) | ReportHub, severity matrix, Sentry |
| [feature-flags-and-configuration](../.claude/skills/feature-flags-and-configuration/SKILL.md) | Feature flags, features registry, app arguments |

## When to Use This Document

Use the reference documentation below when you need:
- Deep architectural context beyond what skills cover
- Avatar rendering details (GPU skinning, compute shaders)
- Chat system internals (state machine, commands, auto-translation)
- Build and CI pipeline specifics (GitHub workflows, Unity Cloud)
- Development environment setup (SSH, GPG, LODs)
- Network synchronization details (LiveKit, CRDT bridge)

## Documentation Reference

### Getting Started

| Topic | File | Description |
|-------|------|-------------|
| Index | [README.md](README.md) | Top-level landing page linking to all documentation |
| Setup | [setup.md](setup.md) | SSH, GPG keys, LODs, and environment setup |
| Getting Started | [getting-started.md](getting-started.md) | Scene loaders and Unity project entry points |
| Explorer Packages | [working-with-explorer-packages.md](working-with-explorer-packages.md) | Managing private UPM packages via `manifest.json` |

### Contributing

| Topic | File | Description |
|-------|------|-------------|
| Branch & PR Standards | [branch-and-pr-standards.md](branch-and-pr-standards.md) | Branch naming, PR process, squash merge policy |
| Code Style Guidelines | [code-style-guidelines.md](code-style-guidelines.md) | Naming (`PascalCase`/`camelCase`/`ALL_UPPER_SNAKE_CASE`), formatting, ordering, `.editorconfig` |
| Standards | [standards.md](standards.md) | Memory, GC, performance, tests, and error handling rules |

### Architecture

| Topic | File | Description |
|-------|------|-------------|
| Architecture Overview | [architecture-overview.md](architecture-overview.md) | ECS concepts (entities, components, systems, worlds), plugins, containers, singletons, abstractions, REnum union types, exception-free async flow |
| Directories & Assemblies | [directories-and-assemblies-structure.md](directories-and-assemblies-structure.md) | Project folder structure, assembly rules, feature encapsulation |
| Systems | [systems.md](systems.md) | ECS system lifecycle, streamables, scene bounds, system groups |
| Scene Runtime | [scene-runtime.md](scene-runtime.md) | SDK7 execution via ClearScript/V8, CRDT bridge, JS modules, adaptation layer |
| MVC | [mvc.md](mvc.md) | UI architecture: controllers, views, MVC manager, window stack, subordinate controllers |
| Third-Party Libraries | [third-party-libraries.md](third-party-libraries.md) | ArchECS, ClearScript, Sentry, and other dependencies |

### Development Guides

| Topic | File | Description |
|-------|------|-------------|
| Development Guide | [development-guide.md](development-guide.md) | **Key reference** — ECS system/component design, queries, `ref var` mutation, cleanup patterns, singletons, test environments, protocol updates |
| Async Programming | [async-programming.md](async-programming.md) | Detached flows, cancellation, `SuppressToResultAsync`, exception-free `Result` pattern |
| Implement SDK Components | [how-to-implement-new-sdk-components.md](how-to-implement-new-sdk-components.md) | Step-by-step guide for adding new SDK7 components (protobuf, systems, serialization) |
| Feature Flags | [feature-flags.md](feature-flags.md) | Fetching from `feature-flags.decentraland.org`, checking `FeatureFlagsCache`, variants |
| Features Registry | [features-registry.md](features-registry.md) | Centralized `FeaturesRegistry` singleton for feature state |
| App Arguments | [app-arguments.md](app-arguments.md) | Command-line flags (`AppArgsFlags`) for launch configuration |

### Core Systems

| Topic | File | Description |
|-------|------|-------------|
| Asset Promises | [asset-promises.md](asset-promises.md) | `AssetPromise<T>` for async ECS asset loading, lifecycle, and consumption |
| Asset Bundles Conversion | [asset-bundles-conversion.md](asset-bundles-conversion.md) | GLTF to Asset Bundle pipeline, server-side and Unity-side details |
| Web Requests Framework | [web-requests-framework.md](web-requests-framework.md) | Allocation-free web requests, retry policies, error code qualification, signing |
| Memory & Resource Unloading | [memory-budgeting-and-resource-unloading.md](memory-budgeting-and-resource-unloading.md) | `MemoryBudgetProvider`, `CacheCleaner`, LRU cache unloading, profiling |
| Network Synchronization | [network-synchronization.md](network-synchronization.md) | Entity sync via LiveKit rooms (Scene vs Island), `OnUserEnter`/`OnUserLeave` |
| Diagnostics | [diagnostics.md](diagnostics.md) | `ReportHub` logging, `CategorySeverityMatrix`, Sentry integration, exception tolerance |

### Avatar System

| Topic | File | Description |
|-------|------|-------------|
| Avatar Rendering | [avatar-rendering.md](avatar-rendering.md) | GPU skinning, compute shaders, Global Vertex Buffer, `AvatarCelShading` shader |
| Emotes | [emotes.md](emotes.md) | Emote loading, Mecanim controllers from Asset Bundles, `EmotePlayer` pooling |
| Skeleton Loading Animation | [skeleton-loading-animation.md](skeleton-loading-animation.md) | `SkeletonLoadingView` MonoBehaviour for UI bone animations |
| Avatar Animation for Demos | [avatar-animation-for-demos.md](avatar-animation-for-demos.md) | Unity Timeline for demo recordings (teleport, move, emote clips) |

### Chat & Social

| Topic | File | Description |
|-------|------|-------------|
| Chat | [chat.md](chat.md) | Chat architecture (MVP, state machine, commands, services, event bus, auto-translation) |
| Chat Emojis | [chat-emojis.md](chat-emojis.md) | Emoji atlas creation with TextMesh Pro, noto-emoji font |
| Chat History Storage | [chat-history-local-storage.md](chat-history-local-storage.md) | Local encrypted chat history persistence, feature flag control |

### Features & UI

| Topic | File | Description |
|-------|------|-------------|
| Locomotion | [locomotion.md](locomotion.md) | Character movement, jumping, sprinting, IK |
| Landscape | [landscape.md](landscape.md) | Procedural terrain generation |
| Notifications | [notifications.md](notifications.md) | Remote notification polling, `INotificationsBusController`, local notifications |
| Generic Context Menu | [generic-context-menu.md](generic-context-menu.md) | Dynamic MVC context menu components (buttons, toggles, separators) |
| Quality Settings | [quality-settings.md](quality-settings.md) | Graphics quality levels, renderer configuration |
| Settings Panel | [settings-panel.md](settings-panel.md) | Modular settings panel (toggles, sliders, dropdowns, feature flag integration) |
| Shared Space Manager | [shared-space-manager.md](shared-space-manager.md) | Coordinated UI panel visibility (`IPanelInSharedSpace`) |
| Visibility Propagation | [visibility-component-propagation.md](visibility-component-propagation.md) | SDK `VisibilityComponent` inheritance and `PropagateToChildren` rules |

### Authentication & Web3

| Topic | File | Description |
|-------|------|-------------|
| Web3 & Authentication | [web3-authentication.md](web3-authentication.md) | Wallet signing, ephemeral addresses, auth chains, `IEthereumApi` |
| IPFS Realms | [ipfs-realms.md](ipfs-realms.md) | Publishing entities to IPFS catalysts |

### Testing & Debugging

| Topic | File | Description |
|-------|------|-------------|
| Connect to Local Scene | [how-to-connect-to-a-local-scene.md](how-to-connect-to-a-local-scene.md) | Running and connecting to local SDK7 scenes from Editor or builds |
| Master of Bots | [master-of-bots.md](master-of-bots.md) | Simulating multiple bot users for load testing |
| Override Debug Log Matrix | [override-debug-log-matrix.md](override-debug-log-matrix.md) | Runtime log severity overrides per category/handler |
| Performance Benchmark | [performance-benchmark.md](performance-benchmark.md) | Generating PDF benchmark reports |

### Build & CI

| Topic | File | Description |
|-------|------|-------------|
| Build & CI | [build-and-ci.md](build-and-ci.md) | GitHub workflows, Python build handler, Unity Cloud setup |
| Unity Upgrades | [unity-upgrades.md](unity-upgrades.md) | Handling Unity version upgrades and CI images |
| Troubleshooting Docker Images | [troubleshooting-missing-docker-images.md](troubleshooting-missing-docker-images.md) | Fixing missing UnityCI Docker images |
