# draco verdict notes (2026-09-06)

Policy for this drv (user directive mid-task): byte-identity NOT a goal.
Target = clean build of both Unity draco libs from the pinned source rev on
CURRENT nixpkgs (26.11pre-git, gcc 15.3.0). `divergent` is acceptable when
the differences are toolchain noise only.

Built store path: /nix/store/2njsxjwpwv0sv4pd24gac01ivci51344-draco-unity-unity-2021-09-11-427fc7e6

## Source pin (verified provenance chain)
- Explorer manifest: com.atteneder.draco @ 4.0.2 (OpenUPM); UPM tarball ships
  the exact shipped bytes (sha256 match, see
  an earlier out-of-tree build record).
- github.com/atteneder/DracoUnity tag v4.0.2: git-lfs pointers carry the exact
  shipped sha256 oids (dec 4f137c51…, enc d9670d1e…), introduced by commit
  718b1169 "Updating binaries" (2021-09-11 22:18 +0200).
- Native source: github.com/atteneder/draco @
  427fc7e631707762c362706c4e72d3c025793e9e (2021-09-11 21:16 +0200, branch
  legacy/unity-2). Original CI: ubuntu-18.04 / GCC 7.5.0 (matches shipped
  .comment), cmake -G Ninja -DCMAKE_BUILD_TYPE=MinSizeRel
  -DDRACO_UNITY_PLUGIN=ON -DDRACO_GLTF=ON -DDRACO_BACKWARDS_COMPATIBILITY=OFF,
  targets dracodec_unity + dracoenc_unity.

## Per-blob verdict analysis

Both entries in verdict.lock.json are `divergent` with dynsym_match=false.
The dynsym diff was audited symbol-by-symbol for both blobs
(nm -D --defined-only, type+name):

- draco-dec (libdracodec_unity.so): every diff line is either
  (a) linker-synthesized: __bss_start/_end/_edata/_init/_fini — GCC7-era
      binutils exported these; modern ld does not; or
  (b) STB_WEAK (W) C++ template instantiations — 250 weak-symbol churn lines,
      pure inliner/instantiation differences between GCC 7.5.0 and 15.3.0.
  All 273 shipped strong (T) symbols minus _init/_fini are present and
  identical in the built lib (271/271 match). All 10 decoder P/Invoke entry
  points (DecodeDracoMeshStep1/2, GetAttribute*, GetMeshIndices,
  ReleaseDraco*) verified present.

- draco-enc (libdracoenc_unity.so): same pattern — linker artifacts plus 296
  weak-symbol churn lines; zero strong-symbol diffs besides _init/_fini
  (283/283 match). All 11 encoder P/Invoke entry points (dracoEncoder*)
  verified present.

Conclusion: both `divergent` verdicts are toolchain noise only (new gcc/ld =
desired outcome per policy). API surface consumed by Unity via P/Invoke is
100% intact for both libraries.

## Build patching
2021-era draco misses explicit <cstdint> includes that gcc>=13 libstdc++ no
longer provides transitively; the drv injects `#include <cstdint>` into 17
headers in postPatch (toolchain compat only, no functional change).
