#!/usr/bin/env python3
"""Localize the C++ runtime in a vendored ClearScriptV8.linux-x64.so.

The vendored binary statically links libstdc++/libgcc but was linked without a
version script, so its whole symbol surface (~80k symbols, std::locale included)
is exported with DEFAULT visibility. In the Unity player another plugin pulls in
the dynamic libstdc++.so.6, ClearScript's std:: references then bind across into
that copy while its internal (hidden) runtime state stays in the static copy —
two libstdc++ heaps -> `free(): invalid size` inside V8::Initialize when the
first scene JS isolate starts.

Fix: mark every C++-runtime-owned symbol in .dynsym as STV_HIDDEN. ld.so then
binds same-object relocations against them locally without a scope search
(dl_symbol_visibility_binds_local_p) and skips them in cross-object lookup
(do_lookup_x), so the static runtime becomes fully self-contained. The
ClearScript C interop surface (V8Context_*, V8Isolate_*, StdString_*, ...) keeps
DEFAULT visibility.

Usage:
  clearscript_hide_dynsyms.py IN.so SYMLIST OUT.so

SYMLIST is one mangled name per line — clearscript-hidden-symbols.txt beside
this script is the verified set for the 2026-08 vendored binary
(sha256 35454aef… -> patched 2fe119ea…). To regenerate for a new binary, union:
  (a) pattern set: grep the .so's `nm -D --defined-only` names for
      ^_ZSt / ^_ZN[KV]{0,2}S[tabsiod] / ^_ZT[IVSTC](S|N[KV]{0,2}St) / ^_ZGV /
      ^_ZZ(N?K?St) / ^_ZGTt / ^_ZT[hv][0-9n_]*_N?K?S[tdiso] / __gnu_cxx /
      __cxxabiv1 / ^_Zn[wa] / ^_Zd[la] / ^__cxa_ / ^__dynamic_cast$ /
      ^__gxx_personality_v0$ / ^_Unwind_ / ^__emutls_ / ^__once_proxy$
  (b) intersection of the .so's defined dynamic exports with those of the
      dynamic libstdc++.so.6 + libgcc_s.so.1 it will coexist with.
Verify with: LD_DEBUG=bindings + dlopen(RTLD_NOW) after loading libstdc++.so.6
RTLD_GLOBAL — the patched .so must show zero bindings into libstdc++.so.6.
"""
import struct, sys

so_path, list_path, out_path = sys.argv[1], sys.argv[2], sys.argv[3]
hide = set(l.strip() for l in open(list_path) if l.strip())

data = bytearray(open(so_path, 'rb').read())
assert data[:4] == b'\x7fELF' and data[4] == 2 and data[5] == 1, 'not ELF64 LSB'

e_shoff = struct.unpack_from('<Q', data, 0x28)[0]
e_shentsize = struct.unpack_from('<H', data, 0x3a)[0]
e_shnum = struct.unpack_from('<H', data, 0x3c)[0]

dynsym = None
sections = []
for i in range(e_shnum):
    off = e_shoff + i * e_shentsize
    sh_type = struct.unpack_from('<I', data, off + 4)[0]
    sh_offset = struct.unpack_from('<Q', data, off + 0x18)[0]
    sh_size = struct.unpack_from('<Q', data, off + 0x20)[0]
    sh_link = struct.unpack_from('<I', data, off + 0x28)[0]
    sh_entsize = struct.unpack_from('<Q', data, off + 0x38)[0]
    sections.append((sh_type, sh_offset, sh_size, sh_link, sh_entsize))
    if sh_type == 11:  # SHT_DYNSYM
        dynsym = (sh_offset, sh_size, sh_link, sh_entsize)
assert dynsym, 'no .dynsym'
sym_off, sym_size, str_idx, sym_ent = dynsym
str_off = sections[str_idx][1]
assert sym_ent == 24
nsyms = sym_size // 24

def name_at(st_name):
    end = data.index(b'\x00', str_off + st_name)
    return data[str_off + st_name:end].decode('ascii', 'replace')

patched = 0
seen = set()
for i in range(1, nsyms):
    o = sym_off + i * 24
    st_name = struct.unpack_from('<I', data, o)[0]
    st_shndx = struct.unpack_from('<H', data, o + 6)[0]
    if st_shndx == 0:      # SHN_UNDEF: imports stay untouched
        continue
    nm = name_at(st_name)
    if nm in hide:
        data[o + 5] = (data[o + 5] & 0xF8) | 2   # STV_HIDDEN
        patched += 1
        seen.add(nm)

open(out_path, 'wb').write(data)
print(f'dynsym entries: {nsyms}, patched to HIDDEN: {patched}, '
      f'unique names: {len(seen)}, listed-but-absent: {len(hide - seen)}')
