# Dependency security review moved to Jarvis

The maintained review policy is in
[agent-server/skills/security-review/SKILL.md](https://github.com/decentraland/agent-server/blob/main/skills/security-review/SKILL.md).
It is shared across repositories, with conditional supply-chain, automation and Unity references.

`dependency-security-review.yml` only inventories changed paths and requests a review from
`decentraland-bot`. Jarvis performs the security pass as part of its PR review and owns the
`Dependency Security Review` commit status. This file is a migration pointer, not executable
review instructions. See [the migration and recovery guide](../../docs/security-review.md).
