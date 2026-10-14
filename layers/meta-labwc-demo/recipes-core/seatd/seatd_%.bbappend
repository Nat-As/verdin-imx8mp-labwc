# Run seatd as the seat manager for the labwc session.
#
# Why not logind: the Toradex distro builds systemd with polkit support, but
# the image has no polkit daemon. libseat's logind backend calls
# org.freedesktop.login1.Session.Activate, which logind authorises through
# polkit (org.freedesktop.login1.chvt) for non-root callers; without polkitd
# that fails and libseat aborts, so an unprivileged compositor cannot start.
# seatd runs as root and hands DRM/input devices to members of the "video"
# group, so labwc can run as a normal user without polkit.
inherit systemd

SYSTEMD_SERVICE:${PN} = "seatd.service"
SYSTEMD_AUTO_ENABLE:${PN} = "enable"

do_install:append() {
    if ${@bb.utils.contains('DISTRO_FEATURES', 'systemd', 'true', 'false', d)}; then
        install -Dm0644 ${S}/contrib/systemd/seatd.service ${D}${systemd_system_unitdir}/seatd.service
        sed -i -e 's#^ExecStart=seatd -g seat#ExecStart=${bindir}/seatd -g video#' \
            ${D}${systemd_system_unitdir}/seatd.service
        grep -q '^ExecStart=${bindir}/seatd -g video$' ${D}${systemd_system_unitdir}/seatd.service
    fi
}
