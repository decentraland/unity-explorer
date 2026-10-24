#!/bin/sh
# Starts a virtual display for the player, then hands over to it. The player redirects its own stdout
# into its log once it starts, so the result lines go out on file descriptor 3, which is pointed at the
# container's stdout here. xvfb-run is not used because it merges stderr into stdout.
set -eu

SIZE="${RENDER_SERVER_SIZE:-1024}"

Xvfb :99 -screen 0 "$((SIZE + 64))x$((SIZE + 64))x24" -nolisten tcp >/dev/null 2>&1 &
export DISPLAY=:99

i=0
while [ ! -e /tmp/.X11-unix/X99 ]; do
    i=$((i + 1))
    if [ "$i" -gt 100 ]; then
        echo "Xvfb did not start" >&2
        exit 3
    fi
    sleep 0.1
done

exec 3>&1
exec /opt/renderer/renderer.x86_64 \
    -logFile /dev/stderr \
    -screen-fullscreen 0 -screen-width "$SIZE" -screen-height "$SIZE" \
    --size "$SIZE" --results /dev/fd/3 "$@"
