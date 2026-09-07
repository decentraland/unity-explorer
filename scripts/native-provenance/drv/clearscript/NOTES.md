# ClearScript provenance — two derivations, two claims

## default.nix — FROM-SOURCE build (lock entry `clearscript-source`, divergent)

Layered for iteration efficiency:
- `v8-monolith.nix`  — V8 14.7.173.23 assembled from 17 DEPS-pinned fixed-output
  fetches + ClearScript's V8Patch/BuildPatch/ICUPatch + nix-compat patches
  (`patches/build-nix-compat.diff`, `patches/polyfill-guarded.h`) -> gn/ninja
  -> libv8_monolith.a + the PATCHED include tree. Heavy (~2200 targets);
  rebuilds only when V8 inputs change. NOTE: gn/ninja nixpkgs setup hooks are
  disabled (dontUseGnConfigure/dontUseNinja*) — they hijack the phases and run
  `gn gen` before the tree exists.
- `default.nix`      — ClearScriptV8 wrapper (16 TUs per Unix/ClearScriptV8/
  Makefile, clang++ -std=c++20 -O3 -fvisibility=default -fno-rtti), linked
  -static-libstdc++ -static-libgcc -fuse-ld=lld -lv8_monolith + static
  libatomic.a, then the DCL hide-dynsyms pass. ~1 min; iterating here never
  rebuilds V8.

Verdict `divergent` — expected and accepted (current clang 22/libstdc++ 15 vs
Microsoft's chromium-clang 23). Audited divergence:
- ALL 151 shipped C interop entry points (V8SplitProxy*/V8Context_*/
  V8Isolate_*/V8Value_*/StdString_*/Memory_*) present in the built lib; 0 missing.
- dynsym_only_shipped: v8-internal + libstdc++ template instantiations
  (compiler-version emission differences).
- dynsym_only_built: libat_lock_n/libat_unlock_n (static libatomic.a — clang 22
  emits 16-byte-CAS libcalls that chromium-clang inlines) + gcc-15 era
  template churn.

## binary-repack.nix — byte-identical provenance proof (lock entry `clearscript`, reproduced)

The shipped blob is NOT a Decentraland compile. It is Microsoft's official
NuGet binary (Microsoft.ClearScript.V8.Native.linux-x64 7.5.1, inner .so
sha256 35454aef...) post-processed by the in-tree clearscript_hide_dynsyms.py
+ clearscript-hidden-symbols.txt (7274 libstdc++/libgcc dynsyms -> STV_HIDDEN;
fixes the dual-libstdc++ `free(): invalid size` crash in the Unity player).
binary-repack.nix reproduces that pipeline byte-for-byte
(sha256 2fe119ea... == shipped).

## Which to use

- Shipping the status quo / verifying what shipped: binary-repack.nix.
- Escaping the Microsoft binary dependency (own toolchain, own V8): default.nix.
