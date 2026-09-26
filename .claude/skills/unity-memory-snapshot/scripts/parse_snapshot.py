#!/usr/bin/env python3
"""
Parser for Unity Memory Profiler `.snap` files (QueriedSnapshot binary format).

Provenance / attribution:
    The `.snap` file layout implemented here was derived by reading the source of the
    `com.unity.memoryprofiler` package (v1.1.11), which Unity ships to the project in
    source form under the Unity Companion License
    (https://unity3d.com/legal/licenses/unity_companion_license).
    This is an INDEPENDENT, clean-room re-implementation of the file *format* (a
    functional interface) written for use with this Unity-dependent project. No Unity
    source code, comments, or assets are reproduced here. Provided "AS IS" with no
    warranty, consistent with the UCL.

Why this exists: the `.snap` format is a proprietary chunked binary with no public
spec. This parser encodes that format (see the layout notes below) and was VALIDATED
against known captures (per-object size arrays match block totals; the No->N avatar
diff adds exactly +1 of each per-instance object per avatar). Use this instead of
opening snapshots in the Editor when you need scriptable, diff-able, headless
extraction of native-object memory by type.

=========================== FORMAT (from package source) ===========================
Header:  uint32 magic 0xAEABCDCD @ 0
Footer:  uint32 magic 0xABCDCDAE @ filelen-4 ; uint64 directoryOffset @ filelen-12
Directory @ dirOff:
    uint32 0xCDCDAEAB ; uint32 chapterVer 0x20170724 ; uint64 blockSectionOff @ dirOff+8
    int32  entryCount @ dirOff+16 ; entryCount x uint64 chapterOffsets @ dirOff+20
        (indexed by EntryType ordinal; 0 == entry absent)
Block section @ blockSectionOff:
    uint32 blockVer 0x20170724 ; int32 blockCount @ +4 ; blockCount x uint64 blockOffsets @ +8
Block @ blockOff:
    uint64 ChunkSize ; uint64 TotalBytes ; then OffsetCount x int64 chunk file-offsets @ +16
    OffsetCount = ceil(TotalBytes / ChunkSize). Block data is split into ChunkSize chunks
    scattered through the file; logical offset L -> chunk[L//ChunkSize] + (L % ChunkSize).
EntryHeader (18 bytes, pack=2) @ chapterOffset:
    uint16 Format @0 ; uint32 BlockIndex @2 ; uint32 EntriesMeta @6 ; uint64 HeaderMeta @10
    Format: 1=SingleElement, 2=ConstantSizeElementArray, 3=DynamicSizeElementArray
    ConstantSizeElementArray: count = (uint32)HeaderMeta  (LOW 32 BITS); element size = EntriesMeta
    DynamicSizeElementArray:  count = EntriesMeta; per-element start offsets read as
        EntriesMeta x int64 @ chapterOffset+18. After the package's fixup:
        offsets[0]=HeaderMeta, offsets[i]=disk[i-1] (i>=1), total=disk[count-1].
EntryType ordinals used here: NativeTypes_Name=5, NativeObjects_NativeTypeArrayIndex=7,
    NativeObjects_Name=11, NativeObjects_Size=13. (Full enum in EntryType.cs.)
====================================================================================
"""
import struct, sys, collections, argparse, json

# --- EntryType ordinals (subset; see EntryType.cs for the full list) ---
ET_NativeTypes_Name = 5
ET_NativeObjects_NativeTypeArrayIndex = 7
ET_NativeObjects_Name = 11
ET_NativeObjects_Size = 13

HEADER_MAGIC = 0xAEABCDCD
FOOTER_MAGIC = 0xABCDCDAE
DIR_MAGIC = 0xCDCDAEAB
SECTION_VER = 0x20170724


class Snapshot:
    def __init__(self, path):
        self.path = path
        with open(path, "rb") as f:
            self.data = f.read()
        self._open()

    # primitive readers
    def _u32(self, o): return struct.unpack_from("<I", self.data, o)[0]
    def _i32(self, o): return struct.unpack_from("<i", self.data, o)[0]
    def _u64(self, o): return struct.unpack_from("<Q", self.data, o)[0]
    def _i64(self, o): return struct.unpack_from("<q", self.data, o)[0]

    def _open(self):
        d = self.data
        flen = len(d)
        if self._u32(0) != HEADER_MAGIC:
            raise ValueError(f"{self.path}: not a Unity snapshot (bad header magic)")
        if self._u32(flen - 4) != FOOTER_MAGIC:
            raise ValueError(f"{self.path}: bad footer magic")
        dir_off = self._u64(flen - 12)
        if self._u32(dir_off) != DIR_MAGIC:
            raise ValueError(f"{self.path}: bad directory signature")
        if self._u32(dir_off + 4) != SECTION_VER:
            raise ValueError(f"{self.path}: unexpected chapter section version")
        block_section_off = self._u64(dir_off + 8)
        entry_count = self._i32(dir_off + 16)
        self.chapter_offsets = [self._u64(dir_off + 20 + 8 * i) for i in range(entry_count)]
        if self._u32(block_section_off) != SECTION_VER:
            raise ValueError(f"{self.path}: unexpected block section version")
        block_count = self._i32(block_section_off + 4)
        block_offsets = [self._u64(block_section_off + 8 + 8 * i) for i in range(block_count)]
        self.blocks = []
        for bo in block_offsets:
            chunk = self._u64(bo)
            total = self._u64(bo + 8)
            oc = total // chunk + (1 if total % chunk else 0)
            coffs = [self._u64(bo + 16 + 8 * i) for i in range(oc)]
            self.blocks.append((chunk, coffs))

    def _block_read(self, bi, start, length):
        chunk, coffs = self.blocks[bi]
        out = bytearray()
        L, rem = start, length
        while rem > 0:
            ci, within = divmod(L, chunk)
            n = min(rem, chunk - within)
            fp = coffs[ci] + within
            out += self.data[fp:fp + n]
            L += n
            rem -= n
        return bytes(out)

    def _header(self, et):
        off = self.chapter_offsets[et] if et < len(self.chapter_offsets) else 0
        if off == 0:
            return None
        return dict(
            off=off,
            fmt=struct.unpack_from("<H", self.data, off)[0],
            blk=struct.unpack_from("<I", self.data, off + 2)[0],
            em=struct.unpack_from("<I", self.data, off + 6)[0],
            hm=struct.unpack_from("<Q", self.data, off + 10)[0],
        )

    def const_array_ints(self, et):
        """ConstantSizeElementArray of little-endian unsigned ints -> list[int]."""
        h = self._header(et)
        if not h or h["fmt"] != 2:
            return None
        count = h["hm"] & 0xFFFFFFFF
        esize = h["em"]
        raw = self._block_read(h["blk"], 0, count * esize)
        return [int.from_bytes(raw[i * esize:(i + 1) * esize], "little") for i in range(count)]

    def dynamic_array_bytes(self, et):
        """DynamicSizeElementArray -> list[bytes] (e.g. name strings)."""
        h = self._header(et)
        if not h or h["fmt"] != 3:
            return None
        count = h["em"]
        disk = [self._i64(h["off"] + 18 + 8 * i) for i in range(count)]
        offs = [0] * (count + 1)
        offs[0] = h["hm"]
        for i in range(1, count):
            offs[i] = disk[i - 1]
        offs[count] = disk[count - 1] if count > 0 else h["hm"]
        return [self._block_read(h["blk"], offs[i], offs[i + 1] - offs[i]) for i in range(count)]

    def type_names(self):
        return [b.decode("utf-8", "replace") for b in (self.dynamic_array_bytes(ET_NativeTypes_Name) or [])]

    def native_objects_by_type(self):
        """Returns {type_name: [count, total_bytes]}, plus (num_objects, total_bytes)."""
        names = self.type_names()
        tidx = self.const_array_ints(ET_NativeObjects_NativeTypeArrayIndex) or []
        sizes = self.const_array_ints(ET_NativeObjects_Size) or []
        agg = collections.defaultdict(lambda: [0, 0])
        for t, s in zip(tidx, sizes):
            n = names[t] if 0 <= t < len(names) else f"<type {t}>"
            agg[n][0] += 1
            agg[n][1] += s
        return dict(agg), len(sizes), sum(sizes)


MB = 1024 * 1024


def cmd_summary(args):
    snap = Snapshot(args.file)
    agg, nobj, total = snap.native_objects_by_type()
    rows = sorted(agg.items(), key=lambda kv: -kv[1][1])
    if args.top:
        rows = rows[:args.top]
    if args.json:
        print(json.dumps({"file": args.file, "num_objects": nobj, "total_bytes": total,
                          "types": {k: {"count": c, "bytes": b} for k, (c, b) in rows}}, indent=2))
        return
    print(f"{args.file}: {nobj:,} native objects, {total/MB:,.1f} MB total native object size\n")
    print(f"{'Type':36}{'Count':>10}{'Total MB':>12}")
    for name, (c, b) in rows:
        print(f"{name[:36]:36}{c:>10,}{b/MB:>12.2f}")


def cmd_diff(args):
    a = Snapshot(args.file_a); b = Snapshot(args.file_b)
    aa, na, ta = a.native_objects_by_type()
    bb, nb, tb = b.native_objects_by_type()
    print(f"A {args.file_a}: {na:,} objs, {ta/MB:,.1f} MB")
    print(f"B {args.file_b}: {nb:,} objs, {tb/MB:,.1f} MB")
    print(f"delta: {na-nb:+,} objs, {(ta-tb)/MB:+,.1f} MB native\n")
    keys = set(aa) | set(bb)
    rows = [(k, aa.get(k, [0, 0])[0] - bb.get(k, [0, 0])[0],
             aa.get(k, [0, 0])[1] - bb.get(k, [0, 0])[1]) for k in keys]
    rows.sort(key=lambda r: -r[2])
    if args.top:
        rows = rows[:args.top]
    per = f" / {args.per}" if args.per else ""
    hdr = f"{'Type':30}{'dCount':>10}{'dMB':>10}"
    if args.per:
        hdr += f"{'MB' + per:>12}"
    print(hdr)
    for k, dc, ds in rows:
        line = f"{k[:30]:30}{dc:>10,}{ds/MB:>10.2f}"
        if args.per:
            line += f"{ds/MB/args.per:>12.4f}"
        print(line)


def cmd_scale(args):
    """Category x snapshot matrix. Pass label=path pairs."""
    pairs = []
    for spec in args.specs:
        label, _, path = spec.partition("=")
        pairs.append((label, path if path else label))
    data = {}
    for label, path in pairs:
        agg, n, t = Snapshot(path).native_objects_by_type()
        data[label] = (agg, n, t)
    cats = args.categories.split(",") if args.categories else None
    if not cats:
        # union of top types across all
        allt = collections.defaultdict(float)
        for agg, _, _ in data.values():
            for k, (c, b) in agg.items():
                allt[k] += b
        cats = [k for k, _ in sorted(allt.items(), key=lambda kv: -kv[1])[:args.top]]
    labels = [l for l, _ in pairs]
    print(f"{'Category (MB)':28}" + "".join(f"{l:>10}" for l in labels))
    for c in cats:
        print(f"{c[:28]:28}" + "".join(f"{data[l][0].get(c, [0,0])[1]/MB:>10.1f}" for l in labels))
    print(f"{'TOTAL native MB':28}" + "".join(f"{data[l][2]/MB:>10.0f}" for l in labels))
    print(f"{'object count':28}" + "".join(f"{data[l][1]:>10,}" for l in labels))


def main():
    p = argparse.ArgumentParser(description="Parse Unity Memory Profiler .snap files.")
    sub = p.add_subparsers(dest="cmd", required=True)

    s = sub.add_parser("summary", help="per-type native memory for one snapshot")
    s.add_argument("file")
    s.add_argument("--top", type=int, default=30)
    s.add_argument("--json", action="store_true")
    s.set_defaults(func=cmd_summary)

    d = sub.add_parser("diff", help="per-type delta between two snapshots (A - B)")
    d.add_argument("file_a"); d.add_argument("file_b")
    d.add_argument("--top", type=int, default=25)
    d.add_argument("--per", type=int, default=0, help="divide deltas by N (e.g. avatar count)")
    d.set_defaults(func=cmd_diff)

    sc = sub.add_parser("scale", help="category x snapshot matrix across many files")
    sc.add_argument("specs", nargs="+", help="label=path (or just path)")
    sc.add_argument("--categories", help="comma-separated type names; default = top by size")
    sc.add_argument("--top", type=int, default=15)
    sc.set_defaults(func=cmd_scale)

    args = p.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
