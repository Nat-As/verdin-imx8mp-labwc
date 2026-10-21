# Name the board <LABWC_DEMO_HOSTNAME_PREFIX>-<serial number> (e.g.
# argos-15629353) instead of ${MACHINE}-<serial number>. The prefix is set in
# conf/layer.conf. set-hostname.service runs on first boot, while
# /etc/hostname is empty.
do_install:append() {
    sed -i 's/^hostname=${MACHINE}-/hostname=${LABWC_DEMO_HOSTNAME_PREFIX}-/' ${D}${bindir}/sethostname
    grep -q '^hostname=${LABWC_DEMO_HOSTNAME_PREFIX}-' ${D}${bindir}/sethostname || \
        bbfatal "set-hostname: could not set the hostname prefix in sethostname"
}
