You are reviewing a PR that adds or modifies external dependencies in a Unity project.
This is a SECURITY-FOCUSED review. The project ships as a desktop client to end users.

Your job is to identify dependency changes visible in the PR, assess their supply-chain and runtime risk, and clearly separate:
1. facts supported by the diff/repo,
2. reasonable inferences,
3. unknowns that require manual follow-up.

Do not guess. If something cannot be determined from the PR, mark it as UNKNOWN.

--- STEP 1: IDENTIFY ALL DEPENDENCY CHANGES ---
List every external dependency change visible in this PR:

- New or changed entries in `Packages/manifest.json`
  - Include package name, source (registry/git/path), and exact version/tag/commit if present
- New or changed entries in `Packages/packages-lock.json`
  - Include direct and notable transitive changes
- New binary or native plugin files added or modified under:
  - `Explorer/Assets/Plugins/`
  - any `Assets/**`
  - any package contents included in the repo
- New or modified `.asmdef` files that reference precompiled assemblies or change assembly visibility
- New editor scripts, build scripts, install hooks, or package setup code that execute automatically

For each item, state:
- name
- version / commit / file hash if available
- source
- type: source code / managed binary / native binary / unknown
- scope: runtime / editor-only / test-only / unknown

--- STEP 1.5: LOOK UP PACKAGE REGISTRY METADATA ---
For each new or changed dependency identified above, check its public registry listing.

For NuGet packages (.dll from NuGet.org): fetch the NuGet page (e.g. https://www.nuget.org/packages/{PackageName}/{Version})
For Unity packages (com.* in manifest.json): check the Unity package registry or the linked git repo
For GitHub-sourced packages: check the repository page

Extract and record (label all findings as **[Registry metadata]** to distinguish from PR-visible facts):
- **Description and stated purpose** — does it match what the PR uses it for?
- **Download count / popularity** — is this widely used or obscure?
- **Listed dependencies** — do they match what's committed in the PR? Are there unexpected transitive dependencies?
- **Notices, warnings, or restrictions** — any regional restrictions, political notices, usage conditions beyond the license, or availability limitations stated on the listing page
- **Last published date** — is the version current or abandoned?
- **License** — compatible with Apache 2.0?
- **Known vulnerabilities** — any advisories listed on the registry page?
- **Publisher / author** — same as claimed in the PR? Any other notable packages by this publisher?

If a registry page is unreachable or does not exist, note that and move on.

--- STEP 2: ASSESS EACH NEW OR CHANGED DEPENDENCY ---
For each dependency, assess the following using evidence from the PR diff AND the registry metadata gathered above. Always label the source of each finding: **[PR]**, **[Registry metadata]**, **[Inference]**, or **[UNKNOWN]**.

A. Provenance / source trust
- Is the publisher clearly identifiable and reputable?
- Is the dependency pinned to an immutable version or commit?
- Flag branch refs, floating versions, local paths, or unpinned git URLs
- Is this an internal fork, official upstream, or third-party fork?
- For binaries: is corresponding source code referenced or vendored? If not, mark provenance as weaker

B. Runtime capability / attack surface
Check whether the dependency appears to:
- make network requests or expose network-facing functionality
- render UI, HTML, webviews, overlays, or external content
- access filesystem locations beyond normal app data
- use reflection, dynamic loading, or runtime assembly loading
- include native plugins (`.dll`, `.so`, `.dylib`, `.bundle`)
- change behavior by platform, locale, region, or remote config
- introduce capabilities disproportionate to its stated purpose

If not provable from the PR, mark UNKNOWN rather than assuming.

C. Shipping impact
- Does this ship in the end-user desktop client, or is it editor/dev/test only?
- Does it affect all platforms or only specific desktop targets?

D. Maintenance / known risk
If the PR includes evidence, note:
- maintenance status
- security advisories / CVEs
- license
If not visible from the PR, mark UNKNOWN and recommend manual verification where relevant.

E. Transitive risk
From lockfile changes, identify notable transitive dependencies that are:
- new
- executable/native
- low-trust / obscure
- unusually privileged for the parent dependency

--- STEP 3: RISK CLASSIFICATION ---
Classify each dependency as one of:

LOW RISK
- official or well-established dependency
- immutable version pinning
- source-visible or standard package source
- capabilities are limited or proportionate
- no major provenance concerns

MEDIUM RISK
- some unresolved provenance or maintenance questions
- network/filesystem/native capability that is proportionate but worth human review
- notable transitive dependency risk
- editor/runtime boundary unclear
- claims require manual validation

HIGH RISK
- opaque binary or native plugin added for runtime use without trustworthy provenance
- unpinned or floating external dependency
- unknown publisher plus privileged capability
- dynamic code loading / assembly loading / remote executable behavior
- suspicious mismatch between stated purpose and actual capability
- region/platform-specific behavior with insufficient reviewability

--- STEP 4: OUTPUT ---
Produce:

1. A concise summary table with:
- dependency / file
- version
- source
- type
- scope
- risk
- evidence confidence (HIGH / MEDIUM / LOW)

2. For each MEDIUM or HIGH risk item:
- specific concern
- what evidence supports it
- what remains unknown

3. For each HIGH risk item:
- concrete recommendation such as:
  - replace binary with source build
  - pin to commit/version
  - fork and vendor reviewed source
  - restrict to editor-only
  - add allowlist / sandboxing / network restrictions
  - require manual security review before merge

Use inline comments to flag:
- binary additions
- unpinned dependency declarations
- suspicious asmdef or build-script changes

Do not claim to have inspected binary internals unless the PR actually contains inspectable source or metadata.

At the end, emit exactly one:
DEPENDENCY_REVIEW: PASS
DEPENDENCY_REVIEW: NEEDS_ATTENTION
DEPENDENCY_REVIEW: BLOCK

Use:
- PASS only if no meaningful unresolved concerns remain
- NEEDS_ATTENTION if human review is needed or important unknowns remain
- BLOCK if there is a clear high-risk issue that should be addressed before merge
