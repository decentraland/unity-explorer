---
name: creating-skills
disable-model-invocation: true
description: Skill authoring and optimization. Use when creating new skills, editing existing skills, verifying skill quality, or optimizing trigger descriptions.
---

# Creating Skills

Skills are eval-driven process documentation. Build evaluations first, write the skill to pass them, then optimize trigger descriptions for activation accuracy.

For comprehensive Anthropic guidelines, see [best-practices-reference.md](best-practices-reference.md).

## Eval-Driven Development

### Why Evals First

A skill without evals is untested code. The eval proves the skill teaches the right thing and activates at the right time.

### The Claude A / Claude B Pattern

Use two separate agent sessions to avoid self-confirmation bias:

1. **Claude A** (author) — writes the skill content
2. **Claude B** (tester) — runs evaluation scenarios cold, without seeing the authoring process

This separation ensures the skill works for any agent, not just one primed by the authoring context.

### Minimum Evaluation Requirements

Every skill needs at least **3 evaluation scenarios** before writing content:

1. **Baseline (no skill)** — Run scenarios without the skill. Document what the agent did wrong, what rationalizations it used (verbatim), which pressures triggered violations.
2. **With-skill pass** — Run the same scenarios with the skill loaded. The agent should now comply.
3. **Adversarial/edge case** — Test boundary conditions, near-misses, and rationalization attempts.

Record concrete outputs, not impressions. Quote agent responses verbatim when documenting failures.

### Eval-Write-Eval Cycle

```
1. Design 3+ realistic evaluation scenarios
2. Run baseline WITHOUT skill (Claude B) -> document failures
3. Write minimal skill addressing those specific failures (Claude A)
4. Run same scenarios WITH skill (Claude B) -> verify compliance
5. If new failure modes appear, add counters and re-test
```

Do not add content for hypothetical cases. Every section should trace back to an observed failure.

## Skill Types

- **Technique** — Concrete method with steps (e.g., root-cause-tracing, condition-based-waiting)
- **Pattern** — Way of thinking about problems (e.g., flatten-with-flags)
- **Reference** — API docs, syntax guides, tool documentation

## Directory Structure

```
skills/
  skill-name/          # letters, numbers, hyphens only
    SKILL.md           # required — main instructions, under 500 lines
    reference.md       # optional — detailed docs, loaded on demand
    examples.md        # optional — usage examples, loaded on demand
    scripts/
      helper.py        # optional — executed via Bash, NOT loaded into context
    assets/
      template.pptx    # optional — output files (templates, icons, fonts), NOT loaded into context
```

**`assets/`** holds files used in skill output (brand logos, slide templates, boilerplate projects). Unlike references, assets are never read into context — they're copied, modified, or embedded in output.

Keep references **one level deep** from SKILL.md. Nested references (SKILL.md → advanced.md → details.md) cause Claude to partially read files. For files over 100 lines, include a table of contents at the top.

## SKILL.md Frontmatter

```yaml
---
name: skill-name-with-hyphens
description: What it does. Use when [specific triggering conditions]
---
```

**Hard constraints:**
- `name`: Max 64 chars. Lowercase letters, numbers, hyphens only. No "anthropic" or "claude".
- `description`: Max 1024 chars. Non-empty. No XML tags.
- Prefer gerund form for names: `processing-pdfs`, `analyzing-data`, `testing-code`

**Optional frontmatter fields:**
- `disable-model-invocation: true` — only user can invoke (not auto-triggered, saves context budget)
- `user-invocable: false` — only Claude can invoke (background knowledge, hidden from `/` menu)
- `allowed-tools` — restrict tools when skill is active (e.g., `Read, Grep, Glob`)
- `context: fork` — run in isolated subagent context
- `agent` — which subagent type when `context: fork` is set
- `model` — override model for this skill (e.g., `haiku` for cheap tasks)
- `effort` — `low`, `medium`, `high`, `max` (Opus 4.6 only)
- `hooks` — lifecycle hooks scoped to this skill's execution
- `paths` — glob patterns limiting when skill auto-activates (e.g., `["src/**/*.py"]`)
- `shell` — `bash` (default) or `powershell`
- `argument-hint` — shown during autocomplete (e.g., `[issue-number]`)

**Description rules:**
- **Third-person always.** Description is injected into the system prompt.
- **CRITICAL: Description = when to use ONLY.** NEVER summarize the skill's process or workflow. Testing shows that when a description summarizes workflow, Claude follows the description instead of reading the skill body.
- Include **both what the skill does AND when to use it**.
- Include 5+ keywords from actual user workflows.
- **Front-load the key use case** — descriptions are truncated at ~250 chars in skill listings.
- **Be assertive about activation** — include phrases like "Make sure to use this skill whenever the user mentions X, even if they don't explicitly ask for it."

**Context budget:** All skill descriptions share a pool of **~1% of context window (fallback ~8K chars)**. Every character in every description consumes from this shared pool. Skills with `disable-model-invocation: true` are NOT loaded into context at all. Override with `SLASH_COMMAND_TOOL_CHAR_BUDGET` env var.

## SKILL.md Body Structure

```markdown
# Skill Name

## Overview
What is this? Core principle in 1-2 sentences.

## Quick Reference
Table or bullets for scanning common operations

## Core Pattern
Before/after comparison (for techniques/patterns)

## Implementation
Inline code for simple patterns, link to file for heavy reference

## Common Mistakes
What goes wrong + fixes
```

**Default assumption: Claude is already very smart.** Only add context Claude doesn't already have. Challenge each piece: "Does Claude really need this explanation?"

**Writing style:** Use imperative/infinitive form ("Validate the input", "Configure the server"), not second person ("You should validate..."). Explain *why* behind instructions — LLMs respond better to reasoning than bare ALWAYS/NEVER directives.

## Degrees of Freedom

Match specificity to task fragility:

| Level | When to use | Example |
|-------|------------|---------|
| **High** (text instructions) | Multiple valid approaches, context-dependent | Code review guidelines |
| **Medium** (parameterized scripts) | Preferred pattern exists, some variation ok | Report generation with template |
| **Low** (exact scripts, no params) | Fragile operations, consistency critical | Database migrations, deployments |

## Token Efficiency

- **Every-session skills** (loaded via hook): absolute minimum tokens. <200 words.
- **Frequently-triggered skills**: <500 words preferred.
- **On-demand skills** (`disable-model-invocation`): more generous, but still under 500 lines.
- **Body word count target**: 1,500-2,000 words ideal, <5,000 words max.
- Move heavy reference to separate files when SKILL.md approaches 300+ lines.
- Scripts in `scripts/` are executed, not loaded — only output consumes tokens.

## Description Optimization

The description controls activation rate. Optimize methodically:

### Step 1 — Keyword Coverage

Use words Claude would search for: error messages, symptoms ("flaky", "hanging", "race condition"), tool names, synonyms. Cover the vocabulary space of likely user prompts.

### Step 2 — Build Eval Queries

Create at least 20 evaluation queries:
- 8-10 should-trigger queries (realistic user prompts with personal context, specific details)
- 8-10 should-not-trigger queries (near-misses from adjacent domains, not obviously irrelevant)

### Step 3 — Test and Iterate

1. For each query, check if the description would cause activation
2. Adjust keywords and phrasing to improve precision and recall
3. Re-test until satisfied with activation accuracy

### Activation Rate Benchmarks

- **Good:** >80% correct activation on eval set
- **Acceptable:** >70% with no false positives on critical should-not-trigger queries
- **Needs work:** <70% or any false positive on dangerous near-misses

### Step 4 — Add Concrete Examples

If keyword coverage alone is insufficient, add specific phrases from real user prompts. Concrete examples improve activation from ~50% to 72-90%.

## Workflow Patterns

### Checklist Pattern
For complex multi-step tasks, provide a copyable checklist Claude can track progress with.

### Feedback Loop Pattern
Run validator → fix errors → repeat. Greatly improves output quality for document generation, form filling, code review.

### Plan-Validate-Execute Pattern
Have Claude create an intermediate plan file → validate with script → execute. Catches errors early.

### Conditional Workflow
Guide Claude through decision points: "Creating new? → Creation workflow. Editing existing? → Editing workflow."

### Template Pattern
Provide output format templates (strict or flexible) so Claude produces consistent structured output.

### Examples Pattern
Input/output pairs work like few-shot prompting — show what good output looks like for representative inputs.

## Bulletproofing Discipline Skills

For skills that enforce rules, resistance to rationalization is critical.

**Close every loophole explicitly** — state the rule, then forbid specific workarounds.

**Build a rationalization table** from baseline testing:

```markdown
| Excuse | Reality |
|--------|---------|
| "Too simple to test" | Simple code breaks. Test takes 30 seconds. |
```

**Red flags list** — makes agent self-checking easy.

## Dynamic Features

### String Substitutions

| Variable | Description |
|----------|------------|
| `$ARGUMENTS` / `$N` | Arguments passed when invoking the skill |
| `${CLAUDE_SKILL_DIR}` | Directory containing the skill's SKILL.md |
| `${CLAUDE_SESSION_ID}` | Current session ID |

### Dynamic Context Injection

The `` !`command` `` syntax runs shell commands before skill content is sent to Claude:
```yaml
## PR context
- PR diff: !`gh pr diff`
- Changed files: !`gh pr diff --name-only`
```

## Anti-Patterns

- **Narrative** — "In session 2025-10-03, we found..." is too specific, not reusable
- **Multi-language dilution** — One great example beats mediocre examples in 5 languages
- **Generic labels** — `helper1`, `step3` have no semantic meaning
- **Skipping evals** — Writing content before observing failures produces bloated skills
- **Graphviz diagrams** — Rendered as raw text in terminals. Use numbered lists or tables instead.
- **Workflow summary in description** — Claude follows description shortcut instead of reading body
- **Vague descriptions** — "Helps with documents" achieves ~20% activation
- **Over-explaining** — Claude knows what PDFs are and how libraries work
- **Deeply nested references** — Keep one level deep from SKILL.md
- **Time-sensitive info** — "Before August 2025, use old API" becomes wrong. Use "Old patterns" section.
- **Inconsistent terminology** — Pick one term ("endpoint" not "endpoint/URL/route/path")
- **Too many options** — Provide one default with an escape hatch, not 5 choices
- **Excessive ALWAYS/NEVER caps** — Explain reasoning instead; LLMs respond better to "why" than bare directives

## Testing Across Models

What works for Opus may need more detail for Haiku. If a skill will be used across models:
- **Haiku**: May need more explicit guidance and examples
- **Sonnet**: Balanced — good default target
- **Opus**: May be over-served by verbose instructions — trim for efficiency

## Checklist

**EVAL Phase:**
- [ ] Design 3+ realistic evaluation scenarios
- [ ] Run WITHOUT skill (Claude B) — document exact failures verbatim
- [ ] Record rationalizations and failure modes

**WRITE Phase:**
- [ ] Name: lowercase, hyphens, max 64 chars, gerund form preferred
- [ ] Frontmatter max 1024 chars, description in third person
- [ ] Description: no workflow summary, includes "Use when..." + keywords
- [ ] Body under 500 lines. Heavy reference in separate files.
- [ ] Appropriate degree of freedom for the task type
- [ ] Only context Claude doesn't already have
- [ ] References one level deep, files >100 lines have TOC

**VERIFY Phase:**
- [ ] Run WITH skill (Claude B) — verify compliance on all scenarios
- [ ] Add rationalization table (if discipline skill)
- [ ] Re-test until bulletproof

**OPTIMIZE Phase:**
- [ ] Build 20+ eval queries for description
- [ ] Test activation accuracy (target >80%)
- [ ] Add concrete examples if activation <70%
- [ ] Test with Haiku, Sonnet, and Opus if cross-model use planned
