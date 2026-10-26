# meta-toradex-demos installs /etc/profile.d/x11-display-var.sh, which sets
# DISPLAY=:0.0 in every login shell. With DISPLAY set, wlroots tries its X11
# backend before DRM ("cannot create X11 backend"), so labwc cannot be started
# from a console login. This image is pure Wayland: drop it.
do_install:append() {
    rm -f ${D}${sysconfdir}/profile.d/x11-display-var.sh
}

# Release version of the image (config/release.conf), for scripts and support
LABWC_DEMO_VERSION ??= "0.0.0"
do_install:append() {
    printf 'NAME="Argos labwc demo image"\nVERSION=%s\n' "${LABWC_DEMO_VERSION}" > ${D}${sysconfdir}/argos-release
}
