# Jarvis security review pilot

Keep `Dependency Security Review` advisory: it is optional and does not itself change code verdicts, approvals, QA waivers or merge requirements. Concrete findings follow ordinary review criteria. Enforcement requires a later explicit decision.

For review policy, use [Jarvis's shared security-review skill](https://github.com/decentraland/agent-server/blob/main/skills/security-review/SKILL.md). For placement rationale and remaining risks, read [the migration assessment](https://github.com/decentraland/agent-server/blob/main/docs/security-review-migration.md).

## Review flow

The [workflow](../.github/workflows/dependency-security-review.yml) inventories changes using trusted default-branch code, without checkout or model execution. It labels relevant PRs `new-dependency` and requests `decentraland-bot` for ready PRs by repository writers. External authors need a maintainer request; drafts wait until ready. Security-relevant pushes request review even when ordinary follow-ups are on demand.

Jarvis incorporates security into its full review. Its host validates bot identity, inspected commit, run marker and verdict before writing the status. PASS becomes success, NEEDS_ATTENTION stays pending, and BLOCK or invalid/failed review becomes failure. A complete irrelevant inventory gets no-changes success. Slack/CLI and review-both reviews without the host marker cannot complete this status.

## Coordinated rollout

1. **Drain:** pause requests, disable the legacy workflow and drain/cancel its runs plus Jarvis's review queue. Continue only when old runs cannot race to publish the same status.
2. **Deploy Jarvis:** deploy runtime and skills together. Verify bot read/review/status-write access and requester write-access enforcement. `ORG_ACCESS_TOKEN` must request reviewers through an actor Jarvis authorizes for this repository.
3. **Switch Unity:** merge the consumer changes into default branch `dev`, then re-enable the workflow and resume requests. `pull_request_target` uses trusted default-branch code; the proposed PR YAML cannot bootstrap itself. The existing review-request webhook subscription suffices.
4. **Exercise the pilot:** re-request the bot on open relevant PRs. Check renderer-only and shared-package changes, importer/asmdef and CI/prompt changes, plus unrelated documentation. Confirm one review with matching `commit_id`, run marker and verdict, and the corresponding advisory status. Missing/stale verdicts must not pass.

Local workflow check: `node --test scripts/ci/dependency-security-review.test.cjs`.

## Recovery

| Result | Action |
| --- | --- |
| Draft or external-author pending | Mark ready as appropriate; a maintainer requests the bot |
| NEEDS_ATTENTION | Supply missing evidence or resolve the concern, then re-request |
| Stale/missing verdict | Wait for the run to finish, then use the Reviewers-panel re-request arrow |
| Request/API/agent failure | Check Actions/Jarvis logs and permissions, resolve the failure, then re-request |
| Truncated inventory | Request a Jarvis review using an immutable git diff and explicit coverage reporting |

For rollback, disable routing and drain runs before reverting both consumer and runtime changes. Re-enable the legacy path only when it is the sole review-result writer. Restoring the old prompt alone does not restore execution; rollback also restores its documented gaps, so keep the status advisory.
