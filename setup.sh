#!/bin/bash
# Fetch the Toradex BSP (tag 7.7.0, tdxref manifest) plus the pinned
# meta-wayland layer into this directory, next to our own layer in
# layers/meta-labwc-demo. Run once after cloning, then ./build.sh.
set -euo pipefail
cd "$(dirname "$0")"

TDX_MANIFEST_URL=${TDX_MANIFEST_URL:-https://git.toradex.com/toradex-manifest.git}
TDX_TAG=${TDX_TAG:-7.7.0}
JOBS=${JOBS:-8}

if ! command -v repo >/dev/null 2>&1; then
    echo "error: 'repo' not found, see https://gerrit.googlesource.com/git-repo" >&2
    exit 1
fi

repo init -u "$TDX_MANIFEST_URL" -b "refs/tags/$TDX_TAG" -m tdxref/default.xml \
     --no-clone-bundle < /dev/null
mkdir -p .repo/local_manifests
cp manifests/labwc-demo.xml .repo/local_manifests/labwc-demo.xml
repo sync -j"$JOBS" -c --no-clone-bundle

echo
echo "Workspace ready. Build with: ./build.sh   (log: build-logs/latest.log)"
