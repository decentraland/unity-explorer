# Skills Index

Focused, agent-agnostic skill files for the Unity Explorer codebase. Each skill targets a distinct implementation domain with activation rules, behavioral constraints, procedural flows, and real code examples.

For full reference documentation, see [`docs/README.md`](../docs/README.md).

## Skills

| # | File | Domain | When to Use |
|---|------|--------|-------------|
| 1 | [code-standards.md](code-standards.md) | Code Standards & Conventions | Writing or reviewing any C# code — naming, formatting, ordering, tests, PRs |
| 2 | [ecs-system-and-component-design.md](ecs-system-and-component-design.md) | ECS System & Component Design | Creating or modifying ECS systems, components, queries, cleanup, singletons |
| 3 | [plugin-architecture.md](plugin-architecture.md) | Plugin & Dependency Architecture | Adding or modifying plugins, settings, containers, assembly structure |
| 4 | [sdk-component-implementation.md](sdk-component-implementation.md) | SDK Component Implementation | Implementing new SDK7 components end-to-end (protocol → C# → systems) |
| 5 | [asset-promise-lifecycle.md](asset-promise-lifecycle.md) | Asset Promise Lifecycle | Loading assets asynchronously through ECS (textures, models, audio) |
| 6 | [async-programming.md](async-programming.md) | Async Programming Patterns | Writing async code with UniTask, cancellation, exception handling |
| 7 | [mvc-and-ui-architecture.md](mvc-and-ui-architecture.md) | MVC & UI Architecture | Building UI controllers, views, window stacking, shared space, context menus |
| 8 | [web-requests.md](web-requests.md) | Web Requests | Making HTTP requests, response parsing, signing, retry policies |
| 9 | [diagnostics-and-logging.md](diagnostics-and-logging.md) | Diagnostics & Logging | Logging, error reporting, ReportHub, severity matrix, Sentry |
| 10 | [feature-flags-and-configuration.md](feature-flags-and-configuration.md) | Feature Flags & Configuration | Gating features, runtime configuration, app arguments |
