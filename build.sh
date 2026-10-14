#!/bin/bash
# Build the labwc demo image. Usage: ./build.sh [extra bitbake args]
# Log: build-logs/build-<timestamp>.log
set -o pipefail
cd "$(dirname "$0")" || exit 1
TS=$(date +%Y%m%d-%H%M%S)
LOG="$PWD/build-logs/build-$TS.log"
mkdir -p build-logs
ln -sf "build-$TS.log" build-logs/latest.log
. ./export > /dev/null
START=$(date +%s)
echo "=== build start $(date -Is)" | tee -a "$LOG"
bitbake -k labwc-demo-image "$@" 2>&1 | tee -a "$LOG"
RC=$?
END=$(date +%s)
echo "=== build end $(date -Is) rc=$RC elapsed=$((END-START))s" | tee -a "$LOG"
exit $RC
