# labwc desktop for Toradex Verdin iMX8M Plus on Mallow (Yocto)

[![build](https://img.shields.io/github/actions/workflow/status/Nat-As/verdin-imx8mp-labwc/build.yml?branch=main&label=build)](https://github.com/Nat-As/verdin-imx8mp-labwc/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/Nat-As/verdin-imx8mp-labwc?include_prereleases&label=release)](https://github.com/Nat-As/verdin-imx8mp-labwc/releases)
[![downloads](https://img.shields.io/github/downloads/Nat-As/verdin-imx8mp-labwc/total?label=downloads)](https://github.com/Nat-As/verdin-imx8mp-labwc/releases)
[![license](https://img.shields.io/github/license/Nat-As/verdin-imx8mp-labwc?label=license)](LICENSE)

This project builds, from source, a Toradex Easy Installer (Tezi) image for the
**Verdin iMX8M Plus** SoM on the **Mallow** carrier board. The image boots
into a minimal **labwc** Wayland desktop (not Weston) with:

| Role | Component |
|---|---|
| Compositor | labwc 0.7.1 (wlroots 0.17.2) |
| Panel | waybar 0.10.0 |
| Application launcher | fuzzel 1.10.2 |
| Terminal | foot 1.17.2 |
| Wallpaper | swaybg 1.2.0 (bundled 1920x1080 PNG) |
| Fonts | DejaVu Sans/Mono/Serif, Font Awesome 6 (panel icons) |

The compositor runs at boot as the unprivileged user `dev`, from a systemd unit, directly on
DRM/KMS (no X11, no Xwayland). Seat access goes through **seatd**. SSH (dropbear) is enabled.

## Download

**Pre-release v0.0.1** (revision 2, confirmed booting into labwc on a Verdin iMX8M Plus + Mallow):

**[⬇ Verdin-iMX8MP_labwc-Demo-Image-Tezi_v0.0.1.tar](https://github.com/Nat-As/verdin-imx8mp-labwc/releases/download/v0.0.1/Verdin-iMX8MP_labwc-Demo-Image-Tezi_v0.0.1.tar)** (148 MB) &nbsp;·&nbsp;
[sha256](https://github.com/Nat-As/verdin-imx8mp-labwc/releases/download/v0.0.1/Verdin-iMX8MP_labwc-Demo-Image-Tezi_v0.0.1.tar.sha256): `980b43770ffedb3bbc96fab23e21089ccd73a6583a9620bcb61ad354835cc2f3` &nbsp;·&nbsp;
[release notes](https://github.com/Nat-As/verdin-imx8mp-labwc/releases/tag/v0.0.1)

Extract it onto a USB stick and install it with Toradex Easy Installer (section 4). Log in with
`dev`/`dev` (section 6). The badge above shows the CI status: lint, then a BitBake parse and
task-graph resolution of the image against the full BSP. See section 2 for what CI does and doesn't build.

## Revision 2: fix for "boots to a console login instead of labwc"

The first flashed image booted to a text login, and running `labwc` by hand failed with
`cannot create X11 backend: disabled at compile-time` / `unable to create backend`. There
were two separate problems, both fixed in revision 2.

1. **labwc could not get a seat at boot (the main bug).** The session used libseat's *logind*
   backend, like OE-core's `weston.service`. That backend calls
   `org.freedesktop.login1.Session.Activate`. On this distro, systemd-logind is **built with polkit**
   (`DISTRO_FEATURES` contains `polkit`), so for any non-root caller it asks polkit to authorise
   `org.freedesktop.login1.chvt`. The image has no polkit daemon, so the call fails, and libseat
   treats that as fatal (seatd 0.8.0 `libseat/backend/logind.c`, systemd 255
   `src/login/logind-polkit.c`). Upstream OE doesn't hit this because its systemd is built without polkit.
   **Fix:** use **seatd** (`seatd.service`, socket group `video`) as the seat manager instead.
   labwc stays an unprivileged user process and seatd hands it the DRM and input devices. The unit
   now also brings tty7 to the foreground first, because seatd binds the compositor to the active VT.
2. **`DISPLAY=:0.0` in every login shell.** Toradex's `meta-toradex-demos` installs
   `/etc/profile.d/x11-display-var.sh`. With `DISPLAY` set, wlroots tries its X11 backend first, which isn't built.
   That's the error you saw when starting labwc from the console. **Fix:** a `base-files`
   bbappend removes that script, and `labwc-session` also unsets `DISPLAY`.

Additional hardening found while testing revision 2:
- `WLR_LIBINPUT_NO_DEVICES=1`: wlroots 0.17 otherwise refuses to start if no keyboard, mouse or
  touchscreen is connected at boot.
- Restart limits: if labwc keeps failing, the unit gives up and switches back to the tty1 console.
- Menu and panel power off/reboot use `sudo -n systemctl ...` with a NOPASSWD rule, since logind would need polkit for these too.

**Verified by a full-system boot of the exact image in QEMU** (`build-qemu-test/`, see section 8).
`graphical.target` is reached, `seatd` and `labwc` are active, labwc runs as `dev` in a logind
session on `seat0`/`tty7` (the active VT), and swaybg, waybar and the user D-Bus run as `dev`.
The desktop renders on the virtio-gpu DRM/KMS output without any login. **Confirmed on hardware:**
the revision-2 tarball boots into labwc on the Verdin iMX8M Plus + Mallow.

![labwc desktop after boot, full-system QEMU test of the revision-2 image](docs/screenshot-qemu-boot.png)

**Status (2026-10-14):** built from source. Revision 1 booted to a text console on the board (see
above). **Revision 2 boots into the labwc desktop on the board** (confirmed on a Verdin iMX8M Plus +
Mallow) and was also boot-tested end to end in a full-system QEMU VM. It is published as pre-release
v0.0.1.
The fallback to the stock Weston image was not needed. The earlier userspace smoke test
(chroot, headless) is shown below. Section 8 describes both tests.

![labwc session (headless smoke test on the build host)](docs/screenshot-headless.png)

*The screenshot above comes from the build host. The image's own rootfs was run under aarch64 emulation with the real `labwc-session` launcher and the shipped configs. Output used wlroots' headless backend (1280x720) with the pixman renderer. Visible: swaybg wallpaper, waybar (launcher button, taskbar, clock, CPU and memory, terminal and power buttons), fuzzel, and foot running htop. The network and temperature modules are hidden in that environment.*

---

## 1. What was used

| Item | Value |
|---|---|
| Toradex manifest | `https://git.toradex.com/toradex-manifest.git`, **tag `7.7.0`** (latest stable quarterly release on the `scarthgap-7.x.y` branch, 2026-07-15), manifest file **`tdxref/default.xml`** |
| Yocto release | Scarthgap (5.0), BitBake 2.8 |
| Distro / machine | `tdx-xwayland` / `verdin-imx8mp` (NXP downstream kernel `linux-toradex` 6.6, U-Boot `u-boot-toradex` 2024.04) |
| Extra third-party layer | `meta-wayland` from `https://codeberg.org/flk/meta-wayland`, branch `scarthgap`, pinned to `25850e97205eaa1b54882197ceaa6814c0403b4a` (added through `.repo/local_manifests/labwc-demo.xml`) |
| Our layer | `layers/meta-labwc-demo` (all customizations; a local git repo) |
| Image recipe | `labwc-demo-image` |

`scarthgap` meta-openembedded has no wlroots, labwc, waybar, foot, fuzzel or swaybg. The OpenEmbedded layer index lists them in `meta-wayland`, and its `scarthgap` branch has all of them.

No Toradex or upstream layer was modified. All changes live in `layers/meta-labwc-demo`, `build/conf/local.conf` and `build/conf/bblayers.conf`. The untouched Toradex sample configs are kept next to them as `*.toradex-orig`.

## 2. Reproducing the build

Host: Ubuntu 22.04, with the usual Yocto host packages installed (see the
Yocto Scarthgap "Required Packages for the Build Host"), plus [`repo`](https://gerrit.googlesource.com/git-repo).
About 100 GB free disk, and as much CPU and RAM as possible.

```sh
git clone https://github.com/Nat-As/verdin-imx8mp-labwc.git
cd verdin-imx8mp-labwc

# 1. Fetch the Toradex BSP (tag 7.7.0, tdxref/default.xml) plus the pinned
#    meta-wayland layer (manifests/labwc-demo.xml) into this directory
./setup.sh

# 2. Build (hours on the first run). Logs go to build-logs/, latest.log points to the newest.
./build.sh            # or: nohup ./build.sh &
```

This repository is laid out as a Toradex `repo` workspace. Only our own files are tracked:
- `layers/meta-labwc-demo` (our layer)
- `build/conf/{local,bblayers}.conf`
- `manifests/labwc-demo.xml` (copied into `.repo/local_manifests/` by `setup.sh`)
- the scripts and docs

`setup.sh` fills in `layers/` with the Toradex and upstream layers at the pinned revisions. `build.sh`
sources Toradex's `export` script (which keeps our tracked `build/conf`) and runs
`bitbake labwc-demo-image`.

**Continuous integration** (`.github/workflows/build.yml`, the *build* badge) runs on every push:
- **lint:** shellcheck, the labwc and manifest XML, and the waybar JSON.
- **yocto:** syncs the full BSP with `setup.sh`, runs `bitbake -p` (every recipe parses) and
  `bitbake -g labwc-demo-image` (the whole build and runtime dependency graph resolves), then checks
  that labwc, wlroots, waybar, foot, fuzzel, swaybg, seatd and the Mallow U-Boot env are in the build and Weston isn't.

A full compile needs about 1.5 h on 20 threads and about 50 GB of disk, which doesn't fit GitHub's hosted
runners (6 h job limit, about 14 GB disk). For complete builds in CI, register a self-hosted runner with the labels
`self-hosted, linux, yocto` and run `.github/workflows/full-build.yml` from the Actions tab. It
uploads the Tezi tarball as an artifact.

### local.conf changes (appended to Toradex's sample)

```
MACHINE ?= "verdin-imx8mp"            # placed before the machine include, as the sample requires
ACCEPT_FSL_EULA = "1"
BB_NUMBER_THREADS = "12"              # 20 HW threads / 32 GB RAM / 1 GB swap:
PARALLEL_MAKE = "-j 14"               # bounded to avoid OOM on big C++ units
DL_DIR = "${TOPDIR}/../downloads"     # shared caches inside the project
SSTATE_DIR = "${TOPDIR}/../sstate-cache"
```

Toradex's defaults stay in place: `EXTRA_IMAGE_FEATURES = "debug-tweaks package-management"`, `rm_work`, buildhistory, and the `tdx-xwayland` distro.

### bblayers.conf changes

The Toradex default list, plus:

```
${TOPDIR}/../layers/meta-wayland
${TOPDIR}/../layers/meta-labwc-demo
```

### Build time

Measured on the i7-13700H / 32 GB host, with `BB_NUMBER_THREADS=12` and `PARALLEL_MAKE=-j14`:

| Run | Wall time | Notes |
|---|---|---|
| First full build, empty `downloads/` and `sstate-cache/` | **1 h 36 min** (5782 s), 10,344 tasks | Includes all source downloads. It hit one recipe failure (wlroots `do_install`, fixed in the layer). |
| Fix-up rebuilds after layer changes | 1 to 3 min each | Only the changed recipes and the image are rebuilt. |

Disk usage after the build: `downloads/` 29 GB, `sstate-cache/` 5.2 GB, `build/tmp/` 16 GB
(`rm_work` is enabled). Build logs are in `build-logs/`.

## 3. Artifacts

All paths are relative to the project root:

| Artifact | Path |
|---|---|
| **Tezi tarball** (what you load in Tezi) | `build/deploy/images/verdin-imx8mp/Verdin-iMX8MP_labwc-Demo-Image-Tezi_7.7.0-devel-20261014222427+build.0.tar` (148 MB, sha256 `980b43770ffedb3bbc96fab23e21089ccd73a6583a9620bcb61ad354835cc2f3`), **revision 2** |
| Stable symlink to the newest Tezi tarball | `build/deploy/images/verdin-imx8mp/Verdin-iMX8MP_labwc-Demo-Image-Tezi.tar` |
| Root filesystem | `build/deploy/images/verdin-imx8mp/Verdin-iMX8MP_labwc-Demo-Image.tar.xz` |
| Boot partition (kernel, DTBs, overlays, boot.scr) | `build/deploy/images/verdin-imx8mp/Verdin-iMX8MP_labwc-Demo-Image.bootfs.tar.xz` |
| Tezi U-Boot env with `fdt_board=mallow` | `build/deploy/images/verdin-imx8mp/u-boot-initial-env-sd-mallow` |
| Tezi metadata | `build/deploy/images/verdin-imx8mp/image-labwc-Demo-Image.json` (also in the tarball as `image.json`) |
| Package manifest | `build/deploy/images/verdin-imx8mp/Verdin-iMX8MP_labwc-Demo-Image.manifest` |
| WIC SD-card image (not needed for Tezi) | `build/deploy/images/verdin-imx8mp/Verdin-iMX8MP_labwc-Demo-Image.wic.gz` |

The published release asset `Verdin-iMX8MP_labwc-Demo-Image-Tezi_v0.0.1.tar` is this file, renamed
because GitHub doesn't keep `+` in asset names.

The tarball contains `image.json`, `imx-boot`, the rootfs and bootfs `.tar.xz`,
`u-boot-initial-env-sd-mallow`, the NXP EULA, and Tezi marketing/icon files. The
version string includes the build timestamp, so the file name changes on every rebuild. The `...-Tezi.tar`
symlink always points to the newest one.

## 4. Installing with Toradex Easy Installer

The board is already in Tezi recovery mode, so Tezi runs on it. Choose one of these methods:

**A. USB stick / SD card (simplest)**

1. Extract the Tezi tarball onto a FAT32 or ext4 formatted USB stick or SD card:
   `tar -xf <Tezi tarball> -C /media/<stick>/`
   This creates a folder `Verdin-iMX8MP_labwc-Demo-Image-Tezi_<version>/` containing `image.json`.
2. Plug the stick into the Mallow board. Tezi scans removable media and lists
   **"Toradex Embedded Linux labwc Demo Image"**.
3. Select it, click **Install**, and accept the NXP EULA. When it finishes, click **Reboot**.

**B. Over the network (HTTP feed)**

1. On the build host:
   `cd <extracted Tezi folder>/.. && python3 -m http.server 8000`
   (the folder must contain `image.json`).
2. In Tezi, open **Feeds**, add `http://<host-ip>:8000/<folder>/image.json`
   (or serve an `image_list.json`), then install as above.

Tezi writes the bootloader (eMMC boot0), the BOOT (FAT) and RFS (ext4) partitions, and the U-Boot environment described below.

## 5. Mallow device tree selection

On BSP 7 for Verdin iMX8M Plus, U-Boot picks the device tree in `preboot`:

```
preboot=test -n ${fdtfile} || setenv fdtfile imx8mp-verdin-${variant}-${fdt_board}.dtb
```

`variant` (`wifi`/`nonwifi`) is set from the module's config block, and `fdt_board` defaults to `dev`
(Verdin Development Board). This was checked in `u-boot-toradex`
`fb30fdd0` (`configs/verdin-imx8mp_defconfig`, `include/configs/verdin-imx8mp.h`)
and matches Toradex's U-Boot documentation. The BSP builds
`imx8mp-verdin-{wifi,nonwifi}-mallow.dtb`.

**The selection is baked into the Tezi image.** The recipe
`u-boot-env-mallow` takes the default environment that Tezi writes to the
module (`u-boot-initial-env-sd`), replaces `fdt_board=dev` with
`fdt_board=mallow`, and drops any saved `fdtfile`. The image points Tezi at
this file through `UBOOT_ENV_TEZI_EMMC = "u-boot-initial-env-sd-mallow"`. After installation, U-Boot boots
`imx8mp-verdin-wifi-mallow.dtb` or `imx8mp-verdin-nonwifi-mallow.dtb`, whichever matches the SoM.
U-Boot itself is unmodified.

To check or change it manually (U-Boot console on the debug UART, 115200 8N1, interrupt autoboot):

```
=> printenv fdt_board fdtfile
=> setenv fdtfile            # make sure no fixed fdtfile is saved
=> setenv fdt_board mallow
=> saveenv
=> reset
```

From Linux: `fw_printenv fdt_board` and `sudo fw_setenv fdt_board mallow`.
Check the result with `cat /proc/device-tree/model`, which should print `Toradex Verdin iMX8M Plus [WB ]on Mallow Board`.

The default overlays from the Toradex BSP stay enabled in `overlays.txt` on the BOOT partition:
`verdin-imx8mp_hdmi_overlay.dtbo`, `verdin-imx8mp_dsi-to-hdmi_overlay.dtbo` and
`verdin-imx8mp_spidev_overlay.dtbo`. Edit `fdt_overlays=` in `overlays.txt` to
use a different display, for example a DSI or LVDS panel overlay from `/boot/overlays/`.

## 6. Logging in

| Login | Password | Notes |
|---|---|---|
| `dev` | `dev` | Desktop user (labwc runs as this user). Has `sudo`. Groups: video, input, render, audio, wayland. |
| `root` | *(empty)* | Toradex reference default via `debug-tweaks`. Works on the serial console and over SSH. |

SSH is provided by dropbear on port 22, the Toradex default. The board gets its address
by DHCP through connman on the Ethernet port. Use `ssh dev@<board-ip>` or `ssh root@<board-ip>`.
The hostname is `verdin-imx8mp-<serial>`, and avahi/mDNS is enabled, so
`ssh dev@verdin-imx8mp-<serial>.local` also works.

**This is a development image.** Change both passwords, or remove `debug-tweaks`, before using it anywhere other than a lab network.

GUI programs can be started over SSH as `dev`: `/etc/profile.d/labwc-wayland.sh`
exports `WAYLAND_DISPLAY=wayland-0` when the session is running. For example: `ssh dev@board`, then `foot &`.

## 7. How the session works

| Piece | File on target | Purpose |
|---|---|---|
| `labwc.service` | `/usr/lib/systemd/system/labwc.service` | System unit, enabled for `graphical.target`, which is the image's default target. Runs as `User=dev`, `PAMName=labwc-autologin` (pam_systemd: logind session, `XDG_RUNTIME_DIR`, user D-Bus), `TTYPath=/dev/tty7`. `Requires=seatd.service`, `LIBSEAT_BACKEND=seatd`, `WLR_LIBINPUT_NO_DEVICES=1`. `+chvt 7` runs before start and `chvt 1` after stop. `Restart=always` with a start limit of 5 per 120 s. Like Toradex's Weston unit, it waits for `/dev/dri/card0` (and `card1` if a GPU node exists). |
| `seatd.service` | `/usr/lib/systemd/system/seatd.service` | seatd's upstream unit (`seatd -g video`), installed by a seatd bbappend. Root daemon that gives `video` group members DRM and input devices. labwc never runs as root. |
| PAM | `/etc/pam.d/labwc-autologin` | Copy of `weston-autologin` with `desktop=labwc`. |
| Launcher | `/usr/bin/labwc-session` | Sets the Wayland session env and handles renderer fallback (see below). |
| Settings | `/etc/default/labwc` | `WLR_RENDERER`, `LABWC_ARGS` (default `-V`), `LANG`, `WLR_NO_HARDWARE_CURSORS`, `WLR_DRM_DEVICES`, ... |
| labwc config | `/etc/xdg/labwc/{rc.xml,menu.xml,autostart,environment}` | System defaults. Per-user overrides go in `~/.config/labwc/`. |
| waybar | `/etc/xdg/waybar/{config.jsonc,style.css}` | Launcher button, taskbar, clock, network/CPU/mem/temp, terminal and power buttons. |
| foot / fuzzel | `/etc/xdg/foot/foot.ini`, `/etc/xdg/fuzzel/fuzzel.ini` | Installed by the foot/fuzzel bbappends, replacing the upstream samples. |
| Wallpaper | `/usr/share/backgrounds/labwc-demo/wallpaper.png` | |
| udev | `/etc/udev/rules.d/90-labwc-demo-gpu.rules` | `galcore`/render node/dma_heap access for the video and render groups. |
| sudoers | `/etc/sudoers.d/dev` | `dev` gets full sudo (with password), plus NOPASSWD for `systemctl poweroff/reboot` (used by the menu and panel). |
| (removed) | `/etc/profile.d/x11-display-var.sh` | Toradex's `DISPLAY=:0.0` export, removed by our `base-files` bbappend. |

**Desktop usage:** the panel's **Apps** button or **Super+D** opens fuzzel. The panel's terminal
icon or **Super+Return** opens foot. Right-click on the desktop (or **Super+M**) opens the menu
(Terminal, Applications, htop, Reconfigure/Exit/Reboot/Power off). **Super+Q** or Alt+F4 closes a window, and Alt+Tab switches windows.

Restarting the session: `sudo systemctl restart labwc` (the menu's "Restart session" does the same). Stopping it and returning to the
console: `sudo systemctl stop labwc`. Logs: `journalctl -b -t labwc` (compositor and session wrapper), `journalctl -b -u labwc -u seatd` (unit start/stop, seat daemon).

**Starting labwc by hand** (for debugging) works from a console or SSH login as `dev` once the
service is stopped. libseat picks seatd automatically:
`sudo systemctl stop labwc`, then on the board's console: `labwc` (or `WLR_RENDERER=pixman labwc -d`).
Don't run it with `sudo`; it isn't needed.

## 8. Rendering: hardware vs. software

**What ships:** hardware rendering is tried first, with an automatic software fallback. In detail:

- wlroots 0.17.2 is built with **only the GLES2 renderer plus the gbm allocator, linked against NXP's
  Vivante stack** (`imx-gpu-viv`: `libEGL`, `libGLESv2`, and `libgbm` "vivante gbm library" 21.3.5).
  This is forced with `-Drenderers=gles2 -Dallocators=gbm`, so a broken GPU dependency fails the build
  instead of silently producing a pixman-only wlroots. The pixman renderer is always built in.
- At runtime, `/usr/bin/labwc-session` starts labwc with `WLR_RENDERER` unset, so wlroots' auto
  selection tries GLES2 on the DRM device first. If that fails, wlroots itself falls back to pixman when the display
  device has no render node. That is expected on the i.MX8MP, where the display controller is a KMS-only device separate from the GPU. On top of that,
  **if labwc exits with an error within 20 s, the wrapper restarts it with `WLR_RENDERER=pixman`.**
  So the board comes up with a usable desktop even if the Vivante path doesn't work.
- labwc runs with `-V`, so the journal records the choice:

  ```sh
  journalctl -b -t labwc | grep -E "labwc-session|Creating (GLES2|pixman) renderer|GL (vendor|renderer)|galcore|EGL"
  ```

**To force a renderer**, edit `/etc/default/labwc` and run `sudo systemctl restart labwc`:

```sh
WLR_RENDERER=pixman    # software, most robust
WLR_RENDERER=gles2     # hardware only; fails rather than falling back
```

**Not verified on hardware:** whether wlroots' GLES2 path actually initialises and renders correctly
on Vivante (`imx-gpu-viv` 6.4.11.p2.12). wlroots requires several EGL extensions, for example
`EGL_KHR_surfaceless_context`, `EGL_EXT_image_dma_buf_import` and `GL_OES_EGL_image_external`, and
the NXP blob support for them could only be checked on the board. The build-host test exercised the
pixman path only (headless backend). If the hardware path starts but renders garbage or a black
screen (no crash, so no automatic fallback), set `WLR_RENDERER=pixman` as shown above. The i.MX8MP
can drive 1080p with pixman, at higher CPU load.

**How it was tested on the build host.** The shipped rootfs was unpacked and `chroot`ed inside an unprivileged user+mount namespace, with the
aarch64 binaries running through qemu-user (binfmt). `labwc-session` was started with `WLR_BACKENDS=headless`,
and screenshots were taken with `grim`. This verified the following:
- the launcher logic, including its D-Bus fallback
- labwc config, menu and autostart
- swaybg, waybar, foot and fuzzel startup
- fonts and icons

That test does **not** cover DRM/KMS, the systemd/PAM path or seat handling, and that gap is why the first image's
seat problem was missed. Revision 2 therefore adds a **full-system boot test** (`build-qemu-test/`):
- A generic arm64 kernel is built from source (`linux-yocto` 6.6, `MACHINE=qemuarm64`, test-only
  build sharing the caches) and boots the **unmodified `.wic` of this image** in `qemu-system-aarch64`
  with virtio-gpu (DRM/KMS, `/dev/dri/card0`), a real VT console, systemd, PAM, logind and seatd.
- `build-qemu-test/qemu-boot-test.py` logs in on the serial console, collects
  `systemctl`/`loginctl`/`journalctl` diagnostics and grabs the screen through the QEMU monitor.
- Result: labwc starts at boot as `dev` on `seat0`/`tty7`, and the desktop is shown without any login.
  The auto (GLES2) attempt goes through Vivante's driver, which can't open `/dev/galcore` in the VM, so labwc exits
  and the wrapper falls back to pixman after about 5 s. This exercises the fallback path. On the board
  `/dev/galcore` exists, so the hardware path gets a real try there.

To rerun it: build the kernel with `build-qemu-test/run-kernel-build.sh`, then
`python3 build-qemu-test/qemu-boot-test.py build-qemu-test/tmp/deploy/images/qemuarm64/Image <disk.wic or qcow2 overlay> <outdir>`.

## 9. Known issues and caveats

1. **Renderer on hardware not yet recorded.** Revision 2 boots into labwc on the board. Which
   renderer it ended up with (Vivante GLES2 or the pixman fallback) can be read with
   `journalctl -b -t labwc | grep -E "labwc-session|Creating (GLES2|pixman) renderer"`. If you report it,
   it can be recorded here and the fallback logic tuned.
2. **`tdx-reference-multiuser-image` does not exist.** It isn't in BSP 7.7.0 or on any
   `meta-toradex-demos` branch (checked all branches). The Weston reference image in this BSP is
   `tdx-reference-multimedia-image`, so `labwc-demo-image` `require`s that recipe and then:
   - removes the `weston` image feature, the Weston/Xwayland bits (`weston-xwayland`, `xterm`) and the
     Weston Qt launcher (`APP_LAUNCH_WAYLAND`);
   - drops `packagegroup-tdx-qt5`, because the Qt5 demos only make sense with their Weston launchers, and to keep the image minimal;
   - drops `gpicview`, a GTK2/X11 app that would need Xwayland.

   Everything else (Toradex CLI tools, gstreamer/VPU, GPU tools/glmark2, ISP, Wi-Fi firmware,
   connman, ...) is unchanged.
3. **Non-root session vs. Toradex's root Weston.** Toradex runs Weston as root ("could not access input
   devices and GPU acceleration hardware"). Here labwc runs as `dev`. seatd (root) provides the
   DRM and input devices. GPU access comes from group membership (`/dev/galcore` is `video:0660`;
   render nodes are `render`). logind is not used for seat access, because on this distro it needs a polkit
   daemon (see "Revision 2" above). Adding polkit (plus a rule for `org.freedesktop.login1.chvt`) would
   be an alternative, but it pulls in a JavaScript engine for no other benefit.
4. **Display/overlays.** The Mallow DTB enables the native Verdin HDMI_1 path, and the default
   `overlays.txt` (Toradex default: `hdmi`, `dsi-to-hdmi`, `spidev`) is left unchanged. For a DSI or
   LVDS panel on Mallow, edit `fdt_overlays` in `overlays.txt` on the BOOT partition (`/boot/overlays.txt` from Linux).
   If several outputs are connected, labwc extends the desktop across them. Use `wlr-randr`
   (included) to inspect or change them.
5. **The Mallow selection relies on Tezi writing the U-Boot environment** (`u_boot_env` in
   `image.json`, Tezi config_format 4, the same mechanism Toradex's own images use). If the board was previously booted with
   a saved `fdtfile` and that variable survives, U-Boot keeps using it. Run the commands in section 5 (`setenv fdtfile; setenv fdt_board mallow; saveenv`) to fix it.
6. **meta-wayland** is a third-party layer that tracks fast-moving upstreams. It is pinned to a known
   commit. Its `scarthgap` branch provides labwc 0.7.1 / wlroots 0.17.2, so newer labwc features
   (0.8+) are not available.
7. The waybar **network and temperature modules** depend on netlink and `thermal_zone0`. Both exist on the
   board but couldn't be checked in the emulated test.
8. **Input devices:** labwc starts without any input device (`WLR_LIBINPUT_NO_DEVICES=1`). Plug in
   a USB keyboard and mouse (or use the touchscreen) at any time. If none is connected, use SSH to manage the board.
9. **Locale:** only `en_US.UTF-8` is installed (Toradex `IMAGE_LINGUAS = "en-us"`). The session
   and login shells default `LANG` to it, because foot refuses to start without a UTF-8 locale.
10. **Development image:** `debug-tweaks` (root with an empty password, including over SSH) and
   `dev`/`dev` with sudo. Do not deploy as is.
11. Building pulls in Rust (`librsvg`, via the GTK/gstreamer stack of the Toradex multimedia image),
    which is a large part of the first build time. fuzzel itself is built without librsvg.
12. The build host lacks passwordless sudo, so the userspace test used an unprivileged
    user namespace instead of a real chroot. That has no effect on the image.

## 10. Project layout

```
verdin-imx8mp-labwc/              (also the repo/Yocto workspace)
├── README.md, LICENSE            this file, MIT license for this repository
├── setup.sh                      repo init/sync: Toradex BSP 7.7.0 + manifests/labwc-demo.xml
├── build.sh                      logged build wrapper (nohup-friendly)
├── manifests/labwc-demo.xml      local manifest: pinned meta-wayland
├── .github/workflows/            build.yml (CI badge), full-build.yml (self-hosted full build)
├── docs/screenshot-*.png         emulation screenshots (headless chroot test, full QEMU boot)
├── build/conf/{local,bblayers}.conf       (+ *.toradex-orig, Toradex's untouched samples)
├── build-qemu-test/              TEST ONLY: qemuarm64 kernel build conf + qemu boot test script
├── layers/
│   ├── meta-labwc-demo/          OUR LAYER
│   │   ├── conf/layer.conf
│   │   ├── recipes-images/images/labwc-demo-image.bb
│   │   ├── recipes-graphics/labwc-session/   unit, PAM, launcher, configs, wallpaper, dev user
│   │   ├── recipes-bsp/u-boot-env-mallow/    Tezi U-Boot env with fdt_board=mallow
│   │   ├── recipes-core/seatd/               bbappend: seatd.service (seat manager, group video)
│   │   ├── recipes-core/base-files/          bbappend: drop Toradex's DISPLAY=:0.0 profile script
│   │   ├── recipes-wlroots/{wlroots,labwc}/  bbappends (GLES2 on Vivante, no Xwayland)
│   │   ├── recipes-gtk/waybar/               bbappend (trimmed modules, our config)
│   │   └── recipes-support/{foot,fuzzel}/    bbappends (our default config; fuzzel without librsvg)
│   └── meta-wayland/, meta-toradex-*, meta-freescale*, openembedded-core, ...
│                                 (not tracked: fetched by setup.sh, unmodified)
└── downloads/ sstate-cache/ build-logs/ .repo/   (not tracked: caches, logs, repo metadata)
```

## 11. License

The contents of this repository (the `meta-labwc-demo` layer, configuration, scripts, wallpaper and
docs) are released under the [MIT License](LICENSE).

The **binary image** published as a release asset is a separate matter. It is built from many third-party
components, each under its own license: the Linux kernel, U-Boot, BusyBox and others under GPL/LGPL;
labwc and wlroots under GPL-2.0/MIT; and **NXP proprietary GPU/VPU firmware and libraries** under
the NXP Software License Agreement (`LA_OPT_NXP_SW`), which Toradex Easy Installer shows for acceptance
before installing. The license texts of every package in the image are installed under
`/usr/share/common-licenses/`, and the package list is in the release notes and the image `.manifest`.
All sources are fetched from their public upstreams at the exact revisions pinned by the Toradex
7.7.0 manifest, `manifests/labwc-demo.xml` and the recipes in this repository, and `./setup.sh && ./build.sh`
reproduces the image.
