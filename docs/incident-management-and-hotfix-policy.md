**Status:** Active | **Updated:** March 2026 | **Status Page:** [status.decentraland.org](http://status.decentraland.org)

> 💡 **Quick Reference:** Not sure? → post in **#qa-team**. SEV-1 or SEV-2 → escalate to **#crash** with `/create-incident`. SEV-1 resolved → schedule a postmortem within 7 days.
>
> Hotfixes are used **only for:**
>
> - **SEV-1 incidents**
> - **SEV-2 incidents affecting Primary features**
>
> All other issues are fixed in the normal release cycle.

---

# Why this exists

The bigger our teams get, the more changes land in production — and the more things can go wrong. Without a shared process, incidents get reported in the wrong channels, the wrong people get pulled in, communication to the community is inconsistent, and resolution takes longer than it should.

This document exists so that when something breaks, everyone knows exactly what to do, who is responsible, and how to keep the community informed — without noise, confusion, or blame.

---

# Roles

| Role | Who | Responsibility |
| --- | --- | --- |
| **Point** | QA, EM or engineer with the most context on the affected system | Owns resolution — leads the investigation, posts updates via `/update-incident`, coordinates the team, and hands off the role explicitly if needed |
| **QA** | QA team | Verifies and classifies all reports, defines acceptance criteria, and confirms fixes before an incident is closed |

---

# Who to Ping

Tag the right team when reporting or escalating an incident.

| Area | Examples | Handle |
| --- | --- | --- |
| **Explorer / Unity client, explorer launcher, aand-renderer (wearable preview)** | Client crash, rendering issues, login failure, teleport broken, avatar sync, voice chat, wearable not displayed properly in Marketplace, launcher not starting the client | @explorer-support |
| **Marketplace, dApps, website, backend services** | Marketplace down, credits not updating, wallet connection, API failures, asset bundle pipeline, catalysts | @core-support |
| **Creator Tools** | Creator Hub bugs, SDK issues, scene deployments | @creatorstoolteam |
| **QA** | Any report, verification, or severity classification | @qa-team |

---

# Severity

> 🏷️ Severity levels align with GitHub issue labels. See [Issue Prioritization & Labeling Guidelines](https://www.notion.so/Issue-Prioritization-Labeling-Guidelines-26a5f41146a580d9b0fbe455a7648a77?pvs=21) for the full labeling reference.

[Primary & Secondary Features - when to hotfix](https://www.notion.so/Primary-Secondary-Features-when-to-hotfix-31e5f41146a581938a86d53b7f49b482?pvs=21)

| Level | GitHub label | When to use | Examples |
| --- | --- | --- | --- |
| **SEV-1** | `0-critical` | Core platform broken for most users. No workaround. | Login fails for everyone; client won't launch; teleport completely broken; Marketplace inaccessible |
| **SEV-2** | `1-high` | A [primary feature](https://www.notion.so/Primary-Secondary-Features-when-to-hotfix-31e5f41146a581938a86d53b7f49b482?pvs=21) is broken for some users. Platform still partially works. | Other users don't see your avatar; voice chat down; emotes not equipping; emote wheel not working; Marketplace credits not updating |
| **SEV-3** | `2-medium` | A [secondary feature](https://www.notion.so/Primary-Secondary-Features-when-to-hotfix-31e5f41146a581938a86d53b7f49b482?pvs=21) is degraded. Main flows still work. | Weekly goals tooltip wrong; camera shortcuts broken; backpack category filter off |
| **SEV-4** | `3-low` | Minor issue, low user impact. | Wearable panel misaligned; tooltip copy error |
| **SEV-5** | `3-low` | Cosmetic only. No functional impact. | Typo in a navigation label; icon slightly off-position |

**Not sure which severity?** Always report via #qa-team — QA will classify it.

SEV-1 and SEV-2 are **major incidents** escalated to #crash. SEV-3 through SEV-5 stay in #qa-team and the bug reporting tool.

- **SEV-1 incidents always require a hotfix.**
- **SEV-2 incidents require a hotfix only if they affect a Primary feature category (see [Hotfix Policy](https://www.notion.so/Incident-Management-Bug-Reporting-3195f41146a5818da9bbf589b8d97237?pvs=21)).**

> 📎 GitHub label `3-low` maps to both SEV-4 and SEV-5. If there is zero functional impact (purely visual or copy) → SEV-5. Anything with minor but real functional impact → SEV-4.

This applies to third-party services too (AWS, Cloudflare, Livekit, etc.) when they affect platform stability.

---

# The Process

## 1. Report to #qa-team

**All reports start here** — whether it's a crash, a broken feature, a cosmetic bug, or something you're not sure about. Use the bug reporting tool or post directly in #qa-team.

Include:

- What's broken and how to reproduce it
- Platform details (Windows, macOS, VPN on/off, etc.)
- Who is affected (just you, multiple users, everyone?)
- Any relevant screenshots, logs, or links

## 2. QA verifies and classifies

QA confirms the issue, determines severity (SEV-1 through SEV-5), and decides the next step:

- **SEV-3, SEV-4, SEV-5** → tracked and handled within #qa-team and the bug reporting tool. No escalation to #crash.
- **SEV-1 or SEV-2** → QA (or the reporter) escalates to #crash immediately via `/create-incident`.
- During verification, QA will escalate the issue to the correct team.

## 3. Escalate to #crash (SEV-1 and SEV-2 only)

Run `/create-incident` in #crash or DM @crashbot. Fill in:

- **Title** — short description of what's broken
- **Description** — what's happening, how to reproduce, who is affected
- **Severity** — SEV-1 or SEV-2
- **Point** — engineer taking ownership

Once the incident is created, **immediately notify**:

- **Support team** — so they can manage incoming player reports and align on what to communicate
- **Marketing team** — so they can prepare community messaging and monitor Discord and social

> ⚠️ **All communication about the incident from this point on happens inside the #crash thread.** Do not split discussion across channels.

## 4. Point leads resolution

- Update the #crash thread
- Coordinate the investigation
- Use `/update-incident` to post progress as it develops — this keeps the status page current
- Hand off the Point role explicitly in #crash if needed (shift change, fatigue)
- Once root cause is identified, the **Point and QA decide whether the fix should be released as a hotfix or scheduled for the normal release cycle**, following the [**Hotfix Policy**](https://www.notion.so/Incident-Management-Bug-Reporting-3195f41146a5818da9bbf589b8d97237?pvs=21).

## 5. QA validates the fix

- Confirm reproduction and track how many users or flows are affected
- Define acceptance criteria for the fix
- Verify the fix before the incident is marked Closed

## 6. Resolve & close

1. QA confirms the fix
2. Point announces **"All Clear"** in the #crash thread
3. Run `/update-incident` → set status to **Closed** (or **Invalid** if it was a false report)
4. Status page updates automatically
5. For **SEV-1**: schedule the postmortem within 7 days

---

# Hotfix Policy - When to hotfix

[Primary & Secondary Features - when to hotfix](https://www.notion.so/Primary-Secondary-Features-when-to-hotfix-31e5f41146a581938a86d53b7f49b482?pvs=21)

**SEV-1** — always hotfix.

**SEV-2** — hotfix only if the bug falls into a [**Primary category**](https://www.notion.so/Primary-Secondary-Features-when-to-hotfix-31e5f41146a581938a86d53b7f49b482?pvs=21).

SEV-2 bugs in Secondary categories are fixed in the normal release cycle.

Sometimes even if a feature belongs to a [**Secondary category**](https://www.notion.so/Primary-Secondary-Features-when-to-hotfix-31e5f41146a581938a86d53b7f49b482?pvs=21), the issue may still require a hotfix if:

- A large percentage of users are affected
- Core gameplay becomes impossible

They should contain **only the minimal change necessary to resolve the incident**.

---

# Postmortem / RCA

Postmortems are **mandatory** for SEV-1 incidents.

SEV-2 through SEV-5 do not require a postmortem. If a SEV-2 keeps recurring, the team can choose to run one optionally.

Blameless by default — incidents are a system problem, not a person problem.

**Point is responsible for:**

1. Scheduling the review meeting within 7 days, inviting all relevant engineers, QA, and stakeholders
2. Writing the postmortem document using the [RCA template](https://github.com/decentraland/rca) and sharing the link in the #crash thread
3. Attaching the RCA link via `/update-incident` — it will appear under Past Incidents on the status page

**Postmortem document must cover:**

- Incident summary (title, dates, severity, duration)
- Timeline of events
- Root cause(s)
- Impact (users affected, features/services down)
- What went well / what went wrong
- Action items with owner and due date

---

# FAQ

**Q: Where do I report an issue if I don't know how severe it is?**

A: Always start with #qa-team. QA will classify it and decide whether to escalate to #crash.

**Q: Who decides who the Point is?**

A: The Point is whoever has the most context on the affected system. Anyone can self-assign. If no one does, the engineering manager for that area nominates someone.

**Q: What if the Point doesn't have enough context to lead resolution?**

A: They should immediately nominate someone with better context and hand off the role explicitly in the #crash thread.

**Q: Can discussion happen outside of the #crash thread once an incident is escalated?**

A: No. Once escalated to #crash, all updates, decisions, and coordination happen in that thread. Side conversations should be brought back into the thread.

**Q: What if the same incident is reported in another channel?**

A: Direct people to #qa-team. We use one channel per stage. Educate by repetition.

**Q: Do third-party outages (AWS, Cloudflare, Livekit…) follow the same process?**

A: Yes — if it impacts platform stability, it gets reported via #qa-team and escalated to #crash if SEV-1 or SEV-2, even if we can't fix it ourselves — we still own the communication to the community.

**Q: What if we can't reproduce a reported incident?**

A: Gather more context from the reporter — additional logs, steps, or environment details. If the issue still can't be reproduced or verified after investigation, use `/update-incident` to mark it **Invalid** with a short note explaining why, so there's a record.

**Q: When is a postmortem not required?**

A: SEV-2 through SEV-5 do not require a postmortem. If a SEV-2 keeps recurring, the team can choose to run one optionally.

**Q: How do we decide if an incident requires a hotfix?**

A: Use the following rule:

- **SEV-1 incidents always require a hotfix.**
- **SEV-2 incidents require a hotfix only if they affect a Primary category.**
- SEV-2 incidents affecting Secondary categories are fixed in the normal release cycle.

If unsure, discuss in the **#crash thread** and involve QA and the engineering manager for that area.

**Q: What if a Secondary feature issue affects a large number of users?**

A: User impact can override category classification.

If the issue:

- affects a large portion of users
- prevents users from entering the world
- blocks core gameplay

then the incident may still require a **hotfix**, even if the feature normally belongs to a Secondary category.

