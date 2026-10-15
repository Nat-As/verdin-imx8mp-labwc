# fish equivalent of /etc/profile.d/labwc-wayland.sh (fish does not read
# /etc/profile). Installed in /etc/fish/conf.d/.

# The image only ships the en_US locale; foot and others need a UTF-8 locale.
if not set -q LANG; and not set -q LC_ALL
    set -gx LANG en_US.UTF-8
end

# Shells of the desktop user (e.g. over SSH) can start GUI programs in the
# running labwc session.
if not set -q WAYLAND_DISPLAY; and set -q XDG_RUNTIME_DIR; and test -S "$XDG_RUNTIME_DIR/wayland-0"
    set -gx WAYLAND_DISPLAY wayland-0
end
