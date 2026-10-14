# labwc desktop for Toradex Verdin iMX8M Plus on Mallow

[![build](https://img.shields.io/github/actions/workflow/status/Nat-As/verdin-imx8mp-labwc/build.yml?branch=main&label=build)](https://github.com/Nat-As/verdin-imx8mp-labwc/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/Nat-As/verdin-imx8mp-labwc?include_prereleases&label=release)](https://github.com/Nat-As/verdin-imx8mp-labwc/releases)
[![downloads](https://img.shields.io/github/downloads/Nat-As/verdin-imx8mp-labwc/total?label=downloads&cacheSeconds=3600)](https://github.com/Nat-As/verdin-imx8mp-labwc/releases)
[![license](https://img.shields.io/github/license/Nat-As/verdin-imx8mp-labwc?label=license)](LICENSE)

A Yocto image, built from source on the Toradex BSP 7.7.0, that boots a **Verdin iMX8M Plus** on
the **Mallow** carrier board straight into a minimal **labwc** Wayland desktop. labwc runs directly
on DRM/KMS, with no X11 or Xwayland.

| Role | Component |
|---|---|
| Compositor | labwc 0.7.1 (wlroots 0.17.2) |
| Panel | waybar |
| Application launcher | fuzzel |
| Terminal | foot |
| Wallpaper | swaybg |
| Fonts | DejaVu Sans/Mono/Serif, Font Awesome 6 |

## Download and install

**Pre-release v0.0.1:**
**[⬇ Verdin-iMX8MP_labwc-Demo-Image-Tezi_v0.0.1.tar](https://github.com/Nat-As/verdin-imx8mp-labwc/releases/download/v0.0.1/Verdin-iMX8MP_labwc-Demo-Image-Tezi_v0.0.1.tar)** (148 MB)

SHA-256: `980b43770ffedb3bbc96fab23e21089ccd73a6583a9620bcb61ad354835cc2f3`
([checksum file](https://github.com/Nat-As/verdin-imx8mp-labwc/releases/download/v0.0.1/Verdin-iMX8MP_labwc-Demo-Image-Tezi_v0.0.1.tar.sha256),
[release notes](https://github.com/Nat-As/verdin-imx8mp-labwc/releases/tag/v0.0.1))

Install it with [Toradex Easy Installer](https://developer.toradex.com/easy-installer/toradex-easy-installer/) (Tezi):

1. Start the module in Tezi ([recovery mode](https://developer.toradex.com/hardware/hardware-resources/recovery-mode/imx-recovery-mode/)).
2. Extract the tarball onto a FAT32 or ext4 formatted USB stick or SD card:
   ```sh
   tar -xf Verdin-iMX8MP_labwc-Demo-Image-Tezi_v0.0.1.tar -C /media/<stick>/
   ```
3. Plug the stick into the Mallow board. Tezi lists **"Toradex Embedded Linux labwc Demo Image"**.
4. Select it, click **Install**, accept the NXP EULA, and click **Reboot** when it finishes.

The board boots into the labwc desktop on the HDMI output. The Mallow device tree is selected
automatically.

<details>
<summary>Installing over the network instead of a USB stick</summary>

1. Extract the tarball on a PC and serve the folder that contains it:
   `python3 -m http.server 8000`
2. In Tezi, open **Feeds** and add
   `http://<pc-ip>:8000/Verdin-iMX8MP_labwc-Demo-Image-Tezi_<version>/image.json`, then install as above.

</details>

## Build

Requirements: an Ubuntu 22.04 host with the
[Yocto host packages](https://docs.yoctoproject.org/5.0/ref-manual/system-requirements.html#ubuntu-and-debian)
and [`repo`](https://gerrit.googlesource.com/git-repo) installed, and about 100 GB of free disk. The first build
takes about 1.5 hours on a 20-thread machine.

```sh
git clone https://github.com/Nat-As/verdin-imx8mp-labwc.git
cd verdin-imx8mp-labwc
./setup.sh      # fetch the Toradex BSP 7.7.0 and the meta-wayland layer
./build.sh      # build labwc-demo-image (log: build-logs/latest.log)
```

The Tezi tarball is written to:

```
build/deploy/images/verdin-imx8mp/Verdin-iMX8MP_labwc-Demo-Image-Tezi.tar
```

To change the image, edit the layer in `layers/meta-labwc-demo` and the build configuration in
`build/conf/local.conf`, then run `./build.sh` again. Rebuilds take a few minutes.

## Screenshots

![labwc desktop after boot](docs/screenshot-qemu-boot.png)

*The desktop after boot: wallpaper and waybar panel.*

![launcher and terminal](docs/screenshot-headless.png)

*The fuzzel launcher and a foot terminal running htop.*

## Log in

| User | Password | Notes |
|---|---|---|
| `dev` | `dev` | The desktop runs as this user. Can use `sudo`. |
| `root` | *(empty)* | Serial console and SSH. |

SSH is enabled. The board gets an address over DHCP on Ethernet:

```sh
ssh dev@<board-ip>
```

The hostname is `verdin-imx8mp-<serial-number>`, and mDNS is enabled, so
`ssh dev@verdin-imx8mp-<serial-number>.local` also works.

> **This is a development image.** Change both passwords before connecting the board to anything
> other than a lab network.

## Use the desktop

| Action | How |
|---|---|
| Open the application launcher | **Apps** button on the panel, or **Super+D** |
| Open a terminal | Terminal icon on the panel, or **Super+Return** |
| Desktop menu (terminal, apps, htop, restart session, reboot, power off) | Right-click the desktop, or **Super+M** |
| Close a window | **Super+Q** or **Alt+F4** |
| Switch windows | **Alt+Tab** |
| Power off / reboot | Power icon on the panel (left click: power off, right click: reboot) |

A keyboard and mouse (or touchscreen) can be plugged in at any time.

To start GUI programs over SSH, log in as `dev` while the desktop is running, then run them
(for example `foot &`).

### Configuration

| What | Where |
|---|---|
| Keyboard shortcuts, window behaviour, menu, autostart | `/etc/xdg/labwc/` (per user: `~/.config/labwc/`) |
| Panel | `/etc/xdg/waybar/` (per user: `~/.config/waybar/`) |
| Terminal and launcher | `/etc/xdg/foot/foot.ini`, `/etc/xdg/fuzzel/fuzzel.ini` |
| Session settings (renderer, debug logging) | `/etc/default/labwc` |

After a configuration change, select **Restart session** in the desktop menu, or run
`sudo systemctl restart labwc`.

### Troubleshooting

- **Show the desktop log:** `journalctl -b -t labwc`
- **Black or garbled screen:** switch to software rendering. Add `WLR_RENDERER=pixman` to
  `/etc/default/labwc`, then run `sudo systemctl restart labwc`.
- **Return to the text console:** `sudo systemctl stop labwc`

## License

This repository is released under the [MIT License](LICENSE).

The image also contains third-party software under its own licenses, including GPL/LGPL components and
NXP proprietary GPU/VPU firmware and libraries under the NXP Software License Agreement, which
Tezi shows before installing. License texts for every package are installed in
`/usr/share/common-licenses/` on the board.
