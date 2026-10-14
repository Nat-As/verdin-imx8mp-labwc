# meta-labwc-demo

Yocto layer that turns the Toradex reference multimedia image into a minimal
labwc Wayland desktop for the Verdin iMX8M Plus on the Mallow carrier board.

See the README.md in the project root for build and usage instructions.

Contents:

- `recipes-images/images/labwc-demo-image.bb`: the image (based on
  `tdx-reference-multimedia-image`, with Weston replaced by labwc)
- `recipes-graphics/labwc-session`: systemd unit, PAM service, session
  wrapper (hardware renderer with pixman fallback), default labwc and waybar
  config, wallpaper, and the `dev` user
- `recipes-bsp/u-boot-env-mallow`: Tezi U-Boot environment with
  `fdt_board=mallow`, so the Mallow device tree is selected on first boot
- `recipes-wlroots`, `recipes-gtk`, `recipes-support`: bbappends for wlroots
  (GLES2/gbm against Vivante), labwc (no Xwayland), waybar (trimmed modules),
  foot and fuzzel (demo default config; fuzzel without librsvg)
