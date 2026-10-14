# Point shells of the desktop user (e.g. over SSH) at the running labwc
# session so GUI programs can be started remotely, e.g. "foot &".
if [ -z "$WAYLAND_DISPLAY" ] && [ -n "$XDG_RUNTIME_DIR" ] && \
   [ -S "$XDG_RUNTIME_DIR/wayland-0" ]; then
	export WAYLAND_DISPLAY=wayland-0
fi
