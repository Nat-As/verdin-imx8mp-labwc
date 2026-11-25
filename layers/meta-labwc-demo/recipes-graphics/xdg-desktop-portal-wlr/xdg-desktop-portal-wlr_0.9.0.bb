SUMMARY = "xdg-desktop-portal backend for wlroots with ScreenCast and RemoteDesktop"
DESCRIPTION = "xdg-desktop-portal-wlr 0.9.0 (meta-multimedia has 0.7.0) with the \
RemoteDesktop interface of pull request #325: screen capture (wlr-screencopy) \
plus pointer and keyboard input (virtual pointer/keyboard protocols) for remote \
desktop applications such as RustDesk on labwc. Also captures compositors \
without linux-dmabuf (pixman renderer) again, with shm buffers."
HOMEPAGE = "https://github.com/emersion/xdg-desktop-portal-wlr"
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://LICENSE;md5=e316e9609dd7672b87ff25b46b2cf3e1"

DEPENDS = " \
    wayland \
    wayland-native \
    wayland-protocols \
    libdrm \
    libinih \
    pipewire \
    virtual/libgbm \
"

inherit meson pkgconfig features_check
REQUIRED_DISTRO_FEATURES = "opengl wayland"

# meta-freescale disables this recipe for the i.MX GPU because older imx-gpu-viv
# libgbm lacked gbm_bo_create_with_modifiers2(); the one in this BSP (NXP
# 6.6.52-2.2.2) exports it, and gbm_bo_get_fd_for_plane()
COMPATIBLE_HOST:pn-xdg-desktop-portal-wlr:imxgpu = ".*"

# v0.9.0
SRC_URI = " \
    git://github.com/emersion/xdg-desktop-portal-wlr.git;protocol=https;branch=master \
    file://0001-Add-the-RemoteDesktop-portal-interface-PR-325.patch \
    file://0002-protocols-bundle-ext-image-capture-source-and-ext-im.patch \
    file://0003-screencast-allow-wlr_screencopy-without-linux-dmabuf.patch \
"
SRCREV = "8388b8a6214470462146840e92909aff5983eeac"

S = "${WORKDIR}/git"

PACKAGECONFIG ?= "${@bb.utils.contains('DISTRO_FEATURES', 'systemd', 'systemd', 'basu', d)}"

PACKAGECONFIG[man-pages] = "-Dman-pages=enabled,-Dman-pages=disabled,scdoc-native"
PACKAGECONFIG[systemd] = "-Dsystemd=enabled -Dsd-bus-provider=libsystemd,-Dsystemd=disabled"
PACKAGECONFIG[basu] = "-Dsd-bus-provider=basu,,basu"

# Which backend xdg-desktop-portal uses on wlroots desktops
# (XDG_CURRENT_DESKTOP=wlroots); upstream ships it in contrib/ only
do_install:append() {
    install -Dm0644 ${S}/contrib/wlroots-portals.conf ${D}${datadir}/xdg-desktop-portal/wlroots-portals.conf
    # No xdg-desktop-portal-gtk in the image: use wlr for everything it has
    sed -i -e 's/^default=gtk$/default=wlr/' -e '/Settings=darkman/d' \
        ${D}${datadir}/xdg-desktop-portal/wlroots-portals.conf
    install -Dm0644 ${S}/contrib/config.sample ${D}${datadir}/doc/${BPN}/config.sample
}

FILES:${PN} += "${systemd_user_unitdir} ${datadir}"

RDEPENDS:${PN} = "xdg-desktop-portal pipewire"
