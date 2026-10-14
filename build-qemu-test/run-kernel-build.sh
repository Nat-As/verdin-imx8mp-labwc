#!/bin/bash
cd "$(dirname "$0")/.." || exit 1
. layers/openembedded-core/oe-init-build-env build-qemu-test > /dev/null
bitbake virtual/kernel > ../build-logs/qemu-kernel.log 2>&1
echo "=== kernel build end rc=$?" >> ../build-logs/qemu-kernel.log
