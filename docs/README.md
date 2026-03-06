# Unity Explorer

Welcome to the official documentation for Unity Explorer — the Decentraland client built on Unity. This wiki covers architecture, development standards, feature guides, and tooling.

---

## Getting Started
- **[Setup](setup.md)** — SSH, GPG, LODs, and environment setup
- **[Getting Started](getting-started.md)** — Scene loaders and Unity project entry points
- **[Working with Explorer Packages](working-with-explorer-packages.md)** — Managing private UPM packages

## Contributing
- **[Branch & PR Standards](branch-and-pr-standards.md)** — Branches, PRs, commits, and merge policy
- **[Code Style Guidelines](code-style-guidelines.md)** — Naming, formatting, and ordering conventions
- **[Standards](standards.md)** — Memory, GC, performance, tests, and error handling

## Architecture
- **[Architecture Overview](architecture-overview.md)** — ECS concepts, dependency management, containers, and abstractions
- **[Directories & Assemblies](directories-and-assemblies-structure.md)** — Project folder structure and assembly rules
- **[Systems](systems.md)** — ECS system lifecycle, streamables, and scene bounds
- **[Scene Runtime](scene-runtime.md)** — SDK7 scene execution, CRDT bridge, and JS modules
- **[MVC](mvc.md)** — UI architecture: controllers, views, and the MVC manager
- **[Third-Party Libraries](third-party-libraries.md)** — ArchECS, ClearScript, and Sentry

## Development Guides
- **[Development Guide](development-guide.md)** — ECS system/component design patterns, queries, cleanup, and singletons
- **[Async Programming](async-programming.md)** — Detached flows, cancellation, and exception-free async
- **[Implement SDK Components](how-to-implement-new-sdk-components.md)** — Step-by-step guide for new SDK7 components
- **[Feature Flags](feature-flags.md)** — Fetching and checking feature flags at runtime
- **[Features Registry](features-registry.md)** — Centralized feature state registry
- **[App Arguments](app-arguments.md)** — Command-line flags and launch parameters

## Core Systems
- **[Asset Promises](asset-promises.md)** — Asynchronous asset loading with ECS promises
- **[Asset Bundles Conversion](asset-bundles-conversion.md)** — GLTF to Asset Bundle conversion pipeline
- **[Web Requests Framework](web-requests-framework.md)** — Allocation-free web requests with retry policies
- **[Memory & Resource Unloading](memory-budgeting-and-resource-unloading.md)** — Memory budgeting and cache unloading strategies
- **[Network Synchronization](network-synchronization.md)** — Entity sync via LiveKit in worlds and Genesis City
- **[Diagnostics](diagnostics.md)** — ReportHub logging system and Sentry integration

## Avatar System
- **[Avatar Rendering](avatar-rendering.md)** — GPU skinning, compute shaders, and cel-shading
- **[Emotes](emotes.md)** — Emote loading, animation controllers, and pooling
- **[Skeleton Loading Animation](skeleton-loading-animation.md)** — Bone animations for loading states
- **[Avatar Animation for Demos](avatar-animation-for-demos.md)** — Using Unity Timeline for demo recordings

## Chat & Social
- **[Chat](chat.md)** — Chat system architecture, state machine, and MVP structure
- **[Chat Emojis](chat-emojis.md)** — Emoji atlas creation and TextMesh Pro integration
- **[Chat History Storage](chat-history-local-storage.md)** — Local encrypted chat history persistence

## Features & UI
- **[Locomotion](locomotion.md)** — Character movement, jumping, sprinting, and IK
- **[Landscape](landscape.md)** — Procedural terrain generation
- **[Notifications](notifications.md)** — Remote notification polling and display
- **[Generic Context Menu](generic-context-menu.md)** — Dynamic context menu components
- **[Quality Settings](quality-settings.md)** — Graphics quality levels and renderer configuration
- **[Settings Panel](settings-panel.md)** — Modular settings panel architecture
- **[Shared Space Manager](shared-space-manager.md)** — Coordinated UI panel visibility management
- **[Visibility Component Propagation](visibility-component-propagation.md)** — SDK visibility component inheritance rules

## Authentication & Web3
- **[Web3 & Authentication](web3-authentication.md)** — Wallet signing, ephemeral addresses, and auth chains
- **[IPFS Realms](ipfs-realms.md)** — Publishing entities to IPFS catalysts

## Testing & Debugging
- **[Connect to Local Scene](how-to-connect-to-a-local-scene.md)** — Running and connecting to local SDK7 scenes
- **[Master of Bots](master-of-bots.md)** — Simulating multiple bot users for load testing
- **[Override Debug Log Matrix](override-debug-log-matrix.md)** — Runtime log severity overrides
- **[Performance Benchmark](performance-benchmark.md)** — Generating PDF benchmark reports

## Build & CI
- **[Build & CI](build-and-ci.md)** — GitHub workflows, Python build handler, and Unity Cloud
- **[Unity Upgrades](unity-upgrades.md)** — Handling Unity version upgrades and CI images
- **[Troubleshooting Missing Docker Images](troubleshooting-missing-docker-images.md)** — Fixing missing UnityCI Docker images
