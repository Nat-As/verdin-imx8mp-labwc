FILESEXTRAPATHS:prepend := "${THISDIR}/files:"

# Use fuzzel's built-in nanosvg instead of librsvg (avoids a cross Rust
# toolchain just for launcher icons).
PACKAGECONFIG:remove = "librsvg"

# Replace fuzzel's sample /etc/xdg/fuzzel/fuzzel.ini with the demo defaults.
SRC_URI += "file://fuzzel.ini"

do_install:append() {
    install -Dm0644 ${WORKDIR}/fuzzel.ini ${D}${sysconfdir}/xdg/fuzzel/fuzzel.ini
}
