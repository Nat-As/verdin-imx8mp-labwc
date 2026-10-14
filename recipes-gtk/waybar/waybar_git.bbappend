# Trim optional modules we don't use to keep the dependency tree small
# (no audio server, media players or battery on this board).
PACKAGECONFIG:remove = "mpris upower_glib pulseaudio pipewire wireplumber"

# Our default bar config/style are shipped by labwc-session in
# /etc/xdg/waybar; drop the upstream sample to avoid file conflicts.
do_install:append() {
    rm -f ${D}${sysconfdir}/xdg/waybar/config ${D}${sysconfdir}/xdg/waybar/config.jsonc \
          ${D}${sysconfdir}/xdg/waybar/style.css
}
