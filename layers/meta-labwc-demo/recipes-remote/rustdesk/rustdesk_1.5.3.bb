SUMMARY = "RustDesk remote desktop"
DESCRIPTION = "RustDesk ${PV} from its aarch64 binary release (.deb, Rust core with a \
Flutter UI). The image runs it without its root service, as one server per \
desktop (argos-remote): on Wayland it then captures the screen and injects \
input through the xdg-desktop-portal RemoteDesktop interface."
HOMEPAGE = "https://rustdesk.com"
# RustDesk is AGPL-3.0; the bundled Flutter engine is BSD-3-Clause
LICENSE = "AGPL-3.0-only & BSD-3-Clause"
LIC_FILES_CHKSUM = " \
    file://${COMMON_LICENSE_DIR}/AGPL-3.0-only;md5=73f1eb20517c55bf9493b7dd6e480788 \
    file://${COMMON_LICENSE_DIR}/BSD-3-Clause;md5=550794465ba0ec5312d6919e203a55f9 \
"

# https://github.com/rustdesk/rustdesk/releases/tag/1.5.3
SRC_URI = "https://github.com/rustdesk/rustdesk/releases/download/${PV}/rustdesk-${PV}-aarch64.deb;subdir=${BP}"
SRC_URI[sha256sum] = "fa85e8d3ccd45e2e303c2d1359af5dc51aa93d880e39ba47c9b04bfabd82ae19"

S = "${WORKDIR}/${BP}"

COMPATIBLE_HOST = "aarch64.*-linux"

# rustdesk-link.desktop: handler for rustdesk:// links
inherit mime-xdg

do_configure[noexec] = "1"
do_compile[noexec] = "1"

do_install() {
    install -d ${D}${datadir}/rustdesk ${D}${bindir}
    cp -r --no-preserve=ownership ${S}/usr/share/rustdesk/rustdesk ${S}/usr/share/rustdesk/lib \
        ${S}/usr/share/rustdesk/data ${D}${datadir}/rustdesk/
    ln -rs ${D}${datadir}/rustdesk/rustdesk ${D}${bindir}/rustdesk
    # Not installed: the root service (rustdesk --service, files/systemd) and
    # its polkit action; it only serves logind's seat0 session
    cp -r --no-preserve=ownership ${S}/usr/share/applications ${S}/usr/share/icons ${D}${datadir}/
}

FILES:${PN} += "${datadir}/rustdesk ${datadir}/icons"

# Keep the binaries as released, and don't offer RustDesk's private libraries
# (Flutter engine and plugins) to other packages
INHIBIT_PACKAGE_STRIP = "1"
INHIBIT_PACKAGE_DEBUG_SPLIT = "1"
EXCLUDE_FROM_SHLIBS = "1"
INSANE_SKIP:${PN} = "already-stripped ldflags textrel file-rdeps libdir dev-so"

# Linked: GTK 3, X11/XCB (also used on Wayland for the UI toolkit), PulseAudio
# (audio), GStreamer. Wayland capture: GStreamer pipewiresrc ! videoconvert !
# appsink, through xdg-desktop-portal. Hardware codecs (libva) are optional.
RDEPENDS:${PN} = " \
    gtk+3 \
    libepoxy \
    libx11 \
    libxcb \
    libxfixes \
    libxtst \
    libxkbcommon \
    wayland \
    dbus-lib \
    libpulse \
    libpulse-simple \
    gstreamer1.0 \
    gstreamer1.0-plugins-base-app \
    gstreamer1.0-plugins-base-videoconvertscale \
    gstreamer1.0-pipewire \
    xdg-desktop-portal \
    procps \
"
