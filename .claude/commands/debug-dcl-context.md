# Investigate Context - Sub-agent

You are mapping the code architecture related to a problem in decentraland/unity-explorer.

## Parameters
- FEATURE: The feature or area to investigate
- KEYWORDS: Search terms related to the problem

## Tasks

### 1. Find Related Files
```bash
rg "{{KEYWORDS}}" -n Explorer/Assets --type cs -l | head -30
```
List all files that mention the feature/keywords.

### 2. Identify Core Files
```bash
# Find main classes/systems for this feature
rg "class.*{{FEATURE}}|interface.*I{{FEATURE}}" -n Explorer/Assets --type cs -A 5
```

### 3. Map Dependencies
```bash
# Find what this feature depends on
rg "using|private.*readonly|constructor" -n [CORE_FILES] --type cs | grep -v "System\."
```

### 4. Find Entry Points
```bash
# Find where this feature is triggered
rg "{{FEATURE}}" -n Explorer/Assets --type cs -B 3 -A 3 | grep -E "public|void|async|Update"
```

### 5. Check Plugin Registration
```bash
rg "{{FEATURE}}.*Plugin|Plugin.*{{FEATURE}}" -n Explorer/Assets --type cs
```

### 6. Find Related Systems
```bash
rg "{{FEATURE}}.*System|System.*{{FEATURE}}" -n Explorer/Assets --type cs
```

### 7. Find Controllers
```bash
rg "{{FEATURE}}.*Controller|Controller.*{{FEATURE}}" -n Explorer/Assets --type cs
```

### 8. Map Data Flow
```bash
# Find components/data structures
rg "{{FEATURE}}.*Component|{{FEATURE}}.*Data|{{FEATURE}}.*State" -n Explorer/Assets --type cs
```

## Output Format

```markdown
## Context: {{FEATURE}}

### File Map
| File | Type | Purpose |
|------|------|---------|
| [file] | [System/Plugin/Controller/Component] | [what it does] |

### Architecture
```
[Plugin/Entry Point]
    ↓
[Controller/Manager]
    ↓
[System(s)]
    ↓
[Component(s)/Data]
```

### Key Classes
- **Main Class**: [name] - [purpose]
- **Dependencies**: [list]
- **Consumers**: [who uses this]

### Entry Points
| Trigger | Location | Description |
|---------|----------|-------------|
| [event/call] | [file:line] | [when this happens] |

### Data Flow
```
[Input] → [Processing] → [Output]
```

### Related Features
- [Other features that interact with this one]

### Files to Focus On
1. [Most relevant file] - [why]
2. [Second most relevant] - [why]
```

## Execute

Map the code architecture for `{{FEATURE}}` using keywords: {{KEYWORDS}}
