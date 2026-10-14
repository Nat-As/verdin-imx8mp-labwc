# Toradex reference multimedia image with the Weston session replaced by a
# minimal labwc Wayland desktop (waybar, fuzzel, foot, swaybg).
require recipes-images/images/tdx-reference-multimedia-image.bb

SUMMARY = "Toradex Embedded Linux labwc Demo Image"
DESCRIPTION = "Reference multimedia image with a labwc Wayland desktop \
(waybar, fuzzel, foot, swaybg) instead of Weston. Selects the Mallow \
carrier board device tree on Verdin iMX8M Plus."

# Prefix of the deployable tarball name
export IMAGE_BASENAME = "labwc-Demo-Image"

# --- Replace Weston with labwc ---------------------------------------------
IMAGE_FEATURES:remove = "weston"
APP_LAUNCH_WAYLAND = ""

# Drop Weston/X11 helpers and the Qt5 demo stack (Qt demos and their Weston
# launchers are not part of a minimal labwc desktop) as well as gpicview,
# which is a GTK2/X11 application and would need Xwayland.
IMAGE_INSTALL:remove = " \
    weston-xwayland \
    xterm \
    packagegroup-tdx-qt5 \
    gpicview \
"

IMAGE_INSTALL += " \
    labwc-session \
    wlr-randr \
    wl-clipboard \
"

# SSH server: Toradex images already ship dropbear (packagegroup-basic); make
# it explicit. debug-tweaks (local.conf) allows root login with an empty
# password; the "dev" user has password "dev". See README.md.
IMAGE_FEATURES += "ssh-server-dropbear"

# Boot into the graphical target so labwc.service starts
SYSTEMD_DEFAULT_TARGET = "graphical.target"

# --- Development login --------------------------------------------------------
# User "dev" is created by labwc-session; set its password to "dev".
# Hash generated with: openssl passwd -6 -salt labwcdemo dev
LABWC_DEMO_DEV_PASSWORD_HASH = "$6$labwcdemo$NCE3oMD5Q7/pCJv69NBtd38SRDxNB3gQZ4ALLLNQqM2fHlpe7ERLLog/i6UvbKuj4nr0PnkJc8sw4q8PL8sfw0"

python set_dev_password () {
    shadow = os.path.join(d.getVar('IMAGE_ROOTFS'), 'etc', 'shadow')
    pw = d.getVar('LABWC_DEMO_DEV_PASSWORD_HASH')
    with open(shadow) as f:
        lines = f.read().splitlines()
    found = False
    for i, line in enumerate(lines):
        fields = line.split(':')
        if fields[0] == 'dev':
            fields[1] = pw
            lines[i] = ':'.join(fields)
            found = True
    if not found:
        bb.fatal("user 'dev' not found in %s" % shadow)
    with open(shadow, 'w') as f:
        f.write('\n'.join(lines) + '\n')
}
ROOTFS_POSTPROCESS_COMMAND += "set_dev_password;"

# --- Toradex Easy Installer: select the Mallow carrier board -----------------
# Tezi writes this U-Boot environment to the module. It is the default
# environment with fdt_board=mallow, so U-Boot loads
# imx8mp-verdin-<wifi|nonwifi>-mallow.dtb.
UBOOT_ENV_TEZI_EMMC:verdin-imx8mp = "u-boot-initial-env-sd-mallow"
do_image_teziimg[depends] += "${@'u-boot-env-mallow:do_deploy' if d.getVar('MACHINE') == 'verdin-imx8mp' else ''}"
