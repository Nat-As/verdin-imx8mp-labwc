SUMMARY = "labwc desktop session for the labwc demo image"
DESCRIPTION = "systemd unit that autostarts labwc as the unprivileged user 'dev' \
on tty7 (seat via seatd, user session via PAM/pam_systemd), a session \
wrapper with automatic pixman fallback, and default labwc, waybar, foot and \
fuzzel configuration plus a wallpaper."
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302"

SRC_URI = " \
    file://labwc-session \
    file://labwc.service \
    file://labwc-autologin \
    file://labwc.default \
    file://labwc-wayland.sh \
    file://90-labwc-demo-gpu.rules \
    file://dev.sudoers \
    file://rc.xml \
    file://menu.xml \
    file://autostart \
    file://environment \
    file://waybar-config.jsonc \
    file://waybar-style.css \
    file://glmark2.desktop \
    file://alakai-wallpaper.png \
    file://labwc-demo.fish \
"

S = "${WORKDIR}"

inherit systemd useradd features_check

REQUIRED_DISTRO_FEATURES = "wayland systemd pam"

# The desktop user. Its password ("dev") is set by the image recipe.
USERADD_PACKAGES = "${PN}"
GROUPADD_PARAM:${PN} = "-r render; -r input; -r wayland"
USERADD_PARAM:${PN} = "--create-home --home-dir /home/dev --shell ${bindir}/fish \
    --user-group -G video,input,render,audio,wayland dev"

SYSTEMD_SERVICE:${PN} = "labwc.service"
SYSTEMD_AUTO_ENABLE:${PN} = "enable"

do_install() {
    install -Dm0755 ${WORKDIR}/labwc-session ${D}${bindir}/labwc-session
    install -Dm0644 ${WORKDIR}/labwc.service ${D}${systemd_system_unitdir}/labwc.service
    install -Dm0644 ${WORKDIR}/labwc-autologin ${D}${sysconfdir}/pam.d/labwc-autologin
    install -Dm0644 ${WORKDIR}/labwc.default ${D}${sysconfdir}/default/labwc
    install -Dm0644 ${WORKDIR}/labwc-wayland.sh ${D}${sysconfdir}/profile.d/labwc-wayland.sh
    install -Dm0644 ${WORKDIR}/90-labwc-demo-gpu.rules ${D}${sysconfdir}/udev/rules.d/90-labwc-demo-gpu.rules
    install -Dm0440 ${WORKDIR}/dev.sudoers ${D}${sysconfdir}/sudoers.d/dev

    # labwc reads $XDG_CONFIG_HOME/labwc first, then $XDG_CONFIG_DIRS/labwc
    for f in rc.xml menu.xml autostart environment; do
        install -Dm0644 ${WORKDIR}/$f ${D}${sysconfdir}/xdg/labwc/$f
    done

    install -Dm0644 ${WORKDIR}/waybar-config.jsonc ${D}${sysconfdir}/xdg/waybar/config.jsonc
    install -Dm0644 ${WORKDIR}/waybar-style.css ${D}${sysconfdir}/xdg/waybar/style.css

    install -Dm0644 ${WORKDIR}/glmark2.desktop ${D}${datadir}/applications/glmark2-es2-wayland.desktop

    install -Dm0644 ${WORKDIR}/alakai-wallpaper.png ${D}${datadir}/backgrounds/labwc-demo/wallpaper.png
    install -Dm0644 ${WORKDIR}/labwc-demo.fish ${D}${sysconfdir}/fish/conf.d/labwc-demo.fish
}

FILES:${PN} += " \
    ${systemd_system_unitdir}/labwc.service \
    ${sysconfdir}/xdg \
    ${datadir}/applications \
    ${datadir}/backgrounds \
"

CONFFILES:${PN} += " \
    ${sysconfdir}/default/labwc \
    ${sysconfdir}/xdg/labwc/rc.xml \
    ${sysconfdir}/xdg/labwc/menu.xml \
    ${sysconfdir}/xdg/labwc/autostart \
    ${sysconfdir}/xdg/labwc/environment \
    ${sysconfdir}/xdg/waybar/config.jsonc \
    ${sysconfdir}/xdg/waybar/style.css \
"
# foot.ini and fuzzel.ini are installed by the foot/fuzzel bbappends

RDEPENDS:${PN} = " \
    labwc \
    seatd \
    waybar \
    foot \
    fuzzel \
    swaybg \
    font-awesome-otf \
    ttf-dejavu-sans \
    ttf-dejavu-sans-mono \
    ttf-dejavu-serif \
    fontconfig-utils \
    xkeyboard-config \
    ncurses-terminfo-base \
    bash \
    fish \
    sudo \
    htop \
    kbd \
    libpam-runtime \
    pam-plugin-env \
    pam-plugin-loginuid \
    pam-plugin-nologin \
    pam-plugin-unix \
"

# glmark2 desktop entry only makes sense with the GPU stack present
RRECOMMENDS:${PN} = "glmark2 adwaita-icon-theme-cursors gdk-pixbuf-loader-png networkmanager-nmtui blueman"
