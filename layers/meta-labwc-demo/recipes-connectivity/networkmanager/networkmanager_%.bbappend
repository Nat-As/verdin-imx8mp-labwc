FILESEXTRAPATHS:prepend := "${THISDIR}/files:"

# nmtui (text UI, opened from the waybar network module) and Wi-Fi support
PACKAGECONFIG:append = " nmtui wifi"

SRC_URI += "file://90-labwc-demo.conf"

do_install:append() {
    install -Dm0644 ${WORKDIR}/90-labwc-demo.conf ${D}${sysconfdir}/NetworkManager/conf.d/90-labwc-demo.conf
}
