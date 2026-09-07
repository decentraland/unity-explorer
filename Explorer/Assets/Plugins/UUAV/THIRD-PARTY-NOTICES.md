# Third-party notices — UUAV native binaries (Linux)

## FFmpeg

The Linux plugin folder `Packages/UUAV/Runtime/Plugins/linux-x86_64/` ships the
following FFmpeg shared libraries, used by `libuuav.so`, `libuuav_core.so` and
`uuav-helper` as dynamically-linked libraries:

```
libavcodec.so.62
libavdevice.so.62
libavfilter.so.11
libavformat.so.62
libavutil.so.60
libswresample.so.6
libswscale.so.9
```

- **Version:** FFmpeg **n8.1** (the libraries embed `FFmpeg version n8.1`;
  libavcodec reports Lavc 62.28.100).
- **License:** GNU Lesser General Public License v2.1 or later
  (**LGPL-2.1+**). The build enables neither `--enable-gpl` nor
  `--enable-nonfree`, so the libraries are pure LGPL. The full license text
  ships next to this file as [`COPYING.LGPLv2.1`](COPYING.LGPLv2.1).
- **Build configuration** (embedded in each library; verify with
  `strings libavutil.so.60 | grep -- --prefix`):

```
--prefix=/buildenv/ffmpeg-build-prefix/Explorer/Assets/Plugins/UUAV/native/.third_party/ffmpeg --pkg-config-flags=--static --enable-shared --disable-static --disable-programs --disable-doc --disable-devices --disable-alsa --disable-sndio --disable-xlib --disable-libxcb --disable-bzlib --disable-lzma --disable-vdpau --disable-cuda-llvm --disable-cuvid --disable-nvdec --disable-nvenc --disable-opencl --disable-vulkan --disable-amf --enable-vaapi --enable-libdrm --enable-openssl --enable-libxml2
```

- **Complete corresponding source:** the FFmpeg n8.1 release, available from
  <https://ffmpeg.org/download.html> (git tag `n8.1` at
  <https://git.ffmpeg.org/ffmpeg.git>), together with the configure line above
  and the build recipe
  [`native/scripts/build-ffmpeg-linux.sh`](native/scripts/build-ffmpeg-linux.sh)
  in this repository.
- **Rebuilding:** these seven libraries are also rebuilt from the pinned FFmpeg
  `n8.1` source by
  [`scripts/native-provenance/drv/ffmpeg/default.nix`](../../../../scripts/native-provenance/drv/ffmpeg/default.nix),
  which applies the configure flag set above verbatim (`--prefix` excepted) and
  reproduces the `$ORIGIN` runpath and the dependency policy. Run
  `nix-build scripts/native-provenance/drv/ffmpeg/default.nix` from the
  repository root. The result is not byte-identical to the libraries shipped
  here — it links the OpenSSL and libxml2 versions the build resolves rather
  than the versions listed below — but the exported dynamic symbol table
  of each of the seven libraries is identical, so the rebuild is a drop-in
  replacement.
- **Relinking freedom:** the FFmpeg libraries ship as separate shared objects
  and are loaded dynamically; nothing in this plugin links them statically.
  A user may replace them with their own compatible build of FFmpeg (same
  library majors: avcodec 62 / avutil 60 line) and the plugin will use it —
  the relinking permission required by LGPL-2.1 section 6 is preserved.
- **Integrity:** sha256 pins for the prebuilt UUAV binaries live in
  [`scripts/uuav/uuav-binaries.lock.json`](../../../../scripts/uuav/uuav-binaries.lock.json),
  verified by `scripts/uuav/verify-binaries.py`; that lock's `targets` pin the
  macOS and Windows binary sets. The Linux set is pinned instead by
  [`scripts/native-provenance/PROVENANCE.lock`](../../../../scripts/native-provenance/PROVENANCE.lock),
  which records each library's sha256 next to the rebuild verdict for it.

### Libraries statically linked into the FFmpeg shared objects

Because the FFmpeg build uses `--pkg-config-flags=--static`, its own support
dependencies are linked into the `.so` files above rather than shipped
separately:

- **OpenSSL 3.5.1** (Apache License 2.0) — TLS for network playback; the
  version string `OpenSSL 3.5.1` is embedded in `libavformat.so.62`.
- **libxml2 2.13.8** (MIT license) — DASH manifest parsing
  (`--enable-libxml2`).

Both are built static and position-independent and linked in, which is what
`--pkg-config-flags=--static` selects. The rebuild described above does the
same, from whichever OpenSSL and libxml2 sources it resolves; those versions
therefore differ from the two above.

VA-API hardware decoding (`--enable-vaapi --enable-libdrm`) links `libva` and
`libdrm` dynamically from the user's system; those libraries are not shipped.
