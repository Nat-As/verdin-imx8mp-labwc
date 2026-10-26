# Toradex reference image with a minimal labwc Wayland desktop (waybar,
# fuzzel, foot, swaybg) instead of Weston.
#
# Based on tdx-reference-minimal-image plus the package groups of
# tdx-reference-multimedia-image (that recipe itself is not used: it is tied to
# Qt 5 / Weston, while this image uses Qt 6 from meta-qt6).
#
# Extra software is configured in config/image-packages.conf and
# config/python-packages.conf (included from build/conf/local.conf).
require recipes-images/images/tdx-reference-minimal-image.bb

SUMMARY = "Toradex Embedded Linux labwc Demo Image"
DESCRIPTION = "Reference image with a labwc Wayland desktop (waybar, fuzzel, \
foot, swaybg) instead of Weston, development tools, Python and Qt 6. \
Selects the Mallow carrier board device tree on Verdin iMX8M Plus."

# Prefix of the deployable tarball name
export IMAGE_BASENAME = "labwc-Demo-Image"

# Lists from config/*.conf (empty if those files are not included)
LABWC_DEMO_PACKAGES ??= ""
LABWC_DEMO_PYTHON_PACKAGES ??= ""
LABWC_DEMO_IMAGE_FEATURES ??= ""
LABWC_DEMO_EXCLUDE ??= ""

# --- Content of tdx-reference-multimedia-image, without Weston and Qt 5 -------
IMAGE_INSTALL += " \
    ${@bb.utils.contains("MACHINE_FEATURES", "tpm2", "packagegroup-tpm2-tdx-cli", "", d)} \
    packagegroup-tdx-cli \
    packagegroup-tdx-graphical \
    packagegroup-fsl-isp \
    \
    bash \
    coreutils \
    less \
    makedevs \
    mime-support \
    net-tools \
    util-linux \
    v4l-utils \
    media-files \
"

# --- labwc desktop ------------------------------------------------------------
# Boot splash (Plymouth, Alakai logo and spinner) from the moment the display
# comes up until the desktop runs; see argos-boot-splash and labwc.service
IMAGE_INSTALL += " \
    plymouth \
    argos-boot-splash \
    labwc-session \
    labwc-demo-admin \
    wlr-randr \
    wl-clipboard \
"

# --- Configured software (config/*.conf) ----------------------------------------
IMAGE_INSTALL += "${LABWC_DEMO_PACKAGES} ${LABWC_DEMO_PYTHON_PACKAGES}"
IMAGE_FEATURES += "${LABWC_DEMO_IMAGE_FEATURES}"
BAD_RECOMMENDATIONS += "${LABWC_DEMO_EXCLUDE}"

# NetworkManager (config/image-packages.conf) replaces Toradex's connman
CONMANPKGS = ""

# NetworkManager manages all interfaces: mask systemd-networkd (whose default
# 80-wired.network would also run DHCP on Ethernet) and the system-wide
# dnsmasq service (NetworkManager runs its own dnsmasq for shared connections).
LABWC_DEMO_DISABLED_SERVICES ?= " \
    systemd-networkd.service \
    systemd-networkd.socket \
    systemd-networkd-wait-online.service \
    dnsmasq.service \
"

disable_conflicting_services () {
    if [ -e ${IMAGE_ROOTFS}${sbindir}/NetworkManager ]; then
        for s in ${LABWC_DEMO_DISABLED_SERVICES}; do
            if [ -e ${IMAGE_ROOTFS}${systemd_system_unitdir}/$s ]; then
                systemctl --root=${IMAGE_ROOTFS} mask $s
            fi
        done
    fi
}
ROOTFS_POSTPROCESS_COMMAND += "disable_conflicting_services;"

# SSH server: Toradex images ship dropbear (packagegroup-basic); make it
# explicit. Root login is disabled (no debug-tweaks in local.conf): log in as
# the desktop or admin user (see "Logins" below).
IMAGE_FEATURES += "ssh-server-dropbear"

# Boot into the graphical target so labwc.service starts
SYSTEMD_DEFAULT_TARGET = "graphical.target"

# --- Logins -------------------------------------------------------------------
# The desktop user (labwc-session) and the admin user (labwc-demo-admin) are
# named in conf/layer.conf (LABWC_DEMO_DESKTOP_USER / LABWC_DEMO_ADMIN_USER).
# Root has no password and cannot log in; the admin user has sudo.
# Hashes generated with: openssl passwd -6 -salt <salt> <password>
#   argos_user   / argos_user
#   alakai_admin / alakai_admin
LABWC_DEMO_DESKTOP_PASSWORD_HASH ?= "$6$argosuser$srko56.K9Wjx2gBB3qcwOd8uhu3rjyKFMKQDcPB7G1Fh5SWsf07V2Edju.WxyO8R9PiSv5qtTY7oHVqd94pXr1"
LABWC_DEMO_ADMIN_PASSWORD_HASH ?= "$6$alakaiadm$WpIBw9WmCQav/UgMhWcKDHXk/5J/S4AIv/J.XjndUdu/9eT/wtXzxiki4wutNs2djPoaJaRMmWHP.4LkRTseu0"

# Login shell for root (used by "sudo -i"). Must be listed in /etc/shells.
LABWC_DEMO_ROOT_SHELL ?= "${bindir}/fish"

python set_passwords () {
    shadow = os.path.join(d.getVar('IMAGE_ROOTFS'), 'etc', 'shadow')
    passwords = {
        d.getVar('LABWC_DEMO_DESKTOP_USER'): d.getVar('LABWC_DEMO_DESKTOP_PASSWORD_HASH'),
        d.getVar('LABWC_DEMO_ADMIN_USER'): d.getVar('LABWC_DEMO_ADMIN_PASSWORD_HASH'),
    }
    with open(shadow) as f:
        lines = f.read().splitlines()
    done = set()
    for i, line in enumerate(lines):
        fields = line.split(':')
        if fields[0] in passwords:
            fields[1] = passwords[fields[0]]
            lines[i] = ':'.join(fields)
            done.add(fields[0])
    missing = set(passwords) - done
    if missing:
        bb.fatal("users %s not found in %s" % (", ".join(sorted(missing)), shadow))
    with open(shadow, 'w') as f:
        f.write('\n'.join(lines) + '\n')
}

python set_root_shell () {
    rootfs = d.getVar('IMAGE_ROOTFS')
    shell = d.getVar('LABWC_DEMO_ROOT_SHELL')
    if not shell:
        return
    if not os.path.exists(rootfs + shell):
        bb.fatal("LABWC_DEMO_ROOT_SHELL %s is not installed in the image" % shell)
    with open(os.path.join(rootfs, 'etc', 'shells')) as f:
        if shell not in f.read().split():
            bb.fatal("%s is not listed in /etc/shells" % shell)
    passwd = os.path.join(rootfs, 'etc', 'passwd')
    with open(passwd) as f:
        lines = f.read().splitlines()
    for i, line in enumerate(lines):
        fields = line.split(':')
        if fields[0] == 'root':
            fields[6] = shell
            lines[i] = ':'.join(fields)
    with open(passwd, 'w') as f:
        f.write('\n'.join(lines) + '\n')
}

ROOTFS_POSTPROCESS_COMMAND += "set_passwords; set_root_shell;"

# --- Toradex Easy Installer: select the Mallow carrier board -----------------
# Tezi writes this U-Boot environment to the module. It is the default
# environment with fdt_board=mallow, so U-Boot loads
# imx8mp-verdin-<wifi|nonwifi>-mallow.dtb.
UBOOT_ENV_TEZI_EMMC:verdin-imx8mp = "u-boot-initial-env-sd-mallow"
do_image_teziimg[depends] += "${@'u-boot-env-mallow:do_deploy' if d.getVar('MACHINE') == 'verdin-imx8mp' else ''}"
