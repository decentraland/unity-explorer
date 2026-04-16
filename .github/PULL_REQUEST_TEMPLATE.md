# Pull Request Description

## What does this PR change?
<!--
Please provide a clear and detailed description of your changes. Include:
- What you're changing and why (describe the problem you're solving)
- Which issue this addresses (if applicable), using #123 format
- For optimizations: Include performance comparisons (before vs. after)
- For SDK features: Include or link to a test scene
- Links to relevant documentation:
  - Design docs
  - Architecture diagrams
  - Figma designs
  - Screenshots
  - Other relevant context
-->

## Test Instructions
<!--
Provide clear, copy-pasteable steps for testing these changes.

### Quick reference

# Run this PR (with cache)
metaforge explorer run <this-PR-number>

# Run this PR (without cache — clears Explorer data)
metaforge explorer run <this-PR-number> --clear

# Run this PR (fresh account — clears everything and creates a new account)
metaforge account create --clear
metaforge explorer run <this-PR-number>

# Tail logs while testing
metaforge explorer logs tail --filter "<relevant text>"

# Run automation tests against this PR
metaforge explorer test <this-PR-number>
-->

**Steps (standard run)**:
```bash
metaforge explorer run XXXX  # ← replace with this PR number
```

**Expected result**:
<!-- What should the reviewer see/verify? -->

**Steps (fresh account)**:
```bash
metaforge account create --clear
metaforge explorer run XXXX  # ← replace with this PR number
```

**Expected result**:
<!-- What should the reviewer see/verify? -->

**Automation** (if applicable):
```bash
metaforge explorer test XXXX
```

### Prerequisites
- [ ] List any required setup steps
- [ ] Include environment/configuration requirements

### Test Steps
1. First step
2. Second step
3. Expected result after step 2
4. ...

### Additional Testing Notes
- Note any edge cases to verify
- Mention specific areas that need careful testing
- List known limitations or potential issues

## Quality Checklist
- [ ] Changes have been tested locally
- [ ] Documentation has been updated (if required)
- [ ] Performance impact has been considered
- [ ] For SDK features: Test scene is included

## Code Review Reference
Please review our [Branch & PR Standards](../docs/branch-and-pr-standards.md) before submitting. It explains the automated review flow, QA/DEV approval requirements, and what each label does — especially useful for first-time contributors.
