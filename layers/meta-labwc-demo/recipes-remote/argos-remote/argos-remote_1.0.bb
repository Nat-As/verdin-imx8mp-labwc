SUMMARY = "Remote desktop (RustDesk) for the labwc demo image"
DESCRIPTION = "Runs a RustDesk server in each labwc session: the touch screen \
session of the desktop user (mirrored) and the unattended headless desktop of \
the admin user. Screen capture and input go through xdg-desktop-portal \
(ScreenCast and RemoteDesktop of xdg-desktop-portal-wlr) and PipeWire. \
argos-remote shows the IDs, ports and passwords and changes passwords."
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302"

SRC_URI = " \
    file://argos-remote \
    file://argos-remote-server \
    file://argos-remote-session \
    file://argos-remote.service \
    file://argos-remote.default \
    file://argos-remote.tmpfiles \
    file://90-argos-video-only.conf \
    file://portal-test \
"

S = "${WORKDIR}"

inherit features_check allarch
REQUIRED_DISTRO_FEATURES = "systemd wayland"

do_install() {
    install -Dm0755 ${WORKDIR}/argos-remote ${D}${bindir}/argos-remote
    sed -i -e 's/@DESKTOP_USER@/${LABWC_DEMO_DESKTOP_USER}/g' \
        -e 's/@ADMIN_USER@/${LABWC_DEMO_ADMIN_USER}/g' ${D}${bindir}/argos-remote
    install -Dm0755 ${WORKDIR}/argos-remote-server ${D}${libexecdir}/argos-remote/argos-remote-server
    install -Dm0755 ${WORKDIR}/argos-remote-session ${D}${bindir}/argos-remote-session
    install -Dm0755 ${WORKDIR}/portal-test ${D}${libexecdir}/argos-remote/portal-test
    install -Dm0644 ${WORKDIR}/argos-remote.service ${D}${systemd_user_unitdir}/argos-remote.service
    install -Dm0644 ${WORKDIR}/argos-remote.default ${D}${sysconfdir}/default/argos-remote
    install -Dm0644 ${WORKDIR}/argos-remote.tmpfiles ${D}${nonarch_libdir}/tmpfiles.d/argos-remote.conf
    sed -i -e 's/@DESKTOP_USER@/${LABWC_DEMO_DESKTOP_USER}/g' \
        -e 's/@ADMIN_USER@/${LABWC_DEMO_ADMIN_USER}/g' ${D}${nonarch_libdir}/tmpfiles.d/argos-remote.conf
    install -Dm0644 ${WORKDIR}/90-argos-video-only.conf \
        ${D}${datadir}/wireplumber/wireplumber.conf.d/90-argos-video-only.conf
}

FILES:${PN} += " \
    ${systemd_user_unitdir} \
    ${nonarch_libdir}/tmpfiles.d \
    ${datadir}/wireplumber \
"
CONFFILES:${PN} = "${sysconfdir}/default/argos-remote"

RDEPENDS:${PN} = " \
    rustdesk \
    xdg-desktop-portal \
    xdg-desktop-portal-wlr \
    pipewire \
    wireplumber \
    gstreamer1.0-pipewire \
    bash \
    gawk \
    coreutils \
    iproute2 \
    python3-core \
    python3-json \
    python3-netclient \
    python3-pygobject \
    gstreamer1.0-plugins-good-png \
    util-linux-runuser \
"
