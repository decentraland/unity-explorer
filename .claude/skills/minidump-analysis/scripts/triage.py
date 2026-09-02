"""Headless triage for Windows minidumps produced by DclAnrIntegration.

Prints, per dump: exact session age at capture, the main thread's instruction pointer as
module+offset, and a histogram of the modules its stack touches. Accepts .dmp or the .zip
Sentry attachment. No WinDbg, no symbols, any OS.

This gives you WHICH SUBSYSTEM the main thread was in. It cannot give you function names --
for that you need symbols and WinDbg (see SKILL.md).

Usage:
    python3 triage.py dump.zip
    python3 triage.py 'dumps/*.zip'
    python3 triage.py dump.dmp --frames      # also list stack frames in order
"""
import collections, glob, os, struct, sys, zipfile

THREAD_LIST, MODULE_LIST, MISC_INFO, THREAD_INFO_LIST = 3, 4, 15, 17


def load(path):
    if path.endswith('.zip'):
        with zipfile.ZipFile(path) as zf:
            names = [n for n in zf.namelist() if n.endswith('.dmp')]
            if not names:
                return None, None
            return zf.read(names[0]), names[0]
    return open(path, 'rb').read(), os.path.basename(path)


def parse(data):
    def u32(o): return struct.unpack_from('<I', data, o)[0]
    def u64(o): return struct.unpack_from('<Q', data, o)[0]

    sig, ver, nstreams, dirrva = struct.unpack_from('<4sIII', data, 0)
    if sig != b'MDMP':
        return None

    dumped_at = u32(20)
    streams = {}
    for i in range(nstreams):
        st, sz, rva = struct.unpack_from('<III', data, dirrva + i * 12)
        streams[st] = (sz, rva)

    def mdstring(rva):
        ln = u32(rva)
        return data[rva + 4: rva + 4 + ln].decode('utf-16-le', 'replace')

    mods = []
    if MODULE_LIST in streams:
        _, rva = streams[MODULE_LIST]
        for i in range(u32(rva)):
            o = rva + 4 + i * 108
            mods.append((u64(o), u32(o + 8), mdstring(u32(o + 20)).split('\\')[-1]))
    mods.sort()

    def which(addr):
        for base, size, name in mods:
            if base <= addr < base + size:
                return name, addr - base
        return None, None

    # process creation time -> exact session age at capture
    created = None
    if MISC_INFO in streams:
        _, rva = streams[MISC_INFO]
        if u32(rva + 4) & 0x2:  # MINIDUMP_MISC1_PROCESS_TIMES
            created = u32(rva + 12)

    tinfo = {}
    if THREAD_INFO_LIST in streams:
        _, rva = streams[THREAD_INFO_LIST]
        hdrsz, entsz, nent = struct.unpack_from('<III', data, rva)
        for i in range(nent):
            o = rva + hdrsz + i * entsz
            ct, et, kt, ut, sa = struct.unpack_from('<QQQQQ', data, o + 16)
            tinfo[u32(o)] = dict(create=ct, kernel=kt, user=ut, start=sa)

    threads = []
    if THREAD_LIST in streams:
        _, rva = streams[THREAD_LIST]
        for i in range(u32(rva)):
            o = rva + 4 + i * 48
            tid = u32(o)
            ctxsz, ctxrva = u32(o + 40), u32(o + 44)
            threads.append(dict(
                tid=tid,
                # CONTEXT_AMD64: Rsp at 0x98, Rip at 0xF8
                rip=u64(ctxrva + 0xF8) if ctxsz >= 0x100 else 0,
                rsp=u64(ctxrva + 0x98) if ctxsz >= 0x100 else 0,
                stkstart=u64(o + 24), stksz=u32(o + 32), stkrva=u32(o + 36),
                **tinfo.get(tid, {})))

    withct = [t for t in threads if t.get('create')]
    if not withct:
        return None
    main = min(withct, key=lambda t: t['create'])

    frames = []
    base, sz, srva = main['stkstart'], main['stksz'], main['stkrva']
    p = 0
    while p + 8 <= sz:
        n, d = which(u64(srva + p))
        if n:
            frames.append((n, d))
        p += 8

    ripmod, ripoff = which(main['rip'])
    return dict(nthreads=len(threads), nmods=len(mods), main=main, frames=frames,
                age=(dumped_at - created) if created else None,
                rip=f'{ripmod}+0x{ripoff:x}' if ripmod else hex(main['rip']),
                modcount=collections.Counter(n for n, _ in frames))


HINTS = [
    ('blocking DNS resolve', lambda m: m.get('dnsapi.dll', 0) >= 3),
    ('enet transport', lambda m: m.get('enet.dll', 0) >= 3),
    ('livekit ffi', lambda m: m.get('livekit_ffi.dll', 0) >= 5),
    ('socket I/O', lambda m: m.get('ws2_32.dll', 0) + m.get('mswsock.dll', 0) >= 3),
    ('file / signature verification', lambda m: m.get('wintrust.dll', 0) >= 1),
    ('registry', lambda m: m.get('advapi32.dll', 0) >= 3),
    ('window / IME message loop',
     lambda m: sum(m.get(k, 0) for k in ('msctf.dll', 'textinputframework.dll',
                                         'uxtheme.dll', 'CoreMessaging.dll',
                                         'WindowResizeConstraint.dll')) >= 5),
    ('gpu driver', lambda m: sum(v for k, v in m.items()
                                 if k.startswith(('igd', 'nvoglv', 'nvd3d', 'amdxc', 'atidx')))>= 3),
    ('running managed code (not blocked)',
     lambda m: m.get('GameAssembly.dll', 0) >= 150),
]


def age_str(a):
    if a is None:
        return 'n/a'
    return f'{a // 3600}h {a % 3600 // 60}m' if a >= 3600 else f'{a // 60}m {a % 60}s'


paths = []
for arg in sys.argv[1:]:
    if not arg.startswith('--'):
        paths.extend(glob.glob(arg))
show_frames = '--frames' in sys.argv

if not paths:
    print(__doc__)
    sys.exit(1)

rows = []
for path in sorted(paths):
    try:
        data, inner = load(path)
        r = parse(data) if data else None
    except Exception as exc:
        print(f'{os.path.basename(path)}: unreadable ({exc})')
        continue
    if not r:
        print(f'{os.path.basename(path)}: not a parseable minidump')
        continue
    hints = [label for label, test in HINTS if test(r['modcount'])] or ['unclassified']
    rows.append((inner, r, hints))

print(f"{'dump':18s} {'session age':>12s} {'thr':>4s}  {'main-thread rip':30s} stack hints")
print('-' * 118)
for inner, r, hints in sorted(rows, key=lambda x: (x[1]['age'] is None, x[1]['age'])):
    print(f"{inner[:18]:18s} {age_str(r['age']):>12s} {r['nthreads']:>4d}  "
          f"{r['rip']:30s} {' + '.join(hints)}")

for inner, r, hints in rows:
    interesting = [(k, v) for k, v in r['modcount'].most_common()
                   if k not in ('GameAssembly.dll', 'ntdll.dll', 'UnityPlayer.dll')][:8]
    print(f"\n{inner}  ({r['nmods']} modules, {len(r['frames'])} stack pointers)")
    print('  modules on the main-thread stack: '
          + (', '.join(f'{k}={v}' for k, v in interesting) or '(none beyond Unity/managed)'))
    if show_frames:
        for n, d in r['frames'][:60]:
            print(f'    {n}+0x{d:x}')

if len(rows) > 1:
    print('\n=== aggregate ===')
    for k, v in collections.Counter(' + '.join(h) for _, _, h in rows).most_common():
        print(f'{v:3d}  {k}')
    for k, v in collections.Counter(r['rip'] for _, r, _ in rows).most_common():
        print(f'{v:3d}  rip {k}')