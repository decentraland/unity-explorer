## Feature Categories for Severity & Hotfix Decisions

This document is the source of truth for two decisions:

1. **Severity labeling** — whether a bug gets `1-high` (SEV-2) or `2-medium` (SEV-3) on GitHub
2. **Hotfix vs normal release** — whether a SEV-2 bug ships as a hotfix or waits for the next release cycle

**The rule:** Primary category bugs that meet SEV-2 criteria get `1-high` and require a hotfix. Secondary category bugs get `2-medium` and are handled in the normal release cycle.

> **If the auto-labeler assigns the wrong priority:** check whether the affected feature is in the right section below and move it if needed. The bot reads this file directly.

---

## When to hotfix

**SEV-1** — always hotfix.

**SEV-2** — hotfix only if the bug falls into a **Primary category**.

SEV-2 bugs in Secondary categories are fixed in the normal release cycle.

Sometimes even if a feature belongs to a **Secondary category**, the issue may still require a hotfix if:

- A large percentage of users are affected
- Core gameplay becomes impossible

---

## 🔴 Primary Categories

*SEV-2 bug in these categories → `1-high` label + hotfix required*

**Stability & Crashes**

- All crashes and freezes, severity depends on the impact of user base

**Auth & Onboarding**

- Login — Web3
- Login — Social (Google, Discord, etc)
- Login — Email + OTP
- Any step of the funnel preventing new and returning users accessing the platform

**Social & Communication**

- Friends
- Text Chat (live communication)
- Communities Chat
- Private Voice Chat

**Blocking & Reporting**

- Block Players

**Avatar & Identity**

- NAMEs (citizen identity)
- Profile (passport)
- Wearables equipping
- Emotes equipping
- Avatar synchronization / visibility to other players
- Avatar locomotion

**World & Navigation**

- Teleportation
- Worlds access

**Video Streaming**

- Video Streaming
- Decentraland Cast

**Genesis Plaza**

- Events
- Streaming
- Entering the platform

**Launcher**

- Desktop Client launch

**Marketplace**

- Marketplace — Purchases

**Admin Tools**

- Admin Tools

**Creator Tools & Scenes**

- Creator Hub not launching
- Local Scene Preview not Working
- Scene deployments
- AssetPacks
- Templates
- Creating/Publishing Wearables & Emotes
- Emotes & Wearables Builder

---

## Excessive Resource Usage

Issues where the client silently uses too many resources (network, storage, CPU, battery) for all users without a visibly "broken" feature. These are easy to miss because no single feature stops working, but they can have severe impact on performance, battery, bandwidth, and infrastructure cost.

| Severity | Condition | Examples |
|----------|-----------|----------|
| **SEV-1** | Affects all users, grows over time, or causes significant infrastructure cost | Failed analytics events retried indefinitely (e.g. Segment size-limit rejection loop); unbounded network upload draining bandwidth and incurring cost; local database growing without limit; memory leak causing crashes over time; shader or system consuming excessive CPU/GPU for all users |
| **SEV-2** | Affects a subset of users or is bounded/self-limiting | Excessive polling frequency on a specific screen; background task consuming high CPU/GPU only while a specific panel is open; disk I/O spike limited to a single flow |

**Key signals to watch for:**
- HTTP 4xx errors in a retry loop (the request will never succeed, but retries keep going)
- Local storage (SQLite, files) growing without bounds
- Abnormally high network upload/download that doesn't correspond to user activity
- Steadily increasing memory usage that doesn't stabilize (memory leak)
- CPU or GPU running at unexpectedly high utilization during normal use
- Constant disk writes to local cache or logs slowing the system

These issues should be treated as **SEV-1 when they affect all users and grow over time** — even if no user-facing feature appears broken.

---

## 🟡 Secondary Categories

*SEV-2 bug in these categories → `2-medium` label, tracked in #qa-team, no hotfix*

**Avatar & Identity**

- Backpack (filtering, sorting, search, UI)
- Outfits
- Smart Wearables
- Linked Wearables
- Portable Experiences

**Social & Communication**

- Autotranslate
- Gifting
- Communities (non-chat: discovery, membership, settings)

**World & Navigation**

- Events Calendar
- Places Browser
- Events dApp
- Places dApp
- Top Scenes

**UI & Settings**

- Loading Screens
- Notifications
- Camera / Gallery
- Skybox
- Badges

**Economy**

- Tips / Donations
- Marketplace — Lists
- Marketplace — Bids
- Marketplace — Rents
- Referral System
- Marketplace Credits

**Creator Tools & Scenes**

- Creator Hub (non-launch issues)
- Docs
- SDK7
- Smart Items
- NPCs
- Curating emotes

**Games & Minigames**

- Mini Games
- Genesis Plaza scene-specific functionality
