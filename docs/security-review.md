# Jarvis dependency and security review

The review policy lives in [Jarvis's shared security-review skill](https://github.com/decentraland/agent-server/blob/main/skills/security-review/SKILL.md), with supply-chain, automation and Unity references. It covers Explorer, avatar-preview-renderer and unity-shared-dependencies. The repository's former dependency prompt is only a migration pointer.

This is an **advisory pilot**. Keep `Dependency Security Review` out of required branch checks. Its result does not directly change code-review approval, QA waivers or merge requirements. Existing code-review criteria still apply to concrete findings.

## How it runs

1. `dependency-security-review.yml` handles PR metadata from trusted default-branch workflow code. It checks the complete changed-file list, including old paths on renames, with no checkout, install, PR code execution or LLM inside Actions.
2. Dependency, plugin/import, assembly, editor/build, workflow/action or agent-instruction changes receive the informational `new-dependency` label and a pending status. A complete inventory without such changes gets a no-changes success.
3. For ready PRs by repository writers, the workflow requests `decentraland-bot` using `ORG_ACCESS_TOKEN`. For external authors, a maintainer must request that reviewer explicitly. Drafts wait until ready. Security-relevant pushes request review even though ordinary code follow-ups are otherwise on demand.
4. Jarvis's existing signed/repo-authorized webhook runs one full review with the shared security pass. Its host captures base/head and generates a run marker. The agent posts one COMMENT review attached to that exact head.
5. Jarvis validates bot identity, head, run marker and one explicit verdict before publishing `Dependency Security Review`: PASS → success; NEEDS_ATTENTION → pending with a human-follow-up message; BLOCK or invalid/failed review → failure. All writes target the captured SHA.

Jarvis sets this status for its webhook reviews of unity-explorer, including manually requested ones. Slack/CLI or review-both results without the host run marker do not satisfy it. No separate Claude API key is used by this workflow.

## Coordinated rollout

These steps are deployment work; preparing the migration does not execute them.

1. Pause review requests and disable the legacy `Dependency Security Review` workflow during the cutover. Drain/cancel its old runs and drain Jarvis's review queue. This prevents old Claude and new Jarvis runs racing to write the same context.
2. Deploy the agent-server change (runtime plus the full skill directory). Confirm the configured bot has read/review and commit-status write access to this repository. Keep requester write-access enforcement enabled. `ORG_ACCESS_TOKEN` must be able to request reviewers; its actor must pass Jarvis's repository authorization.
3. Merge the Unity workflow, prompt pointer, review-instruction and documentation changes into `dev` (the repository's default branch). Metadata-only `pull_request_target` executes the trusted default-branch workflow; a PR cannot bootstrap this switch from its own proposed YAML.
4. Re-enable the workflow and resume review requests. Re-request `decentraland-bot` on open security-relevant PRs; the new workflow can also be exercised by a new commit/ready event. No new webhook event subscription is needed beyond the existing PR review-request integration.
5. Observe a renderer-only manifest change, a shared-package change, an importer/asmdef change and a CI/prompt change. Confirm a single review with the exact `commit_id`, run marker and security verdict, and the corresponding advisory status. Check an unrelated documentation change too.

Do not make the check required during this pilot. A later enforcement decision needs explicit approval after practical results have been reviewed.

## Recovery and rollback

- **Pending: external/draft:** mark ready as appropriate; a maintainer requests `decentraland-bot` from the Reviewers panel.
- **Pending: NEEDS_ATTENTION:** supply provenance, missing source or other evidence; re-request a review after resolving the stated uncertainty.
- **Stale or missing verdict:** wait for the current run to finish, then use the re-request arrow. Old comments or reviews are never reused as a current-run PASS.
- **Request/API/agent failure:** inspect the Actions/Jarvis logs and credential permissions, then re-request. If detection was truncated, a manual Jarvis review can inspect an immutable git diff and report its coverage.
- **Rollback:** disable the routing workflow and drain outstanding runs first, then revert the coordinated consumer/runtime changes before re-enabling the legacy path. Restoring only the old prompt does not restore execution. The original flow's documented gaps would also return; keep it advisory and do not run two status writers together.

Local workflow regression check: `node --test scripts/ci/dependency-security-review.test.cjs`.
The broader assessment and validation are in [agent-server's migration review](https://github.com/decentraland/agent-server/blob/main/docs/security-review-migration.md).
