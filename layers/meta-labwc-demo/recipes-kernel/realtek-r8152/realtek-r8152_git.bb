SUMMARY = "Realtek r8152 driver for RTL8152/RTL8153/RTL8156 USB Ethernet adapters"
DESCRIPTION = "Out-of-tree Realtek r8152 USB Ethernet driver (v2.21.x). It is \
installed in /lib/modules/<version>/updates and preferred over the in-kernel \
r8152 module via /etc/depmod.d. The udev rule switches supported adapters to \
the configuration handled by this driver."
HOMEPAGE = "https://github.com/Nat-As/realtek-r8152-linux"
LICENSE = "GPL-2.0-only"
LIC_FILES_CHKSUM = "file://LICENSE;md5=b234ee4d69f5fce4486a80fdaf4a4263"

SRC_URI = " \
    git://github.com/Nat-As/realtek-r8152-linux.git;protocol=https;branch=master \
    file://realtek-r8152.conf \
"
SRCREV = "3bf045ebe829ca27bc9d832b962ff4ea37fc8e77"
PV = "2.21.4+git${SRCPV}"

S = "${WORKDIR}/git"

inherit module

# The in-kernel driver is packaged as kernel-module-r8152; use a distinct
# package name for this one.
KERNEL_MODULE_PACKAGE_PREFIX = "realtek-"

EXTRA_OEMAKE += "KERNELDIR=${STAGING_KERNEL_DIR}"
MAKE_TARGETS = "modules"

# The upstream Makefile's install target deletes the in-tree driver and runs
# modprobe on the build host; install the module directly instead.
do_install() {
    install -Dm0644 ${B}/r8152.ko ${D}${nonarch_base_libdir}/modules/${KERNEL_VERSION}/updates/r8152.ko
    install -Dm0644 ${S}/50-usb-realtek-net.rules ${D}${nonarch_base_libdir}/udev/rules.d/50-usb-realtek-net.rules
    install -Dm0644 ${WORKDIR}/realtek-r8152.conf ${D}${sysconfdir}/depmod.d/realtek-r8152.conf
}

FILES:${PN} += " \
    ${nonarch_base_libdir}/udev/rules.d/50-usb-realtek-net.rules \
    ${sysconfdir}/depmod.d/realtek-r8152.conf \
"

# Realtek USB NIC firmware (rtl_nic/rtl815x*.fw) from linux-firmware
RRECOMMENDS:${PN} += "linux-firmware-rtl-nic"
