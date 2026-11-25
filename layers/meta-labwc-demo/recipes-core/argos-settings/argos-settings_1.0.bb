SUMMARY = "System settings helper of the Avalonia application"
DESCRIPTION = "The privileged part of the Settings page of the Avalonia \
application (gpu-test): a helper that sets the time, time zone, NTP, \
hardware clock, host name and display session (kiosk or desktop) and \
reboots or powers off, run with sudo without password by the desktop and \
admin users (arguments are validated). A udev rule lets the video group set \
the display backlight brightness."
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302"

SRC_URI = " \
    file://argos-settings-helper \
    file://argos-settings.sudoers \
    file://90-argos-backlight.rules \
"

S = "${WORKDIR}"

inherit allarch

DESKTOP_USER = "${LABWC_DEMO_DESKTOP_USER}"
ADMIN_USER = "${LABWC_DEMO_ADMIN_USER}"

do_install() {
    install -Dm0755 ${WORKDIR}/argos-settings-helper ${D}${libexecdir}/argos/argos-settings-helper
    install -Dm0440 ${WORKDIR}/argos-settings.sudoers ${D}${sysconfdir}/sudoers.d/argos-settings
    sed -i -e 's/@DESKTOP_USER@/${DESKTOP_USER}/g' -e 's/@ADMIN_USER@/${ADMIN_USER}/g' \
        ${D}${sysconfdir}/sudoers.d/argos-settings
    install -Dm0644 ${WORKDIR}/90-argos-backlight.rules ${D}${sysconfdir}/udev/rules.d/90-argos-backlight.rules
}

FILES:${PN} = " \
    ${libexecdir}/argos \
    ${sysconfdir}/sudoers.d \
    ${sysconfdir}/udev/rules.d \
"

# timedatectl and hostnamectl come with systemd; hwclock from util-linux;
# the time zones from tzdata
RDEPENDS:${PN} = "bash sudo systemd util-linux-hwclock tzdata"
