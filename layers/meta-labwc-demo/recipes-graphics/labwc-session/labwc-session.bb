SUMMARY = "labwc desktop session for the labwc demo image"
DESCRIPTION = "systemd unit that autostarts labwc as the unprivileged desktop user \
on tty7 (seat via seatd, user session via PAM/pam_systemd), a session \
wrapper that picks a working GPU renderer (Vulkan, GLES2, pixman fallback), and default labwc, waybar, foot and \
fuzzel configuration plus a wallpaper."
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302"

SRC_URI = " \
    file://labwc-session \
    file://labwc-demo-outputs \
    file://labwc.service \
    file://labwc-autologin \
    file://labwc.default \
    file://labwc-wayland.sh \
    file://90-labwc-demo-gpu.rules \
    file://desktop-user.sudoers \
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

# The desktop user (LABWC_DEMO_DESKTOP_USER, conf/layer.conf). Its password is
# set by the image recipe.
DESKTOP_USER = "${LABWC_DEMO_DESKTOP_USER}"
USERADD_PACKAGES = "${PN}"
GROUPADD_PARAM:${PN} = "-r render; -r input; -r wayland"
USERADD_PARAM:${PN} = "--uid 1000 --create-home --home-dir /home/${DESKTOP_USER} --shell ${bindir}/fish \
    --user-group -G video,input,render,audio,wayland ${DESKTOP_USER}"

SYSTEMD_SERVICE:${PN} = "labwc.service"
SYSTEMD_AUTO_ENABLE:${PN} = "enable"

do_install() {
    install -Dm0755 ${WORKDIR}/labwc-session ${D}${bindir}/labwc-session
    install -Dm0755 ${WORKDIR}/labwc-demo-outputs ${D}${bindir}/labwc-demo-outputs
    install -Dm0644 ${WORKDIR}/labwc.service ${D}${systemd_system_unitdir}/labwc.service
    sed -i -e 's/@DESKTOP_USER@/${DESKTOP_USER}/g' ${D}${systemd_system_unitdir}/labwc.service
    install -Dm0644 ${WORKDIR}/labwc-autologin ${D}${sysconfdir}/pam.d/labwc-autologin
    install -Dm0644 ${WORKDIR}/labwc.default ${D}${sysconfdir}/default/labwc
    install -Dm0644 ${WORKDIR}/labwc-wayland.sh ${D}${sysconfdir}/profile.d/labwc-wayland.sh
    install -Dm0644 ${WORKDIR}/90-labwc-demo-gpu.rules ${D}${sysconfdir}/udev/rules.d/90-labwc-demo-gpu.rules
    install -Dm0440 ${WORKDIR}/desktop-user.sudoers ${D}${sysconfdir}/sudoers.d/${DESKTOP_USER}
    sed -i -e 's/@DESKTOP_USER@/${DESKTOP_USER}/g' ${D}${sysconfdir}/sudoers.d/${DESKTOP_USER}

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
    wlr-randr \
    gawk \
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
