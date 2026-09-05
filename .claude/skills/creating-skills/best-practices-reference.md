# Anthropic Skill Authoring Best Practices — Reference

Comprehensive reference extracted from Anthropic's official documentation. Load this file when you need detailed guidance beyond what SKILL.md covers.

## Contents

- [Sources](#sources)
- [Discovery Mechanics](#discovery-mechanics)
- [Progressive Disclosure — Deep Dive](#progressive-disclosure--deep-dive)
- [Invocation Control — Full Matrix](#invocation-control--full-matrix)
- [Description Writing — Detailed Examples](#description-writing--detailed-examples)
- [Conciseness Guidelines](#conciseness-guidelines)
- [Workflow Patterns — Detailed](#workflow-patterns--detailed)
- [Script Guidelines](#script-guidelines)
- [Iterative Development with Claude](#iterative-development-with-claude)
- [Skill Locations and Priority](#skill-locations-and-priority)
- [Quality Checklist (Anthropic Official)](#quality-checklist-anthropic-official)

---

## Sources

- [Skill Authoring Best Practices](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices)
- [Skills Overview](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/overview)
- [Claude Code Skills Docs](https://code.claude.com/docs/en/skills)
- [Equipping Agents for the Real World](https://claude.com/blog/equipping-agents-for-the-real-world-with-agent-skills)
- [Improving Skill Creator](https://claude.com/blog/improving-skill-creator-test-measure-and-refine-agent-skills)
- [Agent Skills Open Standard](https://agentskills.io/home) — cross-platform (Claude Code, Cursor, VS Code Copilot, Gemini CLI)

---

## Discovery Mechanics

There is no algorithmic routing — pure LLM reasoning via transformer forward pass. All skill metadata (name + description) is formatted into `<available_skills>` tags within the Skill tool's description in the `tools` array of the API request.

At startup, only metadata (~100 tokens per skill) is loaded. Full SKILL.md loads only when Claude selects the skill. Additional bundled files load only on demand.

### Context Budget

- Total character budget for all skill descriptions: **~1% of context window, fallback ~8,000 characters**
- Override with `SLASH_COMMAND_TOOL_CHAR_BUDGET` environment variable
- Run `/context` to check for warnings about excluded skills
- Skills with `disable-model-invocation: true` are NOT loaded into context at all
- **Descriptions truncated at ~250 chars** in skill listings — front-load the key use case

### Activation Rate Benchmarks (from real-world testing, 200+ prompts)

| Strategy | Activation Rate | Effort |
|----------|----------------|--------|
| No optimization | ~20% | None |
| Optimized description with "USE WHEN" | ~50% | Low |
| Adding concrete examples | 72-90% | Medium |
| LLM pre-evaluation hook | ~80% | High |
| Forced evaluation hook | ~84% | High |

### Three-Tier Activation Strategy

1. **Level 1**: Description optimization (50% success, low effort)
2. **Level 2**: CLAUDE.md references mentioning the skill (60-70%, medium effort)
3. **Level 3**: Custom evaluation hooks (84%, high effort)

---

## Progressive Disclosure — Deep Dive

### Three Tiers

1. **Tier 1 (Metadata)**: Name + description loaded at startup (~100 tokens)
2. **Tier 2 (Core Content)**: Full SKILL.md loaded when skill is triggered
3. **Tier 3+ (Details)**: Additional bundled files loaded only when needed

### Pattern 1 — High-level guide with references

```markdown
# PDF Processing

## Quick start
[inline code example]

## Advanced features
- Form filling: See [FORMS.md](FORMS.md)
- API reference: See [REFERENCE.md](REFERENCE.md)
```

Claude loads FORMS.md or REFERENCE.md only when needed.

### Pattern 2 — Domain-specific organization

For skills with multiple domains, organize by domain:

```
bigquery-skill/
  SKILL.md (overview and navigation)
  reference/
    finance.md (revenue, billing)
    sales.md (pipeline, accounts)
    product.md (API usage, features)
```

When user asks about revenue, Claude only reads `reference/finance.md`.

### Pattern 3 — Conditional details

```markdown
## Creating documents
Use docx-js. See [DOCX-JS.md](DOCX-JS.md).

## Editing documents
For simple edits, modify XML directly.
**For tracked changes**: See [REDLINING.md](REDLINING.md)
```

---

## Invocation Control — Full Matrix

| Frontmatter | User can invoke | Claude can invoke | Context loading |
|---|---|---|---|
| (default) | Yes | Yes | Description always in context |
| `disable-model-invocation: true` | Yes | No | Description NOT in context |
| `user-invocable: false` | No | Yes | Description always in context |

### Additional frontmatter fields

| Field | Description |
|---|---|
| `model` | Override model for this skill (e.g., `haiku` for cheap tasks) |
| `effort` | `low`, `medium`, `high`, `max` (Opus 4.6 only) |
| `hooks` | Lifecycle hooks scoped to this skill's execution |
| `paths` | Glob patterns limiting auto-activation (e.g., `["src/**/*.py"]`) |
| `shell` | `bash` (default) or `powershell` |
| `argument-hint` | Shown during autocomplete (e.g., `[issue-number]`) |

### When to use each invocation mode

- **`disable-model-invocation: true`**: Skills with side effects (deploy, commit, send messages) or rarely used skills (saves context budget)
- **`user-invocable: false`**: Background knowledge that is not actionable as a command (e.g., `legacy-system-context`)
- **Default**: Most skills — Claude triggers them automatically when relevant

### Permission control

```text
# Allow specific skills
Skill(commit)
Skill(review-pr *)

# Deny specific skills
Skill(deploy *)
```

---

## Description Writing — Detailed Examples

### Good descriptions (specific, third person, trigger-focused)

```yaml
# PDF Processing
description: Extract text and tables from PDF files, fill forms, merge documents. Use when working with PDF files or when the user mentions PDFs, forms, or document extraction.

# Git Commit Helper
description: Generate descriptive commit messages by analyzing git diffs. Use when the user asks for help writing commit messages or reviewing staged changes.

# Excel Analysis
description: Analyze Excel spreadsheets, create pivot tables, generate charts. Use when analyzing Excel files, spreadsheets, tabular data, or .xlsx files.
```

### Bad descriptions (vague, wrong person, workflow summary)

```yaml
# Too vague
description: Helps with documents

# First person
description: I can help you process Excel files

# Second person
description: You can use this to process Excel files

# Workflow summary (CRITICAL VIOLATION)
description: Use when executing plans - dispatches subagent per task with code review between tasks
```

### Naming conventions

Prefer **gerund form** (verb + -ing):
- `processing-pdfs`, `analyzing-spreadsheets`, `managing-databases`

Acceptable alternatives:
- Noun phrases: `pdf-processing`, `spreadsheet-analysis`
- Action-oriented: `process-pdfs`, `analyze-spreadsheets`

Avoid: `helper`, `utils`, `tools`, `documents`, `data`, `files`

---

## Conciseness Guidelines

**Default assumption: Claude is already very smart.** Only add context Claude doesn't already have.

**Good (concise, ~50 tokens):**
```markdown
## Extract PDF text
Use pdfplumber for text extraction:
\```python
import pdfplumber
with pdfplumber.open("file.pdf") as pdf:
    text = pdf.pages[0].extract_text()
\```
```

**Bad (verbose, ~150 tokens):**
```markdown
## Extract PDF text
PDF (Portable Document Format) files are a common file format that contains
text, images, and other content. To extract text from a PDF, you'll need to
use a library. There are many libraries available...
```

---

## Workflow Patterns — Detailed

### Checklist pattern

```markdown
Copy this checklist and track your progress:

Task Progress:
- [ ] Step 1: Analyze the form (run analyze_form.py)
- [ ] Step 2: Create field mapping (edit fields.json)
- [ ] Step 3: Validate mapping (run validate_fields.py)
- [ ] Step 4: Fill the form (run fill_form.py)
- [ ] Step 5: Verify output (run verify_output.py)
```

### Feedback loop pattern

```markdown
1. Make edits to document.xml
2. Validate: python scripts/validate.py unpacked_dir/
3. If validation fails: fix issues, run validation again
4. Only proceed when validation passes
5. Rebuild: python scripts/pack.py unpacked_dir/ output.docx
```

### Plan-validate-execute pattern

For batch operations or destructive changes:
1. Claude creates intermediate `changes.json`
2. Script validates the plan: field names exist, no conflicts, no missing required fields
3. Only after validation passes, apply changes
4. Verify output

**Benefits:** catches errors early, machine-verifiable, reversible planning, clear debugging.

### Conditional workflow

```markdown
1. Determine the modification type:
   **Creating new content?** -> Follow "Creation workflow" below
   **Editing existing content?** -> Follow "Editing workflow" below
```

---

## Script Guidelines

### Handle errors explicitly

```python
def process_file(path):
    try:
        with open(path) as f:
            return f.read()
    except FileNotFoundError:
        print(f"File {path} not found, creating default")
        with open(path, "w") as f:
            f.write("")
        return ""
```

### Document magic numbers

```python
# HTTP requests typically complete within 30 seconds
REQUEST_TIMEOUT = 30

# Three retries balances reliability vs speed
MAX_RETRIES = 3
```

### Make execution intent clear

- "Run `analyze_form.py` to extract fields" (execute)
- "See `analyze_form.py` for the extraction algorithm" (read as reference)

---

## Iterative Development with Claude

### Creating a new skill

1. Complete a task without a skill — notice what context you repeatedly provide
2. Identify the reusable pattern
3. Ask Claude A to create a skill capturing the pattern
4. Review for conciseness — remove explanations Claude doesn't need
5. Ask Claude A to improve information architecture (separate reference files)
6. Test with Claude B on related use cases
7. Iterate based on observation

### Iterating on existing skills

1. Use the skill in real workflows with Claude B
2. Observe behavior — where does it struggle, succeed, or make unexpected choices?
3. Return to Claude A with specifics: "Claude B forgot to filter test accounts when..."
4. Review Claude A's suggestions
5. Apply changes, re-test with Claude B
6. Repeat as new scenarios emerge

### Watch for navigation patterns

- **Unexpected exploration paths**: Structure may not be intuitive
- **Missed connections**: Links need to be more explicit
- **Overreliance on certain sections**: Content should be in main SKILL.md
- **Ignored content**: May be unnecessary or poorly signaled

---

## Skill Locations and Priority

| Level | Path | Applies to |
|---|---|---|
| Enterprise | Managed settings | All org users |
| Personal | `~/.claude/skills/<name>/SKILL.md` | All your projects |
| Project | `.claude/skills/<name>/SKILL.md` | This project only |
| Plugin | `<plugin>/skills/<name>/SKILL.md` | Where plugin enabled |

Priority: enterprise > personal > project. Plugin skills use `plugin-name:skill-name` namespace.

Monorepo support: Nested `.claude/skills/` in subdirectories are auto-discovered.

---

## Quality Checklist (Anthropic Official)

### Core quality
- [ ] Description is specific, includes key terms, written in third person
- [ ] Description includes both what the skill does and when to use it
- [ ] SKILL.md body under 500 lines
- [ ] Additional details in separate files (if needed)
- [ ] No time-sensitive information
- [ ] Consistent terminology throughout
- [ ] Examples are concrete, not abstract
- [ ] File references one level deep
- [ ] Progressive disclosure used appropriately
- [ ] Workflows have clear steps

### Code and scripts
- [ ] Scripts handle errors explicitly (don't punt to Claude)
- [ ] No magic numbers (all values justified)
- [ ] Required packages listed and verified
- [ ] No Windows-style paths (all forward slashes)
- [ ] Validation/verification steps for critical operations
- [ ] Feedback loops for quality-critical tasks

### Testing
- [ ] At least 3 evaluations created
- [ ] Tested with Haiku, Sonnet, and Opus
- [ ] Tested with real usage scenarios
- [ ] Team feedback incorporated (if applicable)
