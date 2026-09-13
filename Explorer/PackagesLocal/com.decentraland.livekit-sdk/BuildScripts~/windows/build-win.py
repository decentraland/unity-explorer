#!/usr/bin/env python3
"""build-win.py - build livekit_ffi.dll + livekit_ffi.pdb (release, x86_64-pc-windows-msvc).

Self-contained: reads build.config.toml, clones the rust-sdks source into .src/ (gitignored,
never committed), patches [profile.release] for PDB output, runs cargo build, and places the
DLL + PDB per the config flag. Run env-setup.ps1 first to install the toolchain (incl. Python).
"""

import os
import shutil
import subprocess
import sys
import tomllib
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO_ROOT = HERE.parents[1]
PROTOC_DIR = Path(os.environ.get("LOCALAPPDATA", "")) / "protoc"  # where env-setup.ps1 put protoc
DASEL_DIR = Path(os.environ.get("LOCALAPPDATA", "")) / "dasel"    # where env-setup.ps1 put dasel


def step(msg):
    print(f"\n==> {msg}")


def info(msg):
    print(f"    {msg}")


def die(msg):
    sys.exit(f"ERROR: {msg}")


def run(args, cwd=None, env=None, fail=None):
    """Run a command, streaming its output; exit with `fail` on a nonzero code."""
    code = subprocess.run([str(a) for a in args], cwd=cwd, env=env).returncode
    if code != 0:
        die(f"{fail or 'command failed'} (exit {code})")


def find_tool(name, fallback):
    """A tool on PATH, else the copy env-setup.ps1 dropped in LOCALAPPDATA."""
    found = shutil.which(name)
    return Path(found) if found else Path(fallback)


def vcvars_env(vcvars, sdk_ver):
    """Source vcvarsall.bat and return the environment it sets.

    vcvarsall.bat is a batch script, so importing its variables means running it under cmd and
    dumping `set`; there is no native alternative. This is the build env only - unrelated to dasel.
    """
    res = subprocess.run(f'"{vcvars}" x64 {sdk_ver} >nul 2>&1 && set',
                         shell=True, capture_output=True, text=True)
    if res.returncode != 0:
        die(f"vcvarsall.bat failed: {res.stderr.strip()}")
    env = dict(os.environ)
    for line in res.stdout.splitlines():
        key, sep, val = line.partition("=")
        if sep:
            env[key] = val
    return env


def main():
    # --- 0. Config ---------------------------------------------------------
    cfg_path = HERE / "build.config.toml"
    if not cfg_path.is_file():
        die(f"config not found: {cfg_path}")
    cfg = tomllib.loads(cfg_path.read_text(encoding="utf-8"))
    tag = cfg.get("Tag")
    if not tag:
        die("Tag not set in build.config.toml")
    install_to_plugins = bool(cfg.get("InstallToPlugins", False))
    clean_source = bool(cfg.get("CleanSourceAfterBuild", False))

    src_cfg = cfg.get("SourceDir") or ".src"                       # clone root from config
    src_root = Path(src_cfg) if Path(src_cfg).is_absolute() else HERE / src_cfg
    repo = src_root / ("rust-sdks-" + tag.replace("/", "-").replace("\\", "-"))

    # --- 1. Ensure toolchain ----------------------------------------------
    step("Ensure toolchain")
    git = shutil.which("git")
    if not git:
        die("git not found - run env-setup.ps1 first.")
    cargo = find_tool("cargo", Path(os.environ["USERPROFILE"]) / ".cargo" / "bin" / "cargo.exe")
    if not cargo.exists():
        die("cargo not found - run env-setup.ps1 first.")
    # dasel patches Cargo.toml's [profile.release]. Prefer one on PATH, else env-setup's copy.
    dasel = find_tool("dasel", DASEL_DIR / "dasel.exe")
    if not dasel.exists():
        die("dasel not found - run env-setup.ps1 first.")

    # Locate VS2022 + vcvarsall via vswhere.
    vswhere = Path(os.environ["ProgramFiles(x86)"]) / "Microsoft Visual Studio" / "Installer" / "vswhere.exe"
    vs_path = subprocess.run(
        [str(vswhere), "-latest", "-products", "*",
         "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
         "-property", "installationPath"],
        capture_output=True, text=True).stdout.strip()
    if not vs_path:
        die("VS2022 C++ tools not found - run env-setup.ps1 first.")
    vcvars = Path(vs_path) / "VC" / "Auxiliary" / "Build" / "vcvarsall.bat"

    # Newest installed Windows SDK (build against the Win11 SDK headers).
    sdk_inc = Path(os.environ["ProgramFiles(x86)"]) / "Windows Kits" / "10" / "Include"
    sdks = sorted(p.name for p in sdk_inc.iterdir() if p.is_dir() and p.name.startswith("10.")) if sdk_inc.is_dir() else []
    if not sdks:
        die("no Windows 10/11 SDK found - run env-setup.ps1 first.")
    sdk_ver = sdks[-1]

    # libclang + protoc: env vars consumed by the rust build scripts. We are already in Python,
    # so locate libclang by importing the clang package instead of shelling back out to it.
    try:
        import clang
        libclang_path = Path(clang.__file__).parent / "native"
    except ImportError:
        die("libclang (python 'clang' package) not found - run env-setup.ps1 first.")
    protoc = PROTOC_DIR / "bin" / "protoc.exe"
    if not libclang_path.exists():
        die("libclang not found - run env-setup.ps1 first.")
    if not protoc.exists():
        die("protoc not found - run env-setup.ps1 first.")

    info(f"tag:           {tag}")
    info(f"source:        {repo}")
    info(f"install:       {'Runtime/Plugins/ffi-windows-x86_64' if install_to_plugins else 'tool folder'}")
    info(f"cargo:         {cargo}")
    info(f"vcvars:        {vcvars}")
    info(f"WinSDK:        {sdk_ver}")
    info(f"LIBCLANG_PATH: {libclang_path}")
    info(f"PROTOC:        {protoc}")
    info(f"dasel:         {dasel}")

    # --- 2. Download source for the tag (into .src/, with nested submodules) ---
    # git clone --recurse-submodules pulls yuv-sys/libyuv + livekit-protocol/protocol cleanly;
    # webrtc is downloaded later by livekit-ffi/build.rs. Per-tag folder, reused on re-run.
    # core.longpaths handles webrtc's deep paths nested under this repo folder.
    step(f"Download rust-sdks source ({tag})")
    src_root.mkdir(parents=True, exist_ok=True)
    if (repo / ".git").is_dir():
        info(f"reusing existing checkout: {repo}")
    else:
        if repo.exists():
            shutil.rmtree(repo)                                    # clear a partial/failed checkout
        run([git, "-c", "core.longpaths=true", "clone", "--depth", "1", "--branch", tag,
             "--recurse-submodules", "--shallow-submodules",
             "https://github.com/livekit/rust-sdks.git", repo],
            fail=f"git clone failed for tag {tag}")
        info(f"source at: {repo}")
    libyuv = repo / "yuv-sys" / "libyuv" / "include" / "libyuv"
    if not libyuv.exists():
        run([git, "-C", repo, "submodule", "update", "--init", "--recursive"])
    if not libyuv.exists():
        die("libyuv submodule not populated")

    # --- 3. Patch [profile.release] (traceable build with PDB) -------------
    # Overlay Patch.toml's [profile.release] onto the downloaded Cargo.toml with dasel (see Patch.toml
    # for what each flag does and why). dasel rewrites Cargo.toml (reorders/quotes/drops comments), but
    # .src/ is a throwaway clone. dasel reads the source toml from stdin; we hand it the open file so
    # no shell redirect is needed.
    step("Patch [profile.release]")
    cargo_toml = repo / "Cargo.toml"
    patch_toml = HERE / "Patch.toml"
    with open(cargo_toml, "rb") as stdin_file:
        res = subprocess.run(
            [str(dasel), "-i", "toml", "-o", "toml",
             "--var", f"patch=toml:file:{patch_toml}",
             "profile.release = $patch.profile.release", "--root"],
            stdin=stdin_file, capture_output=True)
    if res.returncode != 0 or not res.stdout.strip():
        die(f"dasel failed to patch [profile.release] (does {tag} define one?): "
            f"{res.stderr.decode('utf-8', 'replace').strip()}")
    # Write back the dasel output verbatim (already UTF-8, no BOM), with one trailing LF.
    cargo_toml.write_bytes(res.stdout.rstrip() + b"\n")
    info(f"patched {cargo_toml} via dasel")

    # --- 4. Build ----------------------------------------------------------
    step("cargo build --release -p livekit-ffi")
    env = vcvars_env(vcvars, sdk_ver)
    env["LIBCLANG_PATH"] = str(libclang_path)
    env["PROTOC"] = str(protoc)
    env["PATH"] = str(Path(os.environ["USERPROFILE"]) / ".cargo" / "bin") + os.pathsep + env.get("PATH", "")
    run([cargo, "build", "--release", "-p", "livekit-ffi"], cwd=repo, env=env, fail="cargo build failed")

    # --- 5. Place output ---------------------------------------------------
    rel = repo / "target" / "release"
    dll, pdb = rel / "livekit_ffi.dll", rel / "livekit_ffi.pdb"
    if not dll.exists():
        die(f"build produced no DLL at {dll}")
    if not pdb.exists():
        die(f"build produced no PDB at {pdb}")
    dest = (REPO_ROOT / "Runtime" / "Plugins" / "ffi-windows-x86_64") if install_to_plugins else HERE
    dest.mkdir(parents=True, exist_ok=True)
    shutil.copy2(dll, dest)
    shutil.copy2(pdb, dest)

    # --- 6. Clean source (optional) ----------------------------------------
    if clean_source:
        step("Clean source")
        shutil.rmtree(repo, ignore_errors=True)
        info(f"removed {repo}")

    step("Done")
    print(f"    DLL -> {dest / 'livekit_ffi.dll'}")
    print(f"    PDB -> {dest / 'livekit_ffi.pdb'}")
    if not install_to_plugins:
        print("    (InstallToPlugins is false; copy these into Runtime/Plugins/ffi-windows-x86_64/ to ship.)")


if __name__ == "__main__":
    if os.name != "nt":
        die("this build targets x86_64-pc-windows-msvc and must run on Windows.")
    main()
