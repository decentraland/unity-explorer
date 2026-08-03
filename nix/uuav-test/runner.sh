#!/usr/bin/env bash
# The UUAV media sandbox harness: stand up every format and protocol case the
# plugin can encounter, then launch the SDK7 scene that plays them.
#
# Nix substitutes the @tool@ placeholders; everything else is resolved at run
# time so one build serves from any address.
set -euo pipefail

MEDIA_STORE="@media@"
SCENE_STORE="@scene@"
LIBEXEC="@libexec@"
NGINX_PKG="@nginx@"

STATE="${UUAV_TEST_STATE:-${XDG_STATE_HOME:-$HOME/.local/state}/uuav-test}"
RUN="$STATE/run"
PKI="$STATE/pki"
SCENE="$STATE/scene"

BIND="${UUAV_TEST_BIND:-0.0.0.0}"
HTTPS_PORT="${UUAV_TEST_HTTPS_PORT:-8443}"
HTTP_PORT="${UUAV_TEST_HTTP_PORT:-8080}"
STALL_PORT="${UUAV_TEST_STALL_PORT:-8444}"
SCENE_PORT="${UUAV_TEST_SCENE_PORT:-8000}"
EXTRA_SAN="${UUAV_TEST_EXTRA_SAN:-}"

MODE=serve-and-play
for arg in "$@"; do
    case "$arg" in
        --serve-only) MODE=serve-only ;;
        --check) MODE=check ;;
        --print-manifest) MODE=print-manifest ;;
        --trust) MODE=trust ;;
        -h | --help)
            sed -n '2,8p' "$0"
            cat <<'USAGE'

  (no flag)          serve everything and launch the scene through dcl-one-sdk
  --serve-only       serve everything, do not launch the scene
  --check            serve, run the ffprobe regression check, tear down, exit
  --print-manifest   print the case manifest for the current settings and exit
  --trust            print how to install the local CA on this and other OSes

Environment: UUAV_TEST_{HOST,BIND,HTTPS_PORT,HTTP_PORT,STALL_PORT,SCENE_PORT,
STATE,EXTRA_SAN}, DCL_ONE_SDK_BIN, UUAV_TEST_SDK_FLAKE.
USAGE
            exit 0
            ;;
        *)
            echo "uuav-test: unknown argument $arg (try --help)" >&2
            exit 2
            ;;
    esac
done

# Everything informational goes to stderr so --print-manifest emits nothing but
# the manifest and can be piped straight into jq.
log() { printf '\033[36muuav-test\033[0m %s\n' "$*" >&2; }
die() {
    printf '\033[31muuav-test error\033[0m %s\n' "$*" >&2
    exit 1
}

# ---------------------------------------------------------------- host choice
#
# The certificate carries a DNS name and the loopback addresses. The DNS name is
# the supported path: FFmpeg's SecureTransport backend on macOS matches the url
# host against the certificate through SSLSetPeerDomainName, whose IP-literal
# handling is unreliable. So prefer media.uuav.test when it resolves, and say
# out loud when falling back to the address.
resolve_host() {
    if [ -n "${UUAV_TEST_HOST:-}" ]; then
        HOST="$UUAV_TEST_HOST"
        HOST_SOURCE="UUAV_TEST_HOST"
        return
    fi
    # python3 rather than getent: getent does not exist on macOS, and the
    # harness has to make the same choice there.
    if python3 -c 'import socket,sys; socket.getaddrinfo("media.uuav.test", None)' >/dev/null 2>&1; then
        HOST="media.uuav.test"
        HOST_SOURCE="hosts entry"
        return
    fi
    HOST="127.0.0.1"
    HOST_SOURCE="fallback"
}

# ------------------------------------------------------------------- pki/trust
trust_help() {
    cat <<EOF
Local CA: $PKI/ca.pem  (DER copy: $PKI/ca.crt)

The retail plugin allows only https, so every media url in this harness is
https, and a self-signed leaf is not enough: SecureTransport (macOS) and
Schannel (Windows) both validate against the OS trust store. Install this one
root there once; the leaf is reissued automatically and needs no further steps.

  NixOS (declarative, preferred):
      security.pki.certificateFiles = [ "$PKI/ca.pem" ];
    then: sudo nixos-rebuild switch
    Or use the module in nix/uuav-test/nixos-module.nix, which does this for you.

  Other Linux (Debian/Ubuntu/Fedora):
      sudo cp $PKI/ca.pem /usr/local/share/ca-certificates/uuav-test.crt
      sudo update-ca-certificates          # dnf: sudo update-ca-trust

  macOS - this is the step the Unity client needs, FFmpeg's SecureTransport
  backend reads the System keychain:
      sudo security add-trusted-cert -d -r trustRoot \\
          -k /Library/Keychains/System.keychain $PKI/ca.pem
    verify: security verify-cert -c $PKI/server.pem
    remove: sudo security delete-certificate -c "UUAV media test harness local CA" \\
          /Library/Keychains/System.keychain

  Windows - elevated PowerShell, LocalMachine\\Root is what Schannel reads:
      Import-Certificate -FilePath .\\ca.pem -CertStoreLocation Cert:\\LocalMachine\\Root
    or:  certutil -addstore -f Root ca.pem
    verify: Get-ChildItem Cert:\\LocalMachine\\Root | Where-Object Subject -match uuav
    remove: certutil -delstore Root "UUAV media test harness local CA"

Name resolution - do this on every machine that will run the client, so the url
host matches the certificate's DNS name:

  Linux/macOS:  echo "<harness-ip>  media.uuav.test" | sudo tee -a /etc/hosts
  Windows:      add "<harness-ip>  media.uuav.test" to
                C:\\Windows\\System32\\drivers\\etc\\hosts  (elevated editor)

<harness-ip> is 127.0.0.1 when the harness runs on the same machine as the
client, otherwise the harness box's LAN address. When the client is on another
machine, that address must also be in the certificate:
  UUAV_TEST_EXTRA_SAN=<harness-ip>,<harness-hostname> nix run .#uuav-test
EOF
}

# ------------------------------------------------------------------- staging
stage_media() {
    rm -rf "${RUN:?}/media"
    mkdir -p "$RUN/media"
    cp -r "$MEDIA_STORE"/. "$RUN/media/"
    chmod -R u+w "$RUN/media"

    # The file:/// segment playlist can only be written once the serving path is
    # known. Same for the direct file: url in the manifest below.
    if [ -f "$RUN/media/deny/hls-file/index.m3u8.template" ]; then
        sed "s#@@MEDIA_DIR@@#$RUN/media#g" \
            "$RUN/media/deny/hls-file/index.m3u8.template" \
            >"$RUN/media/deny/hls-file/index.m3u8"
        rm -f "$RUN/media/deny/hls-file/index.m3u8.template"
    fi
}

# The five cases that are properties of the server, not of a file on disk.
write_manifest() {
    local base="https://$HOST:$HTTPS_PORT"
    local basehttp="http://$HOST:$HTTP_PORT"

    jq -n \
        --arg base "$base" \
        --arg basehttp "$basehttp" \
        --arg mediadir "$RUN/media" \
        --arg host "$HOST" \
        --argjson generated "$(date -u +%s)" \
        --slurpfile media "$RUN/media/cases.json" \
        '
    {
      schema: "uuav-media-test-manifest/1",
      generated_at: $generated,
      host: $host,
      base_url_https: $base,
      base_url_http: $basehttp,
      media_dir: $mediadir,
      gates: {
        protocol_whitelist_retail: "https,tls,tcp,crypto,data",
        protocol_whitelist_editor: "https,tls,tcp,crypto,data,file,http",
        format_whitelist: "mov,mp4,matroska,webm,hls,dash,mpegts,mp3,wav,ogg,flac,aac",
        codec_whitelist: "h264,hevc,vp9,av1,aac,mp3,opus,vorbis,flac,pcm_s16le,pcm_s16be,pcm_f32le",
        max_pixels: 33177600,
        rw_timeout_seconds: 15,
        decode_thread_count: 2,
        scheme_gate: "http,https (StringExtensions.HasAllowedMediaScheme, C# side)"
      },
      cases:
        ($media[0].cases | map(. + {
            url: ($base + "/media/" + .path),
            verify: (.verify // "media"),
            sim_decoder: (.sim_decoder // null),
            sim_decoder_hazard: (.sim_decoder_hazard // false)
          }))
        + [
          { id: "https-redirect-chain", path: null, url: ($base + "/redirect/3"),
            container: "mov,mp4,m4a,3gp,3g2,mj2", video: "h264", audio: "aac",
            expected: "PLAYS", expected_editor: "PLAYS", gate: null,
            verify: "media", sim_decoder: null,
            note: "three 302 hops to h264-aac.mp4" },
          { id: "deny-stall-timeout", path: null, url: ($base + "/stall/video.mp4"),
            container: null, video: null, audio: null,
            expected: "REFUSED", expected_editor: "REFUSED", gate: "rw_timeout",
            verify: "stall", sim_decoder: null,
            note: "connection accepted, TLS completed, no response body ever sent; must fail in about 15s, not hang" },
          { id: "deny-http-plaintext", path: null, url: ($basehttp + "/media/h264-aac.mp4"),
            container: "mov,mp4,m4a,3gp,3g2,mj2", video: "h264", audio: "aac",
            expected: "REFUSED", expected_editor: "PLAYS", gate: "protocol_whitelist",
            verify: "media", sim_decoder: null,
            note: "the http/https split between retail and Editor builds; record both" },
          { id: "deny-file-url", path: null, url: ("file://" + $mediadir + "/h264-aac.mp4"),
            container: "mov,mp4,m4a,3gp,3g2,mj2", video: "h264", audio: "aac",
            expected: "REFUSED", expected_editor: "REFUSED", gate: "scheme_gate",
            verify: "none", sim_decoder: null,
            note: "refused on the C# side by HasAllowedMediaScheme before FFmpeg sees it, in every build" },
          { id: "deny-rtsp-url", path: null, url: ("rtsp://" + $host + ":8554/uuav-test"),
            container: null, video: null, audio: null,
            expected: "REFUSED", expected_editor: "REFUSED", gate: "scheme_gate",
            verify: "none", sim_decoder: null,
            note: "refused by HasAllowedMediaScheme; no rtsp server is started, and none is needed" }
        ]
    }' >"$RUN/manifest.json"
}

write_nginx_conf() {
    mkdir -p "$RUN/tmp"
    cat >"$RUN/locations.conf" <<EOF
    add_header Access-Control-Allow-Origin  "*" always;
    add_header Access-Control-Allow-Headers "*" always;
    add_header Access-Control-Expose-Headers "*" always;

    location = /healthz      { default_type text/plain; return 200 "ok\n"; }
    location = /manifest.json { default_type application/json; alias $RUN/manifest.json; }
    location /media/         { alias $RUN/media/; autoindex on; }
    location = /             { return 302 /media/; }

    # deliberate redirect chain, three hops onto a real asset
    location = /redirect/3   { return 302 /redirect/2; }
    location = /redirect/2   { return 302 /redirect/1; }
    location = /redirect/1   { return 302 /media/h264-aac.mp4; }

    # deliberate stall: upstream accepts and never answers, nginx waits far
    # longer than the client's 15s rw_timeout so the client is what gives up
    location /stall          {
        proxy_pass http://127.0.0.1:$STALL_PORT;
        proxy_connect_timeout 5s;
        proxy_read_timeout 300s;
        proxy_send_timeout 300s;
    }
EOF

    cat >"$RUN/nginx.conf" <<EOF
worker_processes 1;
daemon off;
pid $RUN/nginx.pid;
error_log $RUN/nginx-error.log warn;
events { worker_connections 128; }
http {
    include $NGINX_PKG/conf/mime.types;
    types {
        application/dash+xml  mpd;
        video/iso.segment     m4s;
        audio/aac             aac;
        audio/flac            flac;
        application/octet-stream key;
    }
    default_type application/octet-stream;
    access_log $RUN/nginx-access.log;
    client_body_temp_path $RUN/tmp/body;
    proxy_temp_path       $RUN/tmp/proxy;
    fastcgi_temp_path     $RUN/tmp/fastcgi;
    uwsgi_temp_path       $RUN/tmp/uwsgi;
    scgi_temp_path        $RUN/tmp/scgi;
    sendfile on;
    server_tokens off;

    server {
        listen $BIND:$HTTPS_PORT ssl;
        server_name _;
        ssl_certificate     $PKI/server.pem;
        ssl_certificate_key $PKI/server-key.pem;
        ssl_protocols TLSv1.2 TLSv1.3;
        include $RUN/locations.conf;
    }

    server {
        listen $BIND:$HTTP_PORT;
        server_name _;
        include $RUN/locations.conf;
    }
}
EOF
}

PIDS=()
cleanup() {
    local pid
    for pid in "${PIDS[@]:-}"; do
        [ -n "$pid" ] || continue
        kill "$pid" 2>/dev/null || true
    done
    wait 2>/dev/null || true
}
trap cleanup EXIT INT TERM

start_servers() {
    python3 "$LIBEXEC/stall-server.py" "$STALL_PORT" >"$RUN/stall.log" 2>&1 &
    PIDS+=("$!")

    "$NGINX_PKG/bin/nginx" -p "$RUN" -c "$RUN/nginx.conf" -e "$RUN/nginx-error.log" &
    PIDS+=("$!")

    local attempt
    for attempt in $(seq 1 50); do
        : "$attempt"
        if curl -fsS --cacert "$PKI/ca.pem" --resolve "$HOST:$HTTPS_PORT:127.0.0.1" \
            "https://$HOST:$HTTPS_PORT/healthz" >/dev/null 2>&1; then
            return 0
        fi
        sleep 0.2
    done
    tail -n 20 "$RUN/nginx-error.log" >&2 || true
    die "servers did not come up on $HTTPS_PORT/$HTTP_PORT (see $RUN/nginx-error.log)"
}

stage_scene() {
    mkdir -p "$SCENE"
    cp -r "$SCENE_STORE"/. "$SCENE/"
    chmod -R u+w "$SCENE"

    # The scene is a template: it learns its case list and its media host from
    # the manifest, so nothing about ports or addresses is committed.
    jq -r '
      "// Generated by nix/uuav-test/runner.sh - do not edit.\n" +
      "export type Expectation = \"PLAYS\" | \"REFUSED\" | \"BUILD_DEPENDENT\"\n" +
      "export type Case = { id: string; url: string; expected: Expectation; expectedEditor: Expectation; gate: string | null; container: string | null; video: string | null; audio: string | null }\n" +
      "export const BASE_URL = " + (.base_url_https | tostring | @json) + "\n" +
      "export const CASES: Case[] = " + (
        [ .cases[] | {
            id, url,
            expected: .expected,
            expectedEditor: .expected_editor,
            gate: .gate,
            container: .container,
            video: .video,
            audio: .audio
          } ] | tojson
      ) + "\n"
    ' "$RUN/manifest.json" >"$SCENE/src/cases.generated.ts"

    # allowedMediaHostnames must name the host the urls actually use, or the
    # client refuses the media before any of this matters (when the
    # enforce-media-hostname-allowlist feature flag is on).
    jq --arg host "$HOST" \
        '.allowedMediaHostnames = ([$host, "media.uuav.test", "localhost", "127.0.0.1"] | unique)' \
        "$SCENE/scene.json" >"$SCENE/scene.json.tmp"
    mv "$SCENE/scene.json.tmp" "$SCENE/scene.json"
}

resolve_sdk() {
    if [ -n "${DCL_ONE_SDK_BIN:-}" ]; then
        [ -x "$DCL_ONE_SDK_BIN" ] || die "DCL_ONE_SDK_BIN is not executable: $DCL_ONE_SDK_BIN"
        SDK_CMD=("$DCL_ONE_SDK_BIN")
        SDK_SOURCE="DCL_ONE_SDK_BIN"
        return 0
    fi
    if command -v dcl-one-sdk >/dev/null 2>&1; then
        SDK_CMD=("$(command -v dcl-one-sdk)")
        SDK_SOURCE="PATH"
        return 0
    fi
    if [ -n "${UUAV_TEST_SDK_FLAKE:-}" ] && command -v nix >/dev/null 2>&1; then
        SDK_CMD=(nix run "$UUAV_TEST_SDK_FLAKE#dcl-one-sdk" --)
        SDK_SOURCE="UUAV_TEST_SDK_FLAKE=$UUAV_TEST_SDK_FLAKE"
        return 0
    fi
    return 1
}

launch_scene() {
    if ! resolve_sdk; then
        cat >&2 <<EOF

uuav-test: dcl-one-sdk not found, so the scene was not launched. The servers
above are up and the manifest is at $RUN/manifest.json; point any client at it.

dcl-one-sdk is the npm-free Rust replacement for @dcl/sdk-commands. It is a
subtree of the private dcl-one monorepo (~/one/catalyrst/crates/dcl-one-sdk),
exported standalone by ~/one/scripts/export/export-dcl-one-sdk.sh. Give this
harness one of:

  DCL_ONE_SDK_BIN=/path/to/dcl-one-sdk        nix run .#uuav-test
  UUAV_TEST_SDK_FLAKE=/path/to/dcl-one-sdk    nix run .#uuav-test   # flake dir
  ... or put dcl-one-sdk on PATH.

EOF
        return 1
    fi

    log "dcl-one-sdk from $SDK_SOURCE"
    if [ ! -d "$SCENE/node_modules/@dcl/sdk" ]; then
        log "restoring the vendored node_modules into the staged scene"
        "${SDK_CMD[@]}" init --node-modules-only --dir "$SCENE"
    fi

    log "starting preview on port $SCENE_PORT (ctrl-c to stop everything)"
    "${SDK_CMD[@]}" start --dir "$SCENE" --port "$SCENE_PORT" --no-browser --no-asset-bundles
}

summary() {
    local plays refused build
    plays=$(jq '[.cases[] | select(.expected=="PLAYS")] | length' "$RUN/manifest.json")
    refused=$(jq '[.cases[] | select(.expected=="REFUSED")] | length' "$RUN/manifest.json")
    build=$(jq '[.cases[] | select(.expected=="BUILD_DEPENDENT")] | length' "$RUN/manifest.json")
    cat >&2 <<EOF

  media (https)  https://$HOST:$HTTPS_PORT/media/
  media (http)   http://$HOST:$HTTP_PORT/media/
  manifest       https://$HOST:$HTTPS_PORT/manifest.json  (file: $RUN/manifest.json)
  local CA       $PKI/ca.pem   ('nix run .#uuav-test -- --trust' for install steps)
  cases          $plays PLAYS, $refused REFUSED, $build build-dependent

EOF
}

# ----------------------------------------------------------------------- main
mkdir -p "$STATE" "$RUN"
resolve_host

if [ "$MODE" = trust ]; then
    bash "$LIBEXEC/pki.sh" "$PKI" "$EXTRA_SAN" >&2
    trust_help
    exit 0
fi

log "state $STATE"
log "host  $HOST ($HOST_SOURCE)"
if [ "$HOST" = "127.0.0.1" ] && [ "$HOST_SOURCE" = fallback ]; then
    cat >&2 <<EOF
       media.uuav.test does not resolve, so urls use the loopback address.
       That is fine on Linux (OpenSSL matches IP SANs) but macOS SecureTransport
       may reject an IP-literal host. Before running a real client, add:
           echo "127.0.0.1  media.uuav.test" | sudo tee -a /etc/hosts
EOF
fi

bash "$LIBEXEC/pki.sh" "$PKI" "$EXTRA_SAN" >&2
stage_media
write_manifest

if [ "$MODE" = print-manifest ]; then
    cat "$RUN/manifest.json"
    exit 0
fi

write_nginx_conf
start_servers
log "servers up"
summary

case "$MODE" in
    check)
        bash "$LIBEXEC/check.sh" "$RUN/manifest.json" "$PKI/ca.pem"
        exit $?
        ;;
    serve-only)
        log "serving only; ctrl-c to stop"
        wait
        ;;
    serve-and-play)
        stage_scene
        launch_scene || exit 1
        ;;
esac
