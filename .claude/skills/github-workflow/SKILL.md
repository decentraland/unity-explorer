---
name: github-workflow
description: GitHub workflow — creating PRs, issues, and bug reports using gh CLI. Use when creating pull requests, opening issues (bugs, feature requests, tech debt, performance), or interacting with GitHub via gh commands.
allowed-tools: Bash(gh *), Read, Glob, Grep
---

# GitHub Workflow

When interacting with GitHub via `gh`, **always** use this project's templates. Templates live in `.github/`.

## Creating Pull Requests

Read `.github/PULL_REQUEST_TEMPLATE.md` before creating any PR. The PR body **must** follow that template's structure:

1. **What does this PR change?** — Describe the problem and solution, link issues with `#123`, include perf comparisons or test scenes if applicable.
2. **Test Instructions** — Prerequisites checklist + numbered test steps + additional testing notes. Write these for someone without your technical context.
3. **Quality Checklist** — Fill in all applicable items.

Example:
```
gh pr create --title "fix: resolve avatar flicker on teleport" --body "$(cat <<'EOF'
# Pull Request Description

## What does this PR change?
Fixes avatar mesh flickering when teleporting between parcels. The issue was caused by...
Closes #1234

## Test Instructions

### Prerequisites
- [ ] Use a build with GPU skinning enabled

### Test Steps
1. Enter world at 0,0
2. Teleport to 10,10
3. Observe avatar renders without flicker

### Additional Testing Notes
- Test on both Mac (M1) and Windows
- Verify with multiple avatars in view

## Quality Checklist
- [x] Changes have been tested locally
- [ ] Documentation has been updated (if required)
- [x] Performance impact has been considered
- [ ] For SDK features: Test scene is included

EOF
)"
```

## Creating Issues

Before creating any issue, read the templates in `.github/ISSUE_TEMPLATE/` to determine which template to use:

### Bug Reports — `.github/ISSUE_TEMPLATE/bug_report.md`
Use for bugs. Must include: build version, description, STR (steps to reproduce), expected vs actual result, reproduction rate, and OS.

Title format: `[QA] (Section) | (Title)`

```
gh issue create --label "bug,new,need QA validation,qa-team" \
  --title "[QA] Avatar | Mesh flickers on teleport" \
  --body "$(cat <<'EOF'
### Build version:
v1.2.3

### Issue Description:
Avatar mesh flickers briefly when teleporting

### STR:
1. Enter world
2. Teleport to another parcel
3. Observe avatar

### Expected Result:
Avatar renders smoothly after teleport

### Actual Result with evidence:
Avatar flickers for ~200ms (screenshot attached)

### Reproduction:
Always - 100% of teleports

### Operative system and additional Notes:
macOS 14.0, Apple M1
EOF
)"
```

### Feature Requests — `.github/ISSUE_TEMPLATE/feature_request.md`
Use for suggestions. Must include: problem description, proposed solution, alternatives considered, additional context.

### Tech Debt — `.github/ISSUE_TEMPLATE/technical_debt.md`
Use for code quality / maintainability issues.

Title format: `[TECH DEBT] Area | Brief Description`

Must include: priority level, area/component, description, current state, proposed solution, impact assessment, effort estimate.

### Performance Issues — `.github/ISSUE_TEMPLATE/tech-debt.md`
Use for performance degradation or optimization opportunities.

Title format: `[PERF] Component | Brief Description`

Must include: version/environment, impact level, affected metrics, profiling data, reproduction steps.

## General Rules

- **Never create a PR or issue without reading the matching template first.**
- **Fill in every section** of the template. Use `N/A` only if a section is truly not applicable.
- **BLOCKING: When the user provides partial info, DO NOT create the issue or PR. Instead, list every missing template field and ask the user to provide them.** Only proceed once all required fields are filled.
- Use `gh` CLI for all GitHub operations — never construct URLs manually.
