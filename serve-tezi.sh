#!/bin/bash
# Serve a Tezi image to Toradex Easy Installer over the network (e.g. the USB
# NCM/RNDIS link of the module in recovery mode: module 192.168.11.1, this PC
# 192.168.11.2), instead of copying it to a USB stick.
#
#   ./serve-tezi.sh [vX.Y.Z | build | <Tezi .tar>]
#
#   vX.Y.Z  release/vX.Y.Z/Verdin-iMX8MP_labwc-Demo-Image-Tezi_vX.Y.Z.tar
#   build   the current build (build/deploy/images/verdin-imx8mp/...-Tezi.tar)
#   (none)  the newest release in release/
#
# The image is extracted to release/serve/ and served over HTTP with an
# image_list.json feed. The feed is announced with Zeroconf (_tezi._tcp, needs
# avahi-publish), so Tezi lists the image by itself; otherwise add the feed in
# Tezi (Feeds) as http://<this PC>:<port>/image_list.json. Ctrl+C stops.
#
# Environment: TEZI_HOST_IP (default: this PC's address on 192.168.11.0/24),
# TEZI_PORT (default 8080).
set -euo pipefail
cd "$(dirname "$0")"

src=${1:-}
if [ -z "$src" ]; then
    src=$(find release -maxdepth 1 -type d -name 'v*' -printf '%f\n' 2>/dev/null | sort -V | tail -n1)
    [ -n "$src" ] || { echo "no release in release/; build one or pass a .tar" >&2; exit 1; }
fi
case $src in
build)
    tar=build/deploy/images/verdin-imx8mp/Verdin-iMX8MP_labwc-Demo-Image-Tezi.tar
    name=labwc-demo-build ;;
v[0-9]*)
    tar=release/$src/Verdin-iMX8MP_labwc-Demo-Image-Tezi_$src.tar
    name=labwc-demo-$src ;;
*)
    tar=$src
    name=$(basename "$src" .tar | tr -c 'A-Za-z0-9._\n-' '-') ;;
esac
[ -f "$tar" ] || { echo "not found: $tar" >&2; exit 1; }

ip=${TEZI_HOST_IP:-$(ip -4 -o addr show | awk '$4 ~ /^192\.168\.11\./ { sub(/\/.*/, "", $4); print $4; exit }')}
[ -n "$ip" ] || { echo "no address on 192.168.11.0/24: is the module in recovery mode (Tezi) and connected over USB? Or set TEZI_HOST_IP" >&2; exit 1; }
port=${TEZI_PORT:-8080}

# Extract (once per tarball content) into a directory without '+' in its name
root=release/serve
stamp=$root/$name.sha256
sum=$(sha256sum "$tar" | cut -d' ' -f1)
if [ ! -f "$root/$name/image.json" ] || [ "$(cat "$stamp" 2>/dev/null)" != "$sum" ]; then
    echo "Extracting $tar"
    rm -rf "${root:?}/$name.tmp"
    mkdir -p "$root/$name.tmp"
    tar -xf "$tar" -C "$root/$name.tmp" --strip-components=1
    rm -rf "${root:?}/$name"
    mv "$root/$name.tmp" "$root/$name"
    echo "$sum" > "$stamp"
fi
printf '{\n  "config_format": 1,\n  "images": [\n    "%s/image.json"\n  ]\n}\n' "$name" > "$root/image_list.json"

desc=$(python3 -c 'import json, sys; j = json.load(open(sys.argv[1])); print(j.get("name", "") + " " + j.get("version", ""))' "$root/$name/image.json")
url="http://$ip:$port/image_list.json"
echo "Serving $desc"
echo "  feed: $url"

pids=()
cleanup() { kill "${pids[@]}" 2>/dev/null || true; }
trap cleanup EXIT INT TERM

if command -v avahi-publish >/dev/null 2>&1; then
    avahi-publish -s "labwc demo image ($name)" _tezi._tcp "$port" \
        "name=labwc demo image ($name)" "path=/image_list.json" "enabled=1" "https=0" &
    pids+=($!)
    echo "  announced with Zeroconf (_tezi._tcp): Tezi lists the image by itself"
else
    echo "  avahi-publish not found: add the feed in Tezi (Feeds) by hand"
fi
echo "Ctrl+C stops. HTTP requests from Tezi:"
python3 -m http.server "$port" --bind "$ip" --directory "$root" &
pids+=($!)
wait "${pids[@]}"
