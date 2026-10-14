SUMMARY = "Toradex Easy Installer U-Boot environment that selects the Mallow device tree"
DESCRIPTION = "Takes the default U-Boot environment deployed by u-boot-toradex \
(the file Tezi writes to the module, see UBOOT_ENV_TEZI) and sets \
fdt_board=${TEZI_FDT_BOARD}. U-Boot's preboot then derives \
fdtfile=imx8mp-verdin-<wifi|nonwifi>-${TEZI_FDT_BOARD}.dtb from the module's \
config block, so both Wi-Fi and non-Wi-Fi SoMs boot the Mallow device tree."
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302"

INHIBIT_DEFAULT_DEPS = "1"
PACKAGE_ARCH = "${MACHINE_ARCH}"
COMPATIBLE_MACHINE = "verdin-imx8mp"

inherit deploy nopackages

TEZI_FDT_BOARD ?= "mallow"
# Name of the environment file deployed by u-boot-toradex for the "sd" config
UBOOT_ENV_SRC ?= "u-boot-initial-env-${UBOOT_CONFIG}"
UBOOT_ENV_MALLOW ?= "${UBOOT_ENV_SRC}-${TEZI_FDT_BOARD}"

do_configure[noexec] = "1"
do_compile[noexec] = "1"
do_install[noexec] = "1"

do_deploy[depends] += "virtual/bootloader:do_deploy"
do_deploy() {
    src="${DEPLOY_DIR_IMAGE}/${UBOOT_ENV_SRC}"
    if [ ! -f "$src" ]; then
        bbfatal "U-Boot environment $src not found"
    fi

    # Select the carrier board, keep everything else as is. fdtfile must not
    # be pinned in the environment, otherwise preboot does not derive it.
    sed -e '/^fdt_board=/d' -e '/^fdtfile=/d' "$src" > ${B}/env.tmp
    echo "fdt_board=${TEZI_FDT_BOARD}" >> ${B}/env.tmp
    install -m 0644 ${B}/env.tmp ${DEPLOYDIR}/${UBOOT_ENV_MALLOW}
}
addtask deploy after do_install before do_build
