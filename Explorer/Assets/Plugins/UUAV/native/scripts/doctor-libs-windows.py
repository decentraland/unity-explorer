#!/usr/bin/env python3
"""Doctor for the deployed Windows plugin directory - build.sh's hard gate,
mirroring the macOS branch's doctor-libs.sh.

For every deployed module (*.dll, *.exe):

  cfg   IMAGE_DLLCHARACTERISTICS_GUARD_CF in the PE optional header - the bit
        the OS loader keys on and dumpbin /headers prints as "Control Flow
        Guard". CFG is a whole-process property: one uninstrumented module
        silently defeats it for everyone (see the header of build.sh), so a
        single DLL that lost -guard:cf / control-flow-guard=yes fails the
        build here instead of shipping.
  crt   no import of VCRUNTIME*/MSVCP*/api-ms-win-crt-*/ucrtbase* - the
        static-CRT contract from native/.cargo/config.toml: the plugin folder
        works without a VC++ redistributable, so one dynamically-linked module
        would be the only thing in the player demanding one.

And once per directory:

  fetch  an avformat-*.dll is present and exports av_uuav_fetch_register -
         the hook uuav-adapter binds its parent-fetch stubs with. A stock or
         pre-parent-fetch FFmpeg lacks it and the adapter cannot link, so a
         wrong avformat fails here rather than at first playback.

The PE is parsed directly (stdlib only): the gate must run under MSYS2 bash
and in CI without locating dumpbin.

Usage: doctor-libs-windows.py DEPLOY_DIR
Exit code: 0 if all checks pass, 1 otherwise.
"""

from __future__ import annotations

import os
import struct
import sys

GUARD_CF = 0x4000  # IMAGE_DLLCHARACTERISTICS_GUARD_CF
MACHINE_AMD64 = 0x8664
PE32_PLUS = 0x20B
CRT_IMPORT_PREFIXES = ("vcruntime", "msvcp", "api-ms-win-crt-", "ucrtbase")
FETCH_EXPORT = "av_uuav_fetch_register"


class PEError(Exception):
    pass


def read_cstr(blob: bytes, offset: int) -> str:
    end = blob.index(b"\0", offset)
    return blob[offset:end].decode("ascii", "replace")


class Module:
    def __init__(self, path: str):
        with open(path, "rb") as handle:
            self.blob = handle.read()
        blob = self.blob
        if blob[:2] != b"MZ":
            head = blob[:40]
            if head.startswith(b"version https://git-lfs"):
                raise PEError("unfetched Git LFS pointer, not a binary")
            raise PEError("not a PE image (no MZ header)")

        pe = struct.unpack_from("<I", blob, 0x3C)[0]
        if blob[pe:pe + 4] != b"PE\0\0":
            raise PEError("no PE signature")
        machine, nsections, _, _, _, opt_size, _ = \
            struct.unpack_from("<HHIIIHH", blob, pe + 4)
        if machine != MACHINE_AMD64:
            raise PEError(f"machine 0x{machine:04x}, not x86_64")

        opt = pe + 24
        if struct.unpack_from("<H", blob, opt)[0] != PE32_PLUS:
            raise PEError("not PE32+")
        self.dll_characteristics = struct.unpack_from("<H", blob, opt + 70)[0]
        ndirs = struct.unpack_from("<I", blob, opt + 108)[0]
        self.directories = [struct.unpack_from("<II", blob, opt + 112 + 8 * i)
                            for i in range(min(ndirs, 16))]
        self.sections = []
        table = opt + opt_size
        for i in range(nsections):
            _, vsize, vaddr, rawsize, rawptr = \
                struct.unpack_from("<8sIIII", blob, table + 40 * i)
            self.sections.append((vaddr, max(vsize, rawsize), rawptr))

    def offset(self, rva: int) -> int:
        for vaddr, span, rawptr in self.sections:
            if vaddr <= rva < vaddr + span:
                return rawptr + (rva - vaddr)
        raise PEError(f"RVA 0x{rva:x} lands in no section")

    def directory(self, index: int) -> tuple[int, int]:
        if index >= len(self.directories):
            return (0, 0)
        return self.directories[index]

    def imports(self) -> list[str]:
        rva, _ = self.directory(1)
        if rva == 0:
            return []
        names = []
        offset = self.offset(rva)
        while True:
            ilt, _, _, name_rva, iat = struct.unpack_from("<IIIII", self.blob,
                                                          offset)
            if ilt == 0 and name_rva == 0 and iat == 0:
                break
            names.append(read_cstr(self.blob, self.offset(name_rva)))
            offset += 20
        return names

    def exports(self) -> list[str]:
        rva, _ = self.directory(0)
        if rva == 0:
            return []
        offset = self.offset(rva)
        (_, _, _, _, _, _, _, nnames, _, names_rva, _) = \
            struct.unpack_from("<IIHHIIIIIII", self.blob, offset)
        names_offset = self.offset(names_rva)
        out = []
        for i in range(nnames):
            name_rva = struct.unpack_from("<I", self.blob,
                                          names_offset + 4 * i)[0]
            out.append(read_cstr(self.blob, self.offset(name_rva)))
        return out


def doctor(directory: str) -> int:
    modules = sorted(name for name in os.listdir(directory)
                     if name.endswith((".dll", ".exe")))
    if not modules:
        print(f"FAIL {directory} holds no .dll/.exe to check")
        return 1

    fail = 0
    avformat_seen = False
    for name in modules:
        path = os.path.join(directory, name)
        try:
            module = Module(path)
        except PEError as error:
            print(f"FAIL {name:<24} {error}")
            fail = 1
            continue

        faults = []
        if not module.dll_characteristics & GUARD_CF:
            faults.append("no Control Flow Guard (built without -guard:cf / "
                          "control-flow-guard=yes)")
        try:
            crt = sorted({imported for imported in module.imports()
                          if imported.lower().startswith(CRT_IMPORT_PREFIXES)})
        except PEError as error:
            crt = []
            faults.append(f"unreadable import table ({error})")
        if crt:
            faults.append(f"imports the dynamic CRT ({', '.join(crt)}); the "
                          f"plugin ships no redistributable")

        if name.startswith("avformat-"):
            avformat_seen = True
            try:
                if FETCH_EXPORT not in module.exports():
                    faults.append(f"does not export {FETCH_EXPORT}; "
                                  f"uuav-adapter cannot bind its parent-fetch "
                                  f"stubs to this FFmpeg (rebuild via "
                                  f"scripts/build-ffmpeg-windows.cmd)")
            except PEError as error:
                faults.append(f"unreadable export table ({error})")

        if faults:
            fail = 1
            print(f"FAIL {name:<24} {'; '.join(faults)}")
        else:
            detail = "CFG, static CRT"
            if name.startswith("avformat-"):
                detail += f", exports {FETCH_EXPORT}"
            print(f"OK   {name:<24} {detail}")

    if not avformat_seen:
        print(f"FAIL {'avformat-*.dll':<24} missing - the FFmpeg deploy is "
              f"incomplete")
        fail = 1
    return fail


def main() -> int:
    if len(sys.argv) != 2 or not os.path.isdir(sys.argv[1]):
        print(f"usage: {os.path.basename(sys.argv[0])} DEPLOY_DIR",
              file=sys.stderr)
        return 2
    return doctor(sys.argv[1])


if __name__ == "__main__":
    sys.exit(main())
