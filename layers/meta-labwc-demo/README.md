# meta-labwc-demo

Yocto layer that turns the Toradex reference multimedia image into a minimal
labwc Wayland desktop for the Verdin iMX8M Plus on the Mallow carrier board.

See the README.md in the project root for build and usage instructions.

Contents:

- `recipes-images/images/labwc-demo-image.bb`: the image (based on
  `tdx-reference-multimedia-image`, with Weston replaced by labwc)
- `recipes-graphics/labwc-session`: systemd unit, PAM service, session
  wrapper (GPU renderer strategies Vulkan / GLES2 with fallback to pixman),
  7" panel output detection, default labwc and waybar config, wallpaper, and
  the desktop user (`LABWC_DEMO_DESKTOP_USER`)
- `recipes-graphics/labwc-demo-gpu-tools`: `labwc-gpu-probe` and
  `labwc-hwaccel-check` diagnostics
- `recipes-core/labwc-demo-admin`: administrator account with sudo
  (`LABWC_DEMO_ADMIN_USER`); root login is disabled
- `recipes-bsp/u-boot-env-mallow`: Tezi U-Boot environment with
  `fdt_board=mallow`, so the Mallow device tree is selected on first boot
- `recipes-wlroots`, `recipes-gtk`, `recipes-support`: bbappends for wlroots
  (GLES2 + Vulkan renderers against Vivante, with patches for the Vivante EGL
  and Vulkan drivers), labwc (no Xwayland), waybar (trimmed modules), foot and
  fuzzel (demo default config; fuzzel without librsvg), OpenCV (OpenCL)
