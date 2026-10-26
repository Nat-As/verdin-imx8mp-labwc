#!/bin/sh
# GPU Test launcher. --journal (used by the desktop entry, which has no
# terminal) sends the output to the journal: journalctl -t gpu-test
if [ "${1:-}" = "--journal" ]; then
    shift
    if command -v systemd-cat >/dev/null 2>&1; then
        exec systemd-cat -t gpu-test /usr/bin/dotnet /usr/lib/gpu-test/GpuTest.dll "$@"
    fi
fi
exec /usr/bin/dotnet /usr/lib/gpu-test/GpuTest.dll "$@"
