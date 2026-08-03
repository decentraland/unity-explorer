#!/usr/bin/env python3
"""Tests for native/scripts/doctor-libs-windows.py - build.sh's Windows gate.

Run: python3 scripts/uuav/test-doctor-libs-windows.py

Drives the doctor as a subprocess over synthetic PE32+ images built here, so
the gate's three checks (CFG bit, static CRT, av_uuav_fetch_register export)
are each proven to fail the build when their property is absent, with no
Windows toolchain anywhere near the test.
"""

from __future__ import annotations

import os
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DOCTOR = os.path.join(REPO, "Explorer", "Assets", "Plugins", "UUAV", "native",
                      "scripts", "doctor-libs-windows.py")

GUARD_CF = 0x4000
SECTION_RVA = 0x1000
SECTION_RAW = 0x200


def build_pe(dll_characteristics: int, exports: list[str],
             imports: list[str], dll_name: str = "test.dll") -> bytes:
    """A minimal but structurally honest PE32+ DLL image."""
    data = bytearray()

    def alloc(blob: bytes) -> int:
        rva = SECTION_RVA + len(data)
        data.extend(blob)
        return rva

    def alloc_cstr(text: str) -> int:
        return alloc(text.encode("ascii") + b"\0")

    export_rva = export_size = 0
    if exports:
        name_rvas = [alloc_cstr(name) for name in exports]
        dllname_rva = alloc_cstr(dll_name)
        funcs_rva = alloc(struct.pack(f"<{len(exports)}I",
                                      *([0x1F00] * len(exports))))
        names_rva = alloc(struct.pack(f"<{len(exports)}I", *name_rvas))
        ords_rva = alloc(struct.pack(f"<{len(exports)}H",
                                     *range(len(exports))))
        export_rva = alloc(struct.pack(
            "<IIHHIIIIIII", 0, 0, 0, 0, dllname_rva, 1,
            len(exports), len(exports), funcs_rva, names_rva, ords_rva))
        export_size = 40

    import_rva = import_size = 0
    if imports:
        descriptors = []
        for imported in imports:
            thunk = struct.pack("<QQ", 0x8000000000000001, 0)
            ilt_rva = alloc(thunk)
            iat_rva = alloc(thunk)
            name_rva = alloc_cstr(imported)
            descriptors.append(struct.pack("<IIIII", ilt_rva, 0, 0,
                                           name_rva, iat_rva))
        descriptors.append(b"\0" * 20)
        import_rva = alloc(b"".join(descriptors))
        import_size = 20 * len(imports) + 20

    raw_size = (len(data) + SECTION_RAW - 1) // SECTION_RAW * SECTION_RAW
    virtual_size = len(data) or 1
    image_size = SECTION_RVA + (virtual_size + 0xFFF) // 0x1000 * 0x1000

    directories = [(0, 0)] * 16
    directories[0] = (export_rva, export_size)
    directories[1] = (import_rva, import_size)

    optional = struct.pack(
        "<HBBIIIIIQIIHHHHHHIIIIHHQQQQII",
        0x20B, 14, 0,
        0, len(data), 0,
        0, SECTION_RVA,
        0x180000000,
        0x1000, SECTION_RAW,
        6, 0, 0, 0, 6, 0,
        0, image_size, SECTION_RAW, 0,
        2, dll_characteristics,
        0x100000, 0x1000, 0x100000, 0x1000,
        0, 16)
    optional += b"".join(struct.pack("<II", rva, size)
                         for rva, size in directories)

    coff = struct.pack("<HHIIIHH", 0x8664, 1, 0, 0, 0, len(optional), 0x2022)
    section = struct.pack("<8sIIIIIIHHI", b".rdata\0\0", virtual_size,
                          SECTION_RVA, raw_size, SECTION_RAW, 0, 0, 0, 0,
                          0x40000040)

    dos = bytearray(0x80)
    dos[:2] = b"MZ"
    struct.pack_into("<I", dos, 0x3C, 0x80)
    headers = bytes(dos) + b"PE\0\0" + coff + optional + section
    assert len(headers) <= SECTION_RAW, "headers overflowed the raw pointer"
    headers = headers.ljust(SECTION_RAW, b"\0")
    return headers + bytes(data).ljust(raw_size, b"\0")


def good_avformat() -> bytes:
    return build_pe(GUARD_CF, ["av_uuav_fetch_register", "avformat_open_input"],
                    ["KERNEL32.dll"], "avformat-62.dll")


class DoctorTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix="uuav-doctor-test-")

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def write(self, name: str, blob: bytes):
        with open(os.path.join(self.dir, name), "wb") as handle:
            handle.write(blob)

    def run_doctor(self) -> subprocess.CompletedProcess:
        return subprocess.run([sys.executable, DOCTOR, self.dir],
                              capture_output=True, text=True)

    def test_healthy_deploy_passes(self):
        self.write("avformat-62.dll", good_avformat())
        self.write("uuav.dll", build_pe(GUARD_CF, ["uuav_init"],
                                        ["KERNEL32.dll"], "uuav.dll"))
        self.write("uuav-adapter.exe", build_pe(GUARD_CF, [],
                                                ["KERNEL32.dll"]))
        result = self.run_doctor()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_module_without_cfg_fails(self):
        self.write("avformat-62.dll", good_avformat())
        self.write("uuav.dll", build_pe(0, ["uuav_init"], ["KERNEL32.dll"]))
        result = self.run_doctor()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("Control Flow Guard", result.stdout)
        self.assertIn("uuav.dll", result.stdout)

    def test_dynamic_crt_import_fails(self):
        self.write("avformat-62.dll", good_avformat())
        self.write("uuav.dll", build_pe(GUARD_CF, ["uuav_init"],
                                        ["KERNEL32.dll", "VCRUNTIME140.dll"]))
        result = self.run_doctor()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("VCRUNTIME140.dll", result.stdout)

    def test_avformat_without_fetch_export_fails(self):
        """The committed pre-parent-fetch avformat is exactly this case: CFG
        and static CRT are fine, but the adapter's one required hook is not
        exported."""
        self.write("avformat-62.dll",
                   build_pe(GUARD_CF, ["avformat_open_input"],
                            ["KERNEL32.dll"], "avformat-62.dll"))
        result = self.run_doctor()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("av_uuav_fetch_register", result.stdout)

    def test_missing_avformat_fails(self):
        self.write("uuav.dll", build_pe(GUARD_CF, ["uuav_init"],
                                        ["KERNEL32.dll"]))
        result = self.run_doctor()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("avformat", result.stdout)

    def test_lfs_pointer_fails(self):
        self.write("avformat-62.dll", good_avformat())
        self.write("uuav.dll",
                   b"version https://git-lfs.github.com/spec/v1\n"
                   b"oid sha256:" + b"0" * 64 + b"\nsize 1\n")
        result = self.run_doctor()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("LFS pointer", result.stdout)

    def test_empty_directory_fails(self):
        result = self.run_doctor()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main(verbosity=2)
