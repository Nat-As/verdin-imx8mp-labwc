FILESEXTRAPATHS:prepend := "${THISDIR}/files:"

# Replace foot's sample /etc/xdg/foot/foot.ini with the demo defaults
# (DejaVu Sans Mono, TERM=xterm-256color).
SRC_URI += "file://foot.ini"

do_install:append() {
    install -Dm0644 ${WORKDIR}/foot.ini ${D}${sysconfdir}/xdg/foot/foot.ini
}
