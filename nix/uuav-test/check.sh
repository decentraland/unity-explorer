#!/usr/bin/env bash
# Regression check over a running harness. Two independent questions per case:
#
#   SERVED   does the url return the container and codecs the manifest claims?
#            Answered by an ungated ffprobe (or, for the file:/// segment
#            playlist, by fetching the playlist and resolving its segments).
#            This is the half that can be verified anywhere, including on Linux
#            where the plugin itself cannot run.
#
#   GATE     would the sandbox's own option values let this through? Answered by
#            re-running ffprobe with the plugin's exact protocol/format/codec
#            whitelists, max_pixels and rw_timeout. This is a SIMULATION with the
#            system FFmpeg, not the plugin: it proves the gate values do what the
#            manifest says about real content. It cannot prove the plugin passes
#            them correctly, it cannot see the C# scheme gate, and it cannot see
#            a demuxer or decoder missing from the plugin's own FFmpeg build.
#
# Every REFUSED case has to pass SERVED first. A refusal only means something if
# the content behind it is valid: otherwise a typo in a filename would read as a
# working gate.
set -uo pipefail

MANIFEST="${1:?usage: check.sh MANIFEST_JSON CA_PEM}"
CA="${2:?usage: check.sh MANIFEST_JSON CA_PEM}"

RETAIL_PROTOCOLS=$(jq -r '.gates.protocol_whitelist_retail' "$MANIFEST")
EDITOR_PROTOCOLS=$(jq -r '.gates.protocol_whitelist_editor' "$MANIFEST")
FORMATS=$(jq -r '.gates.format_whitelist' "$MANIFEST")
CODECS=$(jq -r '.gates.codec_whitelist' "$MANIFEST")
MAX_PIXELS=$(jq -r '.gates.max_pixels' "$MANIFEST")

pass=0
fail=0
skip=0
info=0
warn=0

ERRFILE=$(mktemp)
trap 'rm -f "$ERRFILE"' EXIT

first_error() { head -n1 "$ERRFILE" | cut -c1-140; }

probe_raw() { # probe_raw URL [extra ffprobe args...]
    local url="$1"
    shift
    timeout 60 ffprobe -v error -hide_banner \
        -tls_verify 1 -ca_file "$CA" \
        -show_entries format=format_name:stream=codec_type,codec_name,width,height \
        -of json "$@" "$url" 2>"$ERRFILE"
}

probe_gated() { # probe_gated URL PROTOCOLS [extra ffprobe args...]
    local url="$1" protocols="$2"
    shift 2
    probe_raw "$url" \
        -protocol_whitelist "$protocols" \
        -format_whitelist "$FORMATS" \
        -codec_whitelist "$CODECS" \
        -max_pixels "$MAX_PIXELS" \
        -rw_timeout 15000000 \
        "$@"
}

report() { # report ID VERDICT DETAIL
    local colour=""
    case "$2" in
        PASS)
            colour=$'\033[32m'
            pass=$((pass + 1))
            ;;
        FAIL)
            colour=$'\033[31m'
            fail=$((fail + 1))
            ;;
        WARN)
            colour=$'\033[33m'
            warn=$((warn + 1))
            ;;
        SKIP)
            colour=$'\033[90m'
            skip=$((skip + 1))
            ;;
        INFO)
            colour=$'\033[36m'
            info=$((info + 1))
            ;;
    esac
    printf '  %s%-6s\033[0m %-24s %s\n' "$colour" "$2" "$1" "$3"
}

# ffprobe's -max_pixels is an AVCodecContext option. Refuse to report a green
# max_pixels case if this build does not accept it.
if ffprobe -v error -max_pixels 1000 -f lavfi -i "color=c=black:s=16x16:d=0.1" \
    -show_entries format=format_name -of csv=p=0 >/dev/null 2>&1; then
    HAVE_MAX_PIXELS=1
else
    HAVE_MAX_PIXELS=0
fi

# The playlist whose segments are file:/// urls: no probe can read it without
# already granting the thing under test, so verify it structurally instead.
verify_playlist() { # verify_playlist URL -> prints a detail line, returns 0/1
    local url="$1" body segment target count=0
    body=$(curl -fsS --max-time 20 --cacert "$CA" "$url" 2>"$ERRFILE") || {
        echo "playlist is not served: $(first_error)"
        return 1
    }
    while read -r segment; do
        target="${segment#file://}"
        [ -f "$target" ] || {
            echo "playlist references a missing segment: $target"
            return 1
        }
        count=$((count + 1))
    done < <(grep '^file://' <<<"$body")
    if [ "$count" -eq 0 ]; then
        echo "playlist carries no file:// segments, so it tests nothing"
        return 1
    fi
    echo "$count file:// segments, all present on disk"
    return 0
}

echo
echo "uuav-test check: $(jq -r '.base_url_https' "$MANIFEST")"
echo

while read -r case_json; do
    id=$(jq -r '.id' <<<"$case_json")
    url=$(jq -r '.url' <<<"$case_json")
    expected=$(jq -r '.expected' <<<"$case_json")
    expected_editor=$(jq -r '.expected_editor' <<<"$case_json")
    gate=$(jq -r '.gate // "none"' <<<"$case_json")
    want_container=$(jq -r '.container // ""' <<<"$case_json")
    want_video=$(jq -r '.video // ""' <<<"$case_json")
    want_audio=$(jq -r '.audio // ""' <<<"$case_json")
    verify=$(jq -r '.verify' <<<"$case_json")
    sim_decoder=$(jq -r '.sim_decoder // ""' <<<"$case_json")
    sim_decoder_hazard=$(jq -r '.sim_decoder_hazard // false' <<<"$case_json")

    # A full FFmpeg can hold several decoders for one codec id and prefers a
    # library one (libdav1d for av1, mp3float for mp3). codec_whitelist matches
    # DECODER names, so the simulation has to pin the decoder the plugin's own
    # restricted build would use, or it measures this machine's FFmpeg instead.
    decoder_args=()
    if [ -n "$sim_decoder" ]; then
        if [ -n "$want_video" ]; then
            decoder_args=(-c:v "$sim_decoder")
        else
            decoder_args=(-c:a "$sim_decoder")
        fi
    fi

    case "$verify" in
        none)
            report "$id" SKIP "client-side $gate, nothing server-side to verify"
            continue
            ;;
        stall)
            start=$SECONDS
            probe_gated "$url" "$RETAIL_PROTOCOLS" >/dev/null
            rc=$?
            elapsed=$((SECONDS - start))
            if [ "$rc" -eq 0 ]; then
                report "$id" FAIL "the stall endpoint answered; it must never send a body"
            elif [ "$elapsed" -ge 10 ] && [ "$elapsed" -le 40 ]; then
                report "$id" PASS "rw_timeout aborted the read after ${elapsed}s (15s configured)"
            else
                report "$id" FAIL "aborted after ${elapsed}s, expected about 15s"
            fi
            continue
            ;;
        playlist)
            detail=$(verify_playlist "$url") || {
                report "$id" FAIL "$detail"
                continue
            }
            probe_gated "$url" "$RETAIL_PROTOCOLS" >/dev/null
            if [ $? -eq 0 ]; then
                report "$id" FAIL "the retail protocol list followed the playlist into file://"
                continue
            fi
            if probe_gated "$url" "$EDITOR_PROTOCOLS" >/dev/null; then
                report "$id" PASS "$detail; retail refuses the pivot, the Editor list follows it"
            else
                report "$id" WARN "$detail; retail refuses the pivot, but the Editor list does not follow it either: $(first_error)"
            fi
            continue
            ;;
    esac

    # ---- SERVED
    raw=$(probe_raw "$url")
    if [ -z "$raw" ]; then
        report "$id" FAIL "url is not served: $(first_error)"
        continue
    fi

    got_container=$(jq -r '.format.format_name // ""' <<<"$raw")
    got_video=$(jq -r '[.streams[]? | select(.codec_type=="video") | .codec_name] | first // ""' <<<"$raw")
    got_audio=$(jq -r '[.streams[]? | select(.codec_type=="audio") | .codec_name] | first // ""' <<<"$raw")

    mismatch=""
    [ -z "$want_container" ] || [ "$got_container" = "$want_container" ] ||
        mismatch="container $got_container != $want_container;"
    [ -z "$want_video" ] || [ "$got_video" = "$want_video" ] ||
        mismatch="$mismatch video $got_video != $want_video;"
    [ -z "$want_audio" ] || [ "$got_audio" = "$want_audio" ] ||
        mismatch="$mismatch audio $got_audio != $want_audio;"
    if [ -n "$mismatch" ]; then
        report "$id" FAIL "served content disagrees with the manifest: $mismatch"
        continue
    fi

    # ---- GATE
    if [ "$gate" = "max_pixels" ] && [ "$HAVE_MAX_PIXELS" -eq 0 ]; then
        report "$id" SKIP "this ffprobe does not accept -max_pixels"
        continue
    fi

    probe_gated "$url" "$RETAIL_PROTOCOLS" "${decoder_args[@]}" >/dev/null
    gated_rc=$?
    gated_err=$(first_error)

    case "$expected" in
        PLAYS)
            if [ "$gated_rc" -ne 0 ]; then
                report "$id" FAIL "the retail gates refuse it: $gated_err"
            elif [ "$sim_decoder_hazard" = "true" ]; then
                # The pinned decoder passed - but this case is flagged because
                # the plugin's build does NOT force that decoder. Re-probe the
                # way the plugin actually does (avformat auto-selects the probe
                # decoder) and, if the whitelist then rejects the auto-selected
                # NAME, surface it every run instead of hiding it behind the pin.
                probe_gated "$url" "$RETAIL_PROTOCOLS" >/dev/null
                auto_name=$(grep -oE 'Codec \([a-z0-9_]+\) not on whitelist' "$ERRFILE" | head -n1 | sed -E 's/Codec \(([a-z0-9_]+)\).*/\1/')
                if [ -n "$auto_name" ]; then
                    report "$id" WARN "content is valid and the pinned decoder passes, but avformat auto-selects '$auto_name', which codec_whitelist REJECTS: legitimate ${want_audio:-$want_video} would be refused by the shipped gate"
                else
                    report "$id" PASS "$got_container ${got_video:+$got_video/}${got_audio:-} (auto-selected decoder also accepted)"
                fi
            else
                report "$id" PASS "$got_container ${got_video:+$got_video/}${got_audio:-}"
            fi
            ;;
        REFUSED)
            if [ "$gated_rc" -eq 0 ]; then
                report "$id" FAIL "the retail gates let it through, but $gate should refuse it"
            else
                detail="$gate: $gated_err"
                if [ "$expected_editor" = "PLAYS" ]; then
                    if probe_gated "$url" "$EDITOR_PROTOCOLS" "${decoder_args[@]}" >/dev/null; then
                        detail="$detail; the Editor list plays it, as claimed"
                    else
                        report "$id" FAIL "refused by the retail gates, but the Editor list does not play it either: $(first_error)"
                        continue
                    fi
                fi
                report "$id" PASS "$detail"
            fi
            ;;
        BUILD_DEPENDENT)
            if [ "$gated_rc" -eq 0 ]; then
                report "$id" INFO "served correctly and the runtime whitelists allow it; whether it plays depends on the plugin's FFmpeg build"
            else
                report "$id" INFO "the runtime whitelists refuse it here: $gated_err"
            fi
            ;;
    esac
done < <(jq -c '.cases[]' "$MANIFEST")

echo
printf '  %d pass, %d fail, %d warn, %d skip, %d info\n\n' "$pass" "$fail" "$warn" "$skip" "$info"
[ "$fail" -eq 0 ]
