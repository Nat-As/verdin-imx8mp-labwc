# CLAUDE.md

Yocto image for the **Toradex Verdin iMX8M Plus** on the **Mallow** carrier board that boots into
a **labwc** Wayland desktop. Repository: https://github.com/Nat-As/verdin-imx8mp-labwc

## Rules (always follow)

1. **Versioning**: every release gets a new version `va.b.c`:
   - `c`: minor tweaks/fixes (e.g. package additions, config changes)
   - `b`: larger changes
   - `a`: major changes / large compatibility differences
   - **v1.0.0** (2026-10-26) is the first official release; v0.x.y were pre-releases. Releases are
     published as official GitHub releases (marked latest), not pre-releases.
   - Create an annotated git tag with exactly that name (`git tag -a v0.0.3 -m "..."`) for each release.
2. **README.md** must be updated for each release: correct information, and the download link,
   size and SHA-256 of the new release asset.
3. **Hardware verification before push**: after a release is committed, the user must test the
   image **on real hardware**. Do **not** `git push`, push tags, or create the GitHub release until
   the user confirms the hardware test passed.

## Release workflow

1. Set the new version in `config/release.conf` (`LABWC_DEMO_VERSION`; shown on the boot animation
   and in `/etc/argos-release`). Change the layer/config, run `./build.sh` (log:
   `build-logs/latest.log`), fix failures.
2. Verify locally: inspect the rootfs (`build/tmp/work/verdin_imx8mp-tdx-linux/labwc-demo-image/1.0/rootfs`)
   and run the QEMU boot test (see "Testing").
3. Stage the release assets in `release/vX.Y.Z/` (git-ignored):
   - `Verdin-iMX8MP_labwc-Demo-Image-Tezi_vX.Y.Z.tar` (copy of
     `build/deploy/images/verdin-imx8mp/Verdin-iMX8MP_labwc-Demo-Image-Tezi.tar`; GitHub drops `+`
     from asset names, hence the rename)
   - `...tar.sha256`, `labwc-Demo-Image-vX.Y.Z.manifest.txt` (image `.manifest`), `release-notes.md`
4. Update README.md (version, link, size, SHA-256), commit locally, hand the tarball to the user.
   The user installs it over the USB NCM link of Tezi (module 192.168.11.1, host 192.168.11.2):
   `./serve-tezi.sh vX.Y.Z` serves it over HTTP (port 8080) and announces it with Zeroconf
   (`_tezi._tcp`), so Tezi lists it by itself (verified with Tezi on hardware). Ctrl+C stops it.
5. **After the user confirms the hardware test**: `git push origin main`, wait for CI (`gh run watch`)
   to be green, `git tag -a vX.Y.Z` + `git push origin vX.Y.Z`,
   `gh release create vX.Y.Z --latest --notes-file release/vX.Y.Z/release-notes.md <assets>`,
   then verify the README download link (download + `sha256sum`) and the shields.io badges.

## Git

- Commit as the GitHub user **Nat-As** (repo-local identity, already configured):
  `user.name=Nat-As`, `user.email=42288915+Nat-As@users.noreply.github.com`. Do not change the
  global git config.
- `gh` is authenticated as Nat-As; pushes use SSH.
- The project root is both the git repo and a `repo`/Yocto workspace. `.gitignore` is a whitelist:
  only our files are tracked (`layers/meta-labwc-demo`, `build/conf/{local,bblayers}.conf`,
  `config/`, `manifests/`, scripts, docs). Never commit `layers/<third-party>`, `downloads/`,
  `sstate-cache/`, `build/tmp`, `release/`.

## Layout

| Path | Purpose |
|---|---|
| `setup.sh` | `repo init/sync`: Toradex BSP **7.7.0** (`tdxref/default.xml`) + `manifests/labwc-demo.xml` (meta-wayland, meta-qt6 6.11, NXP meta-imx `scarthgap-6.6.52-2.2.2`, all pinned; only `meta-imx/meta-imx-ml` is in `bblayers.conf`) |
| `build.sh` | Logged `bitbake -k labwc-demo-image` |
| `serve-tezi.sh` | Serve a release/build Tezi image to Toradex Easy Installer over the network (USB NCM) |
| `layers/meta-labwc-demo/scripts/nuget-inc.py` | Generate a recipe's pinned NuGet `SRC_URI` `.inc` from a host restore |
| `config/image-packages.conf`, `config/python-packages.conf` | Software lists (included from `build/conf/local.conf`) |
| `config/release.conf` | Image version `LABWC_DEMO_VERSION` (boot animation title, `/etc/argos-release`) |
| `docs/logos/` | Logos of the stack for the README (white tiles, readable in light and dark themes) |
| `build/conf/local.conf` | MACHINE, EULA, threads, Fortran toolchain, display overlays (7" DSI + HDMI) |
| `layers/meta-labwc-demo` | Our layer: image, labwc session, users, bbappends, backported recipes |
| `layers/meta-labwc-demo/conf/layer.conf` | Account names (`LABWC_DEMO_DESKTOP_USER`, `LABWC_DEMO_ADMIN_USER`) |
| `build-qemu-test/` | Test only: qemuarm64 kernel build + `qemu-boot-test.py` |
| `.github/workflows/build.yml` | CI (badge): lint + BitBake parse/resolve with package assertions |

## Image facts

- Accounts: desktop user `argos_user` (autologin session, no general sudo, NOPASSWD power off/reboot),
  admin `alakai_admin` (full sudo). Root login is disabled (no `debug-tweaks`). Shell: fish.
- labwc runs from `labwc.service` as the desktop user; seat via **seatd** (logind's
  `Session.Activate` needs a polkit daemon, which the image does not have).
- NetworkManager (connman removed, systemd-networkd/dnsmasq masked), BlueZ + Blueman.
- Python 3.12 + numpy/scipy/pandas/matplotlib/OpenCV/PyQt6, Qt 6.11, dev tools, all kernel
  modules, Realtek out-of-tree `r8152` (preferred via depmod override).
- GPU rendering (v0.2.0): wlroots is built with **only** the gles2 renderer (+ pixman) and three
  patches (`recipes-wlroots/wlroots/files/`, 0003 below): 0001 `WLR_EGL_SKIP_DEVICE` (EGL GBM platform instead
  of the EGL device lookup: stock wlroots segfaults in Vivante's `eglQueryDevicesEXT()`,
  gdb-confirmed) and 0002 `WLR_RENDERER_FINISH` (glFinish per frame, set by labwc-session; fixes
  black flashes in redrawn areas: Vivante gives no implicit-sync fence). `labwc-session` tries the
  strategies in `LABWC_RENDERERS` (default `gles2-render gles2 pixman`) and falls back on a crash,
  renderer errors in the first 20 s or no client response in 45 s; state in
  `/tmp/labwc-session-<uid>/`. It also forces a UTF-8 `LANG` (systemd gives `C`).
- Vulkan was removed in v0.2.0 (v0.1.0-v0.1.4 history): the Vivante VSI Vulkan driver writes
  images in imported (GBM/DMA-BUF) memory in its own tiled layout, so every wlroots Vulkan mode
  showed a black or scrambled screen; only Vulkan-allocated exported buffers worked. Don't
  reintroduce it unless asked. The BSP's Vulkan driver/vulkan-tools still come in via Toradex's
  `packagegroup-vulkan-tools-tdx-graphical`.
- OpenGL ES clients: the Vivante client EGL (no linux-dmabuf support) presents only through its
  vendor protocol `wl_viv`, which its libEGL creates server-side in `eglBindWaylandDisplayWL()`
  (also its own `wl_drm`). wlroots dropped that call in 0.15, so every GL client (GPU Test,
  glmark2) segfaulted on its first EGL call (v0.2.0 rev 1 hardware test, exit 139), with both
  `gles2` and `gles2-render`. wlroots patch 0003 binds the display (`WLR_EGL_NO_BIND_DISPLAY=1`
  disables it; wlroots' own wl_drm is then skipped), adds a fallback `wlr_buffer` type for EGL
  wl_buffers (checked after shm/dmabuf/drm/single-pixel, Weston's order) and imports them as
  `EGL_WAYLAND_BUFFER_WL` images, cached per buffer and re-bound per commit (as NXP Weston does),
  with a Y flip for `EGL_WAYLAND_Y_INVERTED_WL` = false. `labwc-hwaccel-check` reports
  `egl-bind`. GL apps log to the journal (`-t gpu-test`, `-t glmark2`); `gpu-test --journal` is
  used by the launcher entry only, so pipes and files get the output (hwaccel, probe).
- Boot splash (v0.1.3): Plymouth (meta-oe, bbappend: `drm` on, `initrd` off) with the two-step theme
  `argos` (`recipes-graphics/argos-boot-splash`; images from `scripts/make-boot-splash.py`: logo cut
  from the wallpaper + 30 red starburst spinner frames). Kernel fragment `display-builtin.cfg`
  builds DSIM/sn65dsi83/panel-lvds/pwm-bl/pwm-imx27/HDMI PHY in (display ready before userspace
  instead of ~6.5 s) and sets fbcon deferred takeover. `u-boot-distro-boot` bbappend: no
  `console=tty1`, `quiet loglevel=3 logo.nologo`. labwc runs on **tty1** (Plymouth's VT; no
  TTYReset/VHangup/VTDisallocate, which would make fbcon take over), conflicts with
  plymouth-quit/getty@tty1; `labwc-splash` deactivates Plymouth before labwc and quits it
  (`--retain-splash`) when labwc-session writes `/tmp/labwc-session-<uid>/ready`. U-Boot has no
  video support for this board (no LCDIFv3/DSIM/SN65DSI83 drivers): dark until the kernel.
- .NET (v0.1.4): own recipe `recipes-devtools/dotnet/dotnet_10.0.403.bb` (.NET 10 LTS binary SDK,
  runtime 10.0.14; based on meta-mono's, which only has 8.0.7; target: runtime + `dotnet-dev`
  SDK, plus `native` for building). `classes/labwc-demo-dotnet.bbclass` builds C# projects with
  dotnet-native (portable IL, `UseAppHost=false`, offline) into `${libdir}/<app>` + launcher.
  `opencvsharp` 4.6.0.20220608 (matches NXP OpenCV 4.6): native OpenCvSharpExtern built from git,
  managed dll + offline feed from nuget.org nupkgs (`/usr/share/nuget/opencvsharp-feed`).
  Apps: `gpu-test` and `opencv-cs-check` (OpenCL via mangled `cv::ocl` symbols).
- GPU Test (v0.2.1): Avalonia 12.1.4 with its native Wayland backend (`Avalonia.Wayland`,
  `UseWayland()`, opt-in since 12.1; EGL + OpenGL ES, `GlProfiles` forced to GLES 3.0/2.0 because
  on Mesa Avalonia picks desktop GL 4.x, where our GLSL ES shaders crash). Molecule in an
  `OpenGlControlBase` (`MoleculeRenderer`: own 4x MSAA FBO + depth, blit-resolved into Avalonia's
  FBO, transparent clear), Avalonia text overlay, window background 40 % opaque. Avalonia's
  Wayland render loop doesn't wait for the display (thousands of FPS on Mesa), so a pacer thread
  requests frames at `GPU_TEST_MAX_FPS` (default 60). `GPU_TEST_DMABUF=1` sets
  `UseDmabufSwapchain` (default off: wl_egl_window, the path proven on Vivante). `--headless`
  keeps the EGL GBM FBO path (Vivante has no default EGL display outside Wayland).
  NuGet: `dotnet restore -r linux-arm64`, then `scripts/nuget-inc.py` writes
  `gpu-test-nuget.inc` (21 pinned nupkgs into `${WORKDIR}/nuget-feed`); the dotnet class restores
  only from that feed and builds `-r linux-arm64 --self-contained false` (only arm64
  libSkiaSharp/libHarfBuzzSharp). Avalonia 12 needs `UseHarfBuzz()` (`Avalonia.HarfBuzz`).
  Avalonia.Wayland requires xdg_wm_base >= 3; labwc 0.7.1 offered v2, so the labwc bbappend
  backports upstream a98f2635 (xdg-shell v3 with popup repositioning, in labwc 0.7.3).
- Serial ports from C#: .NET 10's `System.IO.Ports` NuGet package (with
  `runtime.linux-arm64.runtime.native.System.IO.Ports`) covers it; the user decided against Mono
  and RJCP SerialPortStream (v0.2.1).
- Hostname: `argos-<serial>` via `recipes-core/set-hostname` bbappend
  (`LABWC_DEMO_HOSTNAME_PREFIX` in layer.conf).
- DRM on hardware: `card0`/`renderD128` = Vivante (galcore, no connectors), `card1` = imx-drm
  (HDMI-A-1, LVDS-1 via the DSI-to-LVDS bridge of the 7" display). The 7" panel output always
  reports connected; `labwc-demo-outputs` turns it off when the touch controller (I2C 1-0041)
  is absent.
- Diagnostics on target: `labwc-hwaccel-check` (PASS/WARN/FAIL per item) and `labwc-gpu-probe`
  (`--kms` for the real display), from `recipes-graphics/labwc-demo-gpu-tools`.
- GPU/NPU compute: OpenCL, OpenVX + libnn-imx, RapidOpenVX, G2D, TIM-VX, TensorFlow Lite 2.16 +
  VX delegate (`/usr/lib/libvx_delegate.so`), OpenCV with OpenCL (opencv bbappend).

## Testing

- **Hardware logs** (the user's SSH sessions, dmesg, journal, probe output) go to
  `logs/hardware/vX.Y.Z/<YYYY-MM-DD>_<what>.log` (git-ignored, like `release/`), where `vX.Y.Z` is
  the image version that was running. The user can drop a log in the project root; read it, then
  move it there. Logs can contain board details (serial numbers, addresses): never commit or
  upload them.

- Desktop log on target: `journalctl -b -t labwc` (not `-u labwc`).
- Full boot test (systemd, seatd, DRM/KMS on virtio-gpu):
  `build-qemu-test/run-kernel-build.sh` once, then
  `XDG_RUNTIME_DIR=/run/user/1000 python3 build-qemu-test/qemu-boot-test.py build-qemu-test/tmp/deploy/images/qemuarm64/Image <qcow2 overlay of the .wic> <outdir>`
  (keep socket paths short; it logs in as the admin user).
- Userspace checks without root: `unshare -r -m -p -f` + `chroot` into an extracted rootfs (binfmt
  qemu-aarch64). Pass `env -i` (the VS Code environment leaks `LOCPATH`). No system D-Bus there,
  so waybar exits in that environment only.

## Pitfalls

- Never start a second `bitbake` while a build is running in `build/`.
- Avoid editing recipes while a build runs (basehash/taskhash noise; rerun afterwards).
- sudo on the build host needs a password: no apt installs; host has no gfortran.
- Image manifests show Debian-renamed package names (`libg2d2`); `IMAGE_INSTALL` needs the
  original names (`imx-gpu-g2d`). Look them up in `build/tmp/pkgdata/<machine>/runtime-reverse/`.
- NXP's meta-imx is under the NXP Software License Agreement: pull it via the manifest, never copy
  its recipes into this MIT repository.
- Backports in our layer exist for Scarthgap gaps: sip 6.16.1 + python3-packaging 24.2 (PyQt6 6.11),
  setuptools-69 compatibility seds (pyqt6-sip, pyqt-builder, sip), scipy `f2py` isolation wrapper,
  matplotlib qhull location. OE's offline `systemctl` only supports `enable`/`mask`/`preset-all`.
