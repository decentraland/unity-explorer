# Jarvis security-review pilot

`Dependency Security Review` is an advisory status for security-relevant Unity Explorer changes. It does not change approvals, QA waivers, branch protection, or merge requirements. Ordinary reviews continue to check code and general security without the detailed Unity pass.

The detailed policy lives in [Agent Server's Unity security-review skill](https://github.com/decentraland/agent-server/blob/main/skills/security-review/SKILL.md). Its placement and host safeguards are documented in the [migration assessment](https://github.com/decentraland/agent-server/blob/main/docs/security-review-migration.md).

## Review flow

The [trusted workflow](../.github/workflows/dependency-security-review.yml) reads PR metadata and the complete changed-file inventory. It never checks out or executes PR code.

1. The workflow classifies the current base/head change.
2. It adds `new-dependency` when detailed review applies, or removes a stale label when it does not.
3. For a ready PR by a repository writer, it requests `decentraland-bot` after the label update finishes. Drafts wait until ready. External authors require a maintainer to request the bot.
4. Agent Server reads repository and label state from the signed review-request webhook. Only Unity Explorer with `new-dependency` starts the detailed path.
5. Jarvis performs one normal review and extends it with the applicable Unity dependency/plugin or automation checks. It posts one COMMENT review.
6. Agent Server verifies the bot, head commit, run marker, and verdict, then updates `Dependency Security Review` on that commit.

An irrelevant PR has no detailed status. A label or classification failure does not request Jarvis. Agent Server alone writes the security status; the workflow does not infer a result from comments.

## What triggers the detailed pass

- UPM manifests, locks, embedded/local packages, and shared package contents in `Explorer/`, `avatar-preview-renderer/`, or `unity-shared-dependencies/`
- managed/native plugin payloads, importer metadata, assemblies, and editor/build/install hooks
- workflows, local actions, build scripts, review prompts, skills, and other privileged automation
- renamed, removed, or patchless files whose old or new path matches this scope

Mixed changes run both specialized branches. The detailed pass reuses the normal review's diff, inventory, context, and security findings instead of collecting them again.

## Results

| Result | Status | Next action |
| --- | --- | --- |
| PASS | Success for repository writers; pending for external authors | Review the COMMENT findings; obtain human confirmation for external authors |
| NEEDS_ATTENTION | Pending | Supply evidence or resolve the concern, then re-request |
| BLOCK | Failure | Resolve the high-risk finding, then re-request |
| Missing, invalid, stale, or failed review | Failure on the captured commit | Check workflow/Jarvis logs, then re-request on the current head |

Concrete findings use the normal code-review severity rules. The status is a pilot signal, not an approval.

## Rollout and recovery

Deploy this workflow change with or before Agent Server's label routing. Reclassify open PRs so stale `new-dependency` labels are removed, and drain legacy model runs before relying on Jarvis as the sole status writer.

After deployment, exercise a renderer-only change, shared-package change, binary/importer or assembly change, automation/prompt change, and unrelated documentation change. Confirm the label matches the current head and that relevant PRs produce one COMMENT review with the matching `commit_id`, run marker, verdict, and advisory status.

Re-request Jarvis after a push, retarget, stale snapshot, invalid verdict, or failed run. If the workflow cannot classify the inventory or maintain the label, fix that failure before requesting manually; otherwise the webhook will intentionally run only the ordinary review.

Run the local workflow checks with:

```text
node --test scripts/ci/dependency-security-review.test.cjs
```
