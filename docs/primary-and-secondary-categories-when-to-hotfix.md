## When to hotfix

**SEV-1** — always hotfix.

**SEV-2** — hotfix only if the bug falls into a **Primary category**.

SEV-2 bugs in Secondary categories are fixed in the normal release cycle.

Sometimes even if a feature belongs to a **Secondary category**, the issue may still require a hotfix if:

- A large percentage of users are affected
- Core gameplay becomes impossible

## 🔴 Primary Categories

*SEV-2 bug in these categories → hotfix required*

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

**Launcher**

- Desktop Client launch

**Marketplace**

- Marketplace — Purchases
- Marketplace Credits

**Creator Tools & Scenes**

- Creator Hub not launching
- Local Scene Preview not Working
- Scene deployments

---

## 🟡 Secondary Categories

*SEV-2 bug in these categories → tracked in #qa-team, no hotfix*

**Avatar & Identity**

- Backpack
- Outfits
- Smart Wearables
- Linked Wearables
- Portable Experiences (internal)

**Social & Communication**

- Autotranslate
- Gifting
- Communities

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

**Economy** *(no matching dashboard category — gap to address)*

- Tips / Donations
- Marketplace — Lists
- Marketplace — Bids
- Referral System

**Admin Tools**

- Admin Tools

**Creator Tools & Scenes**

- Creator Hub (non-launch issues)
- Docs
- SDK7
- Smart Items
- NPCs
- Emotes & Wearables Builder
- Decentraland Cast

**Games & Minigames**

- Mini Games
- Genesis Plaza

