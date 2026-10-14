# meta-toradex-demos installs /etc/profile.d/x11-display-var.sh, which sets
# DISPLAY=:0.0 in every login shell. With DISPLAY set, wlroots tries its X11
# backend before DRM ("cannot create X11 backend"), so labwc cannot be started
# from a console login. This image is pure Wayland: drop it.
do_install:append() {
    rm -f ${D}${sysconfdir}/profile.d/x11-display-var.sh
}
