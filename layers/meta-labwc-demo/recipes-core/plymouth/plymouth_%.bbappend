# Boot splash for the labwc demo image (theme: argos-boot-splash).
# - drm: render with KMS on i.MX8MP (meta-oe only enables it on x86)
# - no initrd: the image boots without an initramfs (and the initrd package
#   needs dracut, which is not available)
# - only the themes the image uses as fallbacks
PACKAGECONFIG:append = " drm"
PACKAGECONFIG:remove = "initrd"
PLYMOUTH_THEMES = "spinner text details"

# Default theme: argos (argos-boot-splash). No delay, so the splash shows as
# soon as the display is ready; wait up to 8 s for the display to appear.
do_install:append() {
    cat > ${D}${sysconfdir}/plymouth/plymouthd.conf <<CONF
[Daemon]
Theme=argos
ShowDelay=0
DeviceTimeout=8
CONF
}
