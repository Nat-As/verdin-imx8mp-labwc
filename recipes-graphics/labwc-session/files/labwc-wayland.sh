# The image only ships the en_US locale; default to it so programs like foot
# (which require a UTF-8 locale) work in SSH and console logins.
if [ -z "$LANG" ] && [ -z "$LC_ALL" ]; then
	export LANG=en_US.UTF-8
fi

# Point shells of the desktop user (e.g. over SSH) at the running labwc
# session so GUI programs can be started remotely, e.g. "foot &".
if [ -z "$WAYLAND_DISPLAY" ] && [ -n "$XDG_RUNTIME_DIR" ] && \
   [ -S "$XDG_RUNTIME_DIR/wayland-0" ]; then
	export WAYLAND_DISPLAY=wayland-0
fi
