# DCL Stack Trace Debugger

An orchestrated debugging system for the decentraland/unity-explorer project. Uses parallel sub-agent investigations to quickly pinpoint root causes.

## Setup

Copy the commands folder to your project:
```bash
cp -r commands/ /path/to/unity-explorer/.claude/
```

Your structure should look like:
```
unity-explorer/
├── .claude/
│   └── commands/
│       ├── debug-dcl.md          # Main orchestrator
│       ├── debug-dcl-ecs.md      # ECS component investigation
│       ├── debug-dcl-plugin.md   # Plugin/DI investigation
│       ├── debug-dcl-asset.md    # Asset loading investigation
│       ├── debug-dcl-async.md    # Async/UniTask investigation
│       ├── debug-dcl-lifecycle.md # Entity/World lifecycle
│       └── debug-dcl-adjacent.md # Adjacent code review
├── Explorer/
└── ...
```

## Usage

### Quick Start

When you have an exception, run:
```
/debug-dcl NullReferenceException: Object reference not set to an instance of an object.
   at DCL.ECS.Systems.AvatarSystem.Update() in AvatarSystem.cs:line 47
   at Arch.SystemGroups...
```

The orchestrator will:
1. Parse the exception
2. Identify the crash context (ECS, Plugin, Async, etc.)
3. Spawn parallel investigations
4. Synthesize findings into a root cause analysis

### Manual Sub-agent Invocation

You can run individual investigations directly:

**ECS Component Issues:**
```
/debug-dcl-ecs COMPONENT="AvatarComponent" ENTITY_CONTEXT="player" CRASH_FILE="AvatarSystem.cs" CRASH_LINE="47"
```

**Plugin/DI Issues:**
```
/debug-dcl-plugin SERVICE="IAvatarService" CRASH_FILE="AvatarController.cs" CRASH_LINE="23"
```

**Asset Loading Issues:**
```
/debug-dcl-asset ASSET_EXPRESSION="settings.AvatarPrefab.Value" CRASH_FILE="AvatarPlugin.cs" CRASH_LINE="31"
```

**Async/UniTask Issues:**
```
/debug-dcl-async METHOD_NAME="LoadAvatarAsync" CRASH_FILE="AvatarLoader.cs" CRASH_LINE="55"
```

**Lifecycle Issues:**
```
/debug-dcl-lifecycle ENTITY_OR_WORLD="playerEntity" CRASH_FILE="PlayerSystem.cs" CRASH_LINE="28"
```

**Adjacent Code Review:**
```
/debug-dcl-adjacent CRASH_FILE="AvatarSystem.cs" CRASH_LINE="47" CRASH_DIR="Explorer/Assets/DCL/ECS/Avatar"
```

## Investigation Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                      /debug-dcl [exception]                      │
│                         (Orchestrator)                           │
└─────────────────────────────┬───────────────────────────────────┘
                              │
                              │ Parses exception, identifies context
                              │
         ┌────────────────────┼────────────────────┐
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│   debug-dcl-    │  │   debug-dcl-    │  │   debug-dcl-    │
│      ecs        │  │     plugin      │  │     asset       │
│                 │  │                 │  │                 │
│ • Component     │  │ • DI registration│ │ • AssetRef      │
│   lifecycle     │  │ • Settings      │  │   configuration │
│ • System order  │  │ • World inject  │  │ • Load timing   │
│ • Query vs Add  │  │ • Initialization│  │ • Disposal      │
└────────┬────────┘  └────────┬────────┘  └────────┬────────┘
         │                    │                    │
         │    ┌───────────────┼───────────────┐    │
         │    │               │               │    │
         ▼    ▼               ▼               ▼    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│   debug-dcl-    │  │   debug-dcl-    │  │   debug-dcl-    │
│     async       │  │    lifecycle    │  │    adjacent     │
│                 │  │                 │  │                 │
│ • Cancellation  │  │ • Entity refs   │  │ • Similar code  │
│ • UniTask flow  │  │ • World scope   │  │ • Null patterns │
│ • Result pattern│  │ • Disposal order│  │ • TODOs/FIXMEs  │
└────────┬────────┘  └────────┬────────┘  └────────┬────────┘
         │                    │                    │
         └────────────────────┼────────────────────┘
                              │
                              ▼
                    ┌─────────────────┐
                    │   SYNTHESIS     │
                    │                 │
                    │ • Root cause    │
                    │ • Causal chain  │
                    │ • Fixes         │
                    └─────────────────┘
```

## Common Null Categories in DCL

| Category | Sub-agent | Common Causes |
|----------|-----------|---------------|
| ECS Component | debug-dcl-ecs | Component not added, wrong system order, entity destroyed |
| Plugin/DI | debug-dcl-plugin | Service not registered, settings not configured, world not injected |
| Asset Loading | debug-dcl-asset | AssetReference empty, .Value before load, cancelled load |
| Async | debug-dcl-async | CancellationToken not checked, Result not validated |
| Lifecycle | debug-dcl-lifecycle | Entity destroyed, world disposed, stale EntityReference |

## Output

The orchestrator produces a comprehensive report including:
- Summary of exception and context
- Findings from each sub-agent investigation
- Root cause analysis with causal chain
- Immediate fix (stop the crash)
- Root cause fix (prevent the null)
- Verification checklist
- Files examined

## Tips

1. **Let the orchestrator decide**: Start with `/debug-dcl` - it will spawn the right investigations
2. **Parallel is faster**: Sub-agents run concurrently, faster than sequential debugging
3. **Check Unity Inspector**: Many DCL nulls are missing asset references in PluginSettingsContainer
4. **System order matters**: Use `[UpdateAfter]` and `[UpdateBefore]` attributes
5. **Use EntityReference**: Never store raw `Entity` outside ECS
