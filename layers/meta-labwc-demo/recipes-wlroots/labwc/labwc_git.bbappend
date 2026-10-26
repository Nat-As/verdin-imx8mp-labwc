# No Xwayland in this demo; keep the session pure Wayland.
PACKAGECONFIG:remove = "xwayland"

# Only pull in what our session uses (the recipe recommends grim, slurp,
# labwc-tweaks, etc.).
RRECOMMENDS:${PN} = "foot swaybg"

# Backport (labwc a98f2635, in 0.7.3): xdg-shell v3 with popup repositioning.
# labwc 0.7.1 offers xdg-shell v2, and Avalonia's Wayland backend (GPU Test)
# requires v3.
FILESEXTRAPATHS:prepend := "${THISDIR}/files:"
SRC_URI += "file://0001-xdg-support-xdg-shell-v3-with-popup-repositioning.patch"
