SUMMARY = "Administrator account of the labwc demo image"
DESCRIPTION = "Creates the administrator login (LABWC_DEMO_ADMIN_USER, \
conf/layer.conf) with full sudo rights. Direct root login is disabled in the \
image, so administration is done with this account and sudo. Its password is \
set by the image recipe."
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302"

SRC_URI = "file://admin-user.sudoers"

S = "${WORKDIR}"

inherit useradd allarch

ADMIN_USER = "${LABWC_DEMO_ADMIN_USER}"

USERADD_PACKAGES = "${PN}"
USERADD_PARAM:${PN} = "--uid 1001 --create-home --home-dir /home/${ADMIN_USER} --shell ${bindir}/fish \
    --user-group ${ADMIN_USER}"

do_install() {
    install -Dm0440 ${WORKDIR}/admin-user.sudoers ${D}${sysconfdir}/sudoers.d/${ADMIN_USER}
    sed -i -e 's/@ADMIN_USER@/${ADMIN_USER}/g' ${D}${sysconfdir}/sudoers.d/${ADMIN_USER}
}

FILES:${PN} = "${sysconfdir}/sudoers.d"

RDEPENDS:${PN} = "sudo fish"
