SUMMARY = "Argos boot splash (Plymouth theme)"
DESCRIPTION = "Plymouth two-step theme: the Alakai logo and a spinning red \
starburst on black, shown from when the display comes up until the labwc \
desktop is running (see labwc.service). Also shown at shutdown."
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302"

# The images are generated from the desktop wallpaper with
# scripts/make-boot-splash.py of this layer
SRC_URI = " \
    file://argos.plymouth \
    file://theme/ \
"

S = "${WORKDIR}"

THEME_DIR = "${datadir}/plymouth/themes/argos"

do_install() {
    install -d ${D}${THEME_DIR}
    install -m 0644 ${WORKDIR}/argos.plymouth ${D}${THEME_DIR}/argos.plymouth
    install -m 0644 ${WORKDIR}/theme/*.png ${D}${THEME_DIR}/
    # Password/entry dialog images (needed by the two-step plugin) from
    # Plymouth's spinner theme
    for f in lock.png entry.png bullet.png capslock.png keyboard.png keymap-render.png; do
        ln -sf ../spinner/$f ${D}${THEME_DIR}/$f
    done
}

FILES:${PN} = "${THEME_DIR}"
RDEPENDS:${PN} = "plymouth"
