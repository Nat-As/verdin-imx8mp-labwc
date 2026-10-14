# No Xwayland in this demo; keep the session pure Wayland.
PACKAGECONFIG:remove = "xwayland"

# Only pull in what our session uses (the recipe recommends grim, slurp,
# labwc-tweaks, etc.).
RRECOMMENDS:${PN} = "foot swaybg"
