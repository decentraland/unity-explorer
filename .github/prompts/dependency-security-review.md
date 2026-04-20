You are reviewing a PR that adds or modifies external dependencies in a Unity project.
This is a SECURITY-FOCUSED review. The project ships as a desktop client to end users.

--- STEP 1: IDENTIFY ALL DEPENDENCY CHANGES ---
List every external dependency change in this PR:
- New packages added to `Packages/manifest.json` (name, source URL, version/commit)
- Version changes to existing packages
- New DLL/binary files added under `Explorer/Assets/Plugins/` or anywhere under `Explorer/`
- New or modified `.asmdef` files that reference external assemblies
- Changes to `Packages/packages-lock.json` (transitive dependency changes)

For each dependency, state: name, version, source, and whether it's a binary (DLL/SO/dylib) or source code.

--- STEP 2: EVALUATE EACH NEW/CHANGED DEPENDENCY ---
For each dependency identified, assess:

**Source trust:**
- Is the source a known, reputable publisher? (Unity official, Microsoft, established OSS with >500 stars)
- Is the version pinned to an immutable reference (commit SHA, exact version)? Flag branch refs or unpinned git URLs.
- Is there a Decentraland fork, or is it pulled directly from a third party?
- For DLLs: can the binary be reproduced from publicly available source code? If not, flag as HIGH RISK.

**Capabilities and attack surface:**
- Does this library make network requests? To what domains? Can it contact arbitrary endpoints?
- Does it render UI, web content, popups, or overlays?
- Does it access the filesystem beyond its own data directory?
- Does it use reflection, runtime code generation, or load additional assemblies dynamically?
- Does it have platform-specific or locale/region-specific behavior? (This was the vector in a past incident where a library showed harmful content to users in specific countries.)
- Does it include native plugins (.dll/.so/.dylib/.bundle) that can't be inspected as C# source?
- Does it have capabilities disproportionate to its stated purpose? (e.g., a video URL resolver that includes an HTML rendering engine)

**Maintenance and reputation:**
- When was the library last updated?
- Does it have known CVEs or security advisories?
- Is the license compatible with Apache 2.0?
- Has the author/org published other widely-used packages?

**Transitive dependencies:**
- What transitive dependencies does this library pull in?
- Do any of them have the above risk factors?
- Flag any transitive dependency that is not well-known.

--- STEP 3: RISK ASSESSMENT ---
Classify each dependency as:
- **LOW RISK**: Unity official package, well-known OSS, source-only, no network/UI capabilities
- **MEDIUM RISK**: Established library with some capabilities (network, filesystem) proportionate to its purpose
- **HIGH RISK**: Any of: opaque binaries without reproducible build, network + UI capabilities, locale/region-specific behavior, unmaintained (>12 months), low reputation, disproportionate capabilities

--- STEP 4: OUTPUT ---
Post a summary comment with:

1. A table of all dependency changes with risk level
2. For each MEDIUM/HIGH risk item, a specific explanation of the concern
3. For HIGH risk items, concrete recommendations (fork it, replace the binary with a source build, use an alternative, add runtime sandboxing)

Use mcp__github_inline_comment__create_inline_comment to flag specific files (especially binary additions).

At the end, emit:
DEPENDENCY_REVIEW: PASS (no HIGH risk items)
or
DEPENDENCY_REVIEW: NEEDS_ATTENTION (has MEDIUM risk items that need human judgment)
or
DEPENDENCY_REVIEW: BLOCK (has HIGH risk items that must be addressed before merge)
