# Boot splash: no kernel or systemd messages on the display.
# - The kernel console goes to the serial port only (Toradex's boot script
#   adds console=tty1, which printed the kernel log on the display).
# - quiet/loglevel: only errors on the serial console; the full log stays
#   available with dmesg and journalctl. For a verbose boot, set
#   "fw_setenv tdxargs loglevel=7" on the board (tdxargs comes last).
# - logo.nologo: no Tux logo on the display.
LABWC_DEMO_BOOT_ARGS ?= "quiet loglevel=3 logo.nologo"
APPEND:append = " ${LABWC_DEMO_BOOT_ARGS}"

do_compile:append() {
    sed -i '/^env set setupargs /s/console=tty1 //' boot.cmd
    if grep -q 'console=tty1' boot.cmd; then
        bbfatal "u-boot-distro-boot: could not remove console=tty1 from the boot script"
    fi
}
