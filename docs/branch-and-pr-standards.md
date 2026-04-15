# Branch & PR Standards

This document describes how code reaches `dev` and `main` — branching, PR metadata, the automated review/approval flow, and the labels that drive it. External contributors and team members should both be able to open a PR using only this page.

---

## Branches

Two long-lived branches:
- **`dev`** — integration branch. All feature and fix branches merge here.
- **`main`** — release branch. Only release-cut commits land here.

Work branches are created off `dev` using these prefixes:

| Prefix | Purpose |
|---|---|
| `feat/` | New functionality |
| `fix/` | Bug fix |
| `chore/` | Cleanup, tooling, non-runtime tweaks |
| `opti/` | Optimization |

Feature-scoped sub-prefixes are allowed: `feat/analytics/...`, `fix/sdk-scene/...`.

---

## PR title

The PR title must start with one of the same prefixes, **lowercase**, followed by a colon:

- `feat: add emote wheel quick-select`
- `fix: null ref when scene unloads mid-animation`
- `chore: bump UniTask to 2.5.10`
- `opti: skip avatar skinning for offscreen players`

The prefix matters — it drives automation (see below). `fix:` and `chore:` PRs receive automatic AI review; `feat:` and `opti:` do not.

If the PR closes an issue, include `Fix #NNNN` in the description so GitHub links and auto-closes it.

---

## PR description

The PR template has three sections. All three must be filled in:

1. **What does this PR change?** — what and *why*. For optimizations, include before/after numbers. For SDK features, include or link a test scene.
2. **Test Instructions** — copy-pasteable steps. This is what QA will run.
3. **Quality Checklist** — self-check before marking ready for review.

A PR with empty sections will typically be sent back before review starts.

---

## What happens after you open the PR

The flow below is entirely automated — you usually don't need to assign anyone by hand.

### 1. Reviewers are auto-assigned
- **1 QA reviewer** is picked from the `qa` team ([`.github/auto_assign_config_qa.yml`](../.github/auto_assign_config_qa.yml)).
- **1 developer reviewer** is picked from the `devs` or `techleads` group ([`.github/auto_assign_config_dev.yml`](../.github/auto_assign_config_dev.yml)).

Skip auto-assign by adding the `no review` or `no QA needed` label (see [Labels](#labels) for when that's legitimate).

### 2. AI pre-review

The `Claude Review` workflow ([`.github/workflows/claude-pr-review.yml`](../.github/workflows/claude-pr-review.yml)) reviews the diff against the project's architecture docs and code standards.

**Automatic** for `fix:` and `chore:` PRs — runs on open and on every push.

**Manual** for `feat:` and `opti:` PRs — comment `@claude review` on the PR to trigger it. External contributors will see a welcome comment with these instructions when they open a PR.

The review:
- Reads the diff, [`CLAUDE.md`](../CLAUDE.md), and the relevant subsystem docs.
- Checks whether the PR addresses the root cause or patches a symptom.
- Posts inline comments for blocking issues (bugs, standards violations, missing error handling, perf regressions).
- Classifies the PR as **SIMPLE** or **COMPLEX** (with a one-line justification).
- Decides whether QA is needed (`QA_REQUIRED: YES/NO`).
- Emits a `Claude Review` commit status (pass/fail).

**Outcomes (for auto-reviewed `fix:`/`chore:` PRs):**

| AI verdict | Complexity | Effect |
|---|---|---|
| PASS | SIMPLE | Auto-approves the PR, adds `claude-approved` label, DEV approval no longer required. |
| PASS | COMPLEX | Posts a comment: "no blocking issues, but complex — human DEV review still required." |
| FAIL | any | Blocks merge until issues are addressed. |

For `feat:`/`opti:` PRs, the AI review provides feedback but does **not** auto-approve — human DEV review is always required.

**AI review has real limits.** It checks standards, bugs, and obvious issues. It cannot judge architectural fit, cross-system impact, or whether the PR fixes the *root cause* vs. a symptom. A green Claude status is not a substitute for a human reviewer on a complex change.

You can invoke or re-invoke it at any time with a PR comment:
- `@claude review` — fresh review (required for `feat:`/`opti:` PRs)
- `@claude re-review` — focus on whether prior feedback was addressed

### 3. QA review
QA runs the **Test Instructions** from the PR description against a build of the branch (typically via `metaforge explorer run <PR>`). If steps are missing or unclear, QA will send the PR back — they should not have to reverse-engineer the test plan from the diff.

### 4. DEV review
A developer reviewer evaluates architecture, correctness, and whether the PR fits the broader system. If the AI auto-approved a SIMPLE PR, this step is waived — but any human reviewer may still override and request changes.

### 5. Merge gate
The `Enforce QA and DEV Approvals` workflow ([`.github/workflows/enforce-group-approvals.yml`](../.github/workflows/enforce-group-approvals.yml)) blocks merge until all of the following hold:

- ≥1 approval from the `qa` team (unless `no QA needed`).
- ≥1 approval from the `explorer-devs` team (unless `claude-approved`).
- All CI checks green.
- PR is not a draft.

When all conditions are met, **use "Squash and merge"**. Write a clear squash message — title + bulleted list of changes + test steps. Most of this can be copied from the PR description.

---

## Labels

Labels drive policy decisions. Only apply them when the criteria below are genuinely met.

| Label | Effect | When to apply |
|---|---|---|
| `claude-approved` | DEV approval no longer required. | Applied **automatically** by AI review on SIMPLE+PASS. Don't apply by hand. |
| `no QA needed` | QA approval no longer required. | Changes limited to CI/CD, scripts, docs, or other non-runtime code. **No** user-facing or runtime Unity code touched. Applied automatically when AI review determines this, or manually with justification in the PR. |
| `no review` | Suppresses AI review and reviewer auto-assignment. | Rare — reserved for trivial maintenance where the author has coordinated review separately. |
| `auto-pr` | Skips both AI review and the approval gate entirely. | Bot-generated PRs only (dependency bumps, auto-sync). Humans should not apply this. |

If in doubt, **do not apply the label.** Let the default flow run.

---

## External contributors

You don't need to be on the `qa` or `explorer-devs` teams to open a PR. Follow this page and the PR template and the automation will route reviewers to you.

When you open a PR, you'll receive a **welcome comment** that explains the review process for your specific PR type — whether AI review runs automatically or needs to be triggered manually with `@claude review`.

A few things to know:

- The auto-assigned reviewers will handle the QA/DEV approvals the merge gate requires — you don't need to pin anyone yourself.
- For `feat:` and `opti:` PRs, AI review is **not automatic**. Comment `@claude review` to request it, or `@claude re-review` after addressing feedback.
- If you're unsure whether a change is "simple" or "complex," lean toward **complex**: write a richer technical description and don't worry if the AI classifies it as COMPLEX. That just means a human dev will look at it, which is the right outcome for anything non-trivial.
- You **cannot** apply `claude-approved`, `no QA needed`, `no review`, or `auto-pr` yourself, and you should not request them. Let the automation or a maintainer decide.
- If AI review raises a concern you think is wrong, reply on the inline comment with reasoning rather than silently dismissing it. A maintainer will adjudicate.

---

## Commits

Commits inside a feature branch have **no format requirements** — the branch will be squashed on merge, so commit as often as you like (think of them as save points).

The squash commit message that lands on `dev` is what matters:
- Self-explanatory title (mirrors the PR title).
- Bulleted list of changes.
- Test steps copied from the PR description.

GitHub pre-fills most of this from the PR; tidy it before clicking the final merge button.

---

## Quick reference for common situations

| Situation | What to do |
|---|---|
| Opened a `fix:` PR, Claude approved it, but you want a human to look anyway | Leave it — the `claude-approved` label only waives the DEV *requirement*. Any human can still approve or request changes. |
| QA reviewer isn't responding | Ping in `#qa-team` on Slack. Don't reassign QA — the auto-assign picked the right rotation. |
| Your PR touches only GitHub workflows | It's still a real PR. The AI should mark it `QA_REQUIRED: NO`; if it doesn't, add `no QA needed` manually and note why. |
| You disagree with an AI inline comment | Reply with your reasoning on the thread. A maintainer will resolve it. Don't silently resolve/dismiss AI comments without engaging. |
| You want a faster turnaround on a hotfix | See [Incident Management & Hotfix Policy](incident-management-and-hotfix-policy.md) — there is a separate fast-track process for SEV-1/SEV-2. |
