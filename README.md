# labwc desktop for Toradex Verdin iMX8M Plus on Mallow

[![build](https://img.shields.io/github/actions/workflow/status/Nat-As/verdin-imx8mp-labwc/build.yml?branch=main&label=build)](https://github.com/Nat-As/verdin-imx8mp-labwc/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/Nat-As/verdin-imx8mp-labwc?label=release)](https://github.com/Nat-As/verdin-imx8mp-labwc/releases)
[![downloads](https://img.shields.io/github/downloads/Nat-As/verdin-imx8mp-labwc/total?label=downloads&cacheSeconds=3600)](https://github.com/Nat-As/verdin-imx8mp-labwc/releases)
[![license](https://img.shields.io/github/license/Nat-As/verdin-imx8mp-labwc?label=license)](LICENSE)

A Yocto image, built from source on the Toradex BSP 7.7.0, that boots a **Verdin iMX8M Plus** on
the **Mallow** carrier board straight into a minimal **labwc** Wayland desktop. labwc runs directly
on DRM/KMS, with no X11 or Xwayland, and renders on the GPU (OpenGL ES). The desktop
shows on the **Toradex Capacitive Touch Display 7" DSI** (1024x600, touch enabled) and on HDMI.

The image includes the complete GPU, NPU and VPU stack of the i.MX 8M Plus (OpenGL ES, OpenCL,
OpenVX, G2D, TensorFlow Lite with the NPU delegate, hardware video codecs), so it can be used as
the base for GUI- and machine-learning-heavy applications, including C# applications on .NET with
OpenCV and Java applications.

<p>
  <a href="https://wayland.freedesktop.org"><img src="docs/logos/wayland.png" height="34" alt="Wayland" title="Wayland"></a>
  <a href="https://www.khronos.org/opengles/"><img src="docs/logos/opengles.png" height="34" alt="OpenGL ES" title="OpenGL ES"></a>
  <a href="https://www.khronos.org/opencl/"><img src="docs/logos/opencl.png" height="34" alt="OpenCL" title="OpenCL"></a>
  <a href="https://www.khronos.org/openvx/"><img src="docs/logos/openvx.png" height="34" alt="OpenVX" title="OpenVX"></a>
  <a href="https://opencv.org"><img src="docs/logos/opencv.png" height="34" alt="OpenCV" title="OpenCV"></a>
  <a href="https://www.tensorflow.org/lite"><img src="docs/logos/tensorflow.png" height="34" alt="TensorFlow Lite" title="TensorFlow Lite"></a>
  <a href="https://dotnet.microsoft.com"><img src="docs/logos/dotnet.png" height="34" alt=".NET" title=".NET"></a>
  <a href="https://avaloniaui.net"><img src="docs/logos/avalonia.png" height="34" alt="Avalonia" title="Avalonia"></a>
  <a href="https://www.qt.io"><img src="docs/logos/qt.png" height="34" alt="Qt" title="Qt"></a>
</p>

| | Included |
|---|---|
| Boot | Boot animation (Alakai logo, spinner and image version) from the moment the display comes up until the desktop is ready; no kernel or boot messages on the screen |
| Desktop | labwc 0.7.1 (wlroots 0.17.2), GPU rendered (OpenGL ES), waybar panel, fuzzel launcher, foot terminal, swaybg wallpaper |
| GPU | Vivante GC7000UL: OpenGL ES, EGL/GBM, OpenCL, G2D (2D blitter), `clinfo`, `glmark2` (**GPU Benchmark** in the launcher), `gmem_info` |
| NPU | 2.3 TOPS NPU: OpenVX with NN extensions, RapidOpenVX, TIM-VX, TensorFlow Lite 2.16 (C++ and Python `tflite_runtime`) with the VX delegate |
| Video | Hantro VPU (H.264/H.265/VP9 decode, H.264/H.265 encode) with GStreamer plugins |
| Shell | fish (default for the terminal, SSH and the console), bash |
| Networking | NetworkManager (`nmtui`, `nmcli`), Bluetooth (BlueZ, Blueman), SSH server (dropbear) |
| Tools | nano, neofetch, htop, tmux, tree, git, curl, wget, rsync, nmap, gzip, man (with man pages) |
| Development | gcc/g++, make, cmake, meson, ninja, gdb, and headers for every installed library (incl. Wayland) |
| C# / .NET | .NET 10 LTS (runtime and SDK), OpenCvSharp for OpenCV with OpenCL, offline NuGet feed; **GPU Test** demo app (C#, Avalonia 12.1.4 on Wayland, OpenGL ES) |
| Java 25 LTS | Java Runtime Environment: Eclipse Temurin JRE 25.0.5 (OpenJDK HotSpot), `java`, `keytool`, `jfr`, `jwebserver`; `JAVA_HOME` set |
| Python 3.12 | numpy, scipy, pandas, matplotlib, OpenCV (with OpenCL), TensorFlow Lite, Pillow, PyQt6, tkinter, pip, the full standard library |
| Qt | Qt 6.11 (Qt Base, Wayland, Declarative/QML, SVG) |
| Drivers | All drivers of the Toradex kernel (Mallow peripherals, 7" DSI display, Wi-Fi/Bluetooth), Realtek r8152 USB Ethernet |

## Download and install

**Release v1.0.1:**
**[⬇ Verdin-iMX8MP_labwc-Demo-Image-Tezi_v1.0.1.tar](https://github.com/Nat-As/verdin-imx8mp-labwc/releases/download/v1.0.1/Verdin-iMX8MP_labwc-Demo-Image-Tezi_v1.0.1.tar)** (642 MB)

SHA-256: `392ad0ad905c7f4eb95bc165700e89ffb3db6d01b72463f35c6f31a3320f2ece`
([checksum file](https://github.com/Nat-As/verdin-imx8mp-labwc/releases/download/v1.0.1/Verdin-iMX8MP_labwc-Demo-Image-Tezi_v1.0.1.tar.sha256),
[release notes](https://github.com/Nat-As/verdin-imx8mp-labwc/releases/tag/v1.0.1),
[all releases](https://github.com/Nat-As/verdin-imx8mp-labwc/releases))

Install it with [Toradex Easy Installer](https://developer.toradex.com/easy-installer/toradex-easy-installer/) (Tezi):

1. Start the module in Tezi ([recovery mode](https://developer.toradex.com/hardware/hardware-resources/recovery-mode/imx-recovery-mode/)).
2. Extract the tarball onto a FAT32 or ext4 formatted USB stick or SD card:
   ```sh
   tar -xf Verdin-iMX8MP_labwc-Demo-Image-Tezi_v1.0.1.tar -C /media/<stick>/
   ```
3. Plug the stick into the Mallow board. Tezi lists **"Toradex Embedded Linux labwc Demo Image"**.
4. Select it, click **Install**, accept the NXP EULA, and click **Reboot** when it finishes.

The board boots into the labwc desktop on the 7" DSI display and the HDMI output. The Mallow device
tree and the display overlays are selected automatically.

During boot the displays stay dark while the bootloader and the kernel start (about 3 seconds),
then show the Alakai logo with a spinner until the desktop takes over. Boot messages go to the
serial console (Mallow USB debug port) only.

### Installing over the USB cable (no USB stick)

In recovery mode, Tezi also runs a network over the USB OTG cable (USB NCM/RNDIS): the module is
`192.168.11.1` and the PC gets `192.168.11.2`. `serve-tezi.sh` (in this repository) serves an
image to Tezi over that link and announces it with Zeroconf, so Tezi lists it by itself:

```sh
./serve-tezi.sh v1.0.1                      # release/v1.0.1/...Tezi_v1.0.1.tar
./serve-tezi.sh build                       # the current build
./serve-tezi.sh ~/Downloads/Verdin-iMX8MP_labwc-Demo-Image-Tezi_v1.0.1.tar
```

Then install it in Tezi as above; Ctrl+C stops the server when Tezi has finished. The script
extracts the image to `release/serve/` and serves it on port 8080 with Python's web server; the
Zeroconf announcement needs `avahi-publish` (`avahi-utils`). Without it, add the feed in Tezi
(**Feeds**) as `http://192.168.11.2:8080/image_list.json`. `TEZI_HOST_IP` and `TEZI_PORT` change
the address and port, for example to serve over Ethernet.

## Build

Requirements: an Ubuntu 22.04 host with the
[Yocto host packages](https://docs.yoctoproject.org/5.0/ref-manual/system-requirements.html#ubuntu-and-debian)
and [`repo`](https://gerrit.googlesource.com/git-repo) installed, and about 150 GB of free disk. The first build
takes several hours (it compiles the toolchain, Qt 6, OpenCV, SciPy and TensorFlow Lite from source).

```sh
git clone https://github.com/Nat-As/verdin-imx8mp-labwc.git
cd verdin-imx8mp-labwc
./setup.sh      # fetch the Toradex BSP 7.7.0, meta-wayland, meta-qt6 and NXP meta-imx-ml
./build.sh      # build labwc-demo-image (log: build-logs/latest.log)
```

The Tezi tarball is written to:

```
build/deploy/images/verdin-imx8mp/Verdin-iMX8MP_labwc-Demo-Image-Tezi.tar
```

### Customize the software

The software in the image is listed in two config files. Edit them and run `./build.sh` again:

| File | Contents |
|---|---|
| [`config/image-packages.conf`](config/image-packages.conf) | Applications, tools, networking, development tools, Qt, drivers, and packages to leave out |
| [`config/python-packages.conf`](config/python-packages.conf) | Python libraries |
| [`config/release.conf`](config/release.conf) | Image version, shown on the boot animation and in `/etc/argos-release` |

Entries are Yocto package names, one per line. To find a package, search the
[OpenEmbedded layer index](https://layers.openembedded.org/layerindex/branch/scarthgap/recipes/)
(branch *scarthgap*). Packages are installed and configured when the image is built; there is no
separate setup step on the board. Python libraries that aren't packaged can be installed on the
board with `pip3 install --user <name>`.

Other customizations are in the `layers/meta-labwc-demo` layer (desktop configuration, wallpaper,
users) and in `build/conf/local.conf` (display overlays, build settings). Rebuilds after a change
take a few minutes to an hour, depending on what changed.

## Screenshots

![labwc desktop after boot](docs/screenshot-qemu-boot.png)

*The desktop after boot: wallpaper and waybar panel.*

![launcher and terminal](docs/screenshot-headless.png)

*The fuzzel launcher and a foot terminal running htop.*

## Log in

| User | Password | Notes |
|---|---|---|
| `argos_user` | `argos_user` | Desktop user: the desktop runs as this user. Can power off and reboot. |
| `alakai_admin` | `alakai_admin` | Administrator: use `sudo` for system tasks. |

Direct `root` login is disabled. Run administrative commands as `alakai_admin` with `sudo`, or get a
root shell with `sudo -i`. The login shell of the accounts is **fish**; run `bash` for a POSIX shell,
or change it with `chsh -s /bin/bash`.

SSH is enabled. The board gets an address over DHCP on Ethernet (or Wi-Fi, see below):

```sh
ssh argos_user@<board-ip>
ssh alakai_admin@<board-ip>
```

The hostname is `argos-<serial-number>` (for example `argos-15629353`), and mDNS is enabled, so
`ssh argos_user@argos-<serial-number>.local` also works. The prefix is set with
`LABWC_DEMO_HOSTNAME_PREFIX` in `layers/meta-labwc-demo/conf/layer.conf`.

> **Change both passwords** (`passwd`) before connecting the board to anything other than a lab
> network.

## Use the desktop

| Action | How |
|---|---|
| Open the application launcher | **Apps** button on the panel, or **Super+D** |
| Open a terminal | Terminal icon on the panel, or **Super+Return** |
| Network settings (Ethernet, Wi-Fi) | Click the network status on the panel (opens `nmtui`) |
| Bluetooth devices | Click the Bluetooth icon on the panel (opens Blueman) |
| Desktop menu (terminal, apps, htop, network, Bluetooth, restart session, reboot, power off) | Right-click the desktop, or **Super+M** |
| Close a window | **Super+Q** or **Alt+F4** |
| Switch windows | **Alt+Tab** |
| Power off / reboot | Power icon on the panel (left click: power off, right click: reboot) |

The touchscreen is enabled (tap to select and click). A keyboard and mouse can be plugged in at any
time.

To start GUI programs over SSH, log in as `argos_user` while the desktop is running, then run them
(for example `foot &`).

### Python and Qt

```sh
python3 -c "import numpy, scipy, pandas, matplotlib, cv2; print(cv2.__version__)"
```

Plots from matplotlib and windows from PyQt6 or Qt 6 C++ programs open as Wayland windows on the
desktop. tkinter is installed, but Tk is an X11 toolkit, so tkinter windows need an X server
(Xwayland), which this image doesn't run.

### GPU and NPU

Check the hardware acceleration (GPU renderer, OpenGL ES applications, OpenCL, OpenVX, NPU, G2D, VPU, display,
touch, audio, RTC, Wi-Fi, Bluetooth, Ethernet), as `alakai_admin`:

```sh
sudo labwc-hwaccel-check        # add -q to skip the NPU benchmark
```

**Desktop renderer.** labwc tries these renderers in order and uses the first one that works;
a renderer that crashes, hangs or reports errors during startup is skipped until the next reboot:

| Strategy | Renderer |
|---|---|
| `gles2-render` | OpenGL ES 2 on the GPU render node; the default |
| `gles2` | OpenGL ES 2 on the display device (the way Weston uses the GPU) |
| `pixman` | Software rendering on the CPU (slow; last resort) |

The renderer in use is shown by `labwc-hwaccel-check` and in the desktop log
(`sudo journalctl -b -t labwc | grep -E "labwc-session|renderer"`). With the GPU
renderers, the desktop provides the Vivante driver's own Wayland protocol (`wl_viv`, through
`EGL_WL_bind_wayland_display`, as Weston does), which OpenGL ES applications need to show their
windows; `labwc-hwaccel-check` reports it as `egl-bind`. The desktop does not use Vulkan (the NXP Vulkan driver cannot draw into the display
buffers on this board); the Vulkan driver of the BSP is still installed for applications.

All GPU strategies wait for the GPU to finish each frame before it is shown. The Vivante driver
doesn't give the display the fence it would need to wait by itself; without the wait, areas that
are being redrawn (for example a running `htop`) can flash black. `LABWC_RENDER_FINISH=0` in
`/etc/default/labwc` turns the wait off.

`sudo labwc-gpu-probe` starts labwc once per renderer (off-screen, the desktop keeps running)
with the GPU Test app as a client and prints which ones start, with a gdb backtrace for any crash.
`sudo labwc-gpu-probe --kms` tests the renderers on the real displays: it shows a white terminal
and the GPU Test molecule with each renderer and asks whether you saw them (the desktop is stopped
for a few minutes).

**NPU.** TensorFlow Lite models run on the NPU through the VX delegate
(`/usr/lib/libvx_delegate.so`):

```sh
cd /usr/bin/tensorflow-lite-2.16.2/examples
./label_image -m mobilenet_v1_1.0_224_quant.tflite -i grace_hopper.bmp -l labels.txt \
    --external_delegate_path=/usr/lib/libvx_delegate.so
```

```python
import tflite_runtime.interpreter as tflite
npu = tflite.load_delegate("/usr/lib/libvx_delegate.so")
interpreter = tflite.Interpreter(model_path="model.tflite", experimental_delegates=[npu])
```

The first inference compiles the model for the NPU and takes a few seconds. To cache the compiled
graph between runs, set `VIV_VX_ENABLE_CACHE_GRAPH_BINARY=1` and
`VIV_VX_CACHE_BINARY_GRAPH_DIR=<writable directory>`. Quantized (int8/uint8) models run fastest
on the NPU. C/C++ programs can use OpenVX (`<VX/vx.h>`, `-lOpenVX`), RapidOpenVX or TIM-VX
directly; the headers are installed.

**OpenCL and OpenCV.** OpenCV 4.6 (NXP's i.MX build) uses the GPU through OpenCL for
`cv::UMat` operations (`cv2.UMat` in Python). Check it with
`python3 -c "import cv2; print(cv2.ocl.haveOpenCL(), cv2.ocl.Device.getDefault().name())"`.
C++ programs build with `pkg-config --cflags --libs opencv4`; the OpenCV samples are in
`/usr/share/opencv4/samples/`. Programs that need GPU access (`/dev/galcore`) must run as a member
of the `video` group (the desktop user is) or as root.

### C# and .NET

.NET 10 (LTS) is installed with its SDK, so C# programs can be built and run on the board:

```sh
dotnet new console -o hello && cd hello && dotnet run
```

**GPU Test** (in the application launcher, or `gpu-test`) is a C# demo built with
[Avalonia](https://avaloniaui.net) 12.1: a rotating cyclohexane molecule rendered with OpenGL ES on
the GPU, with the measured frame rate in the top right corner, in a window with a 40 % opaque
background. It uses Avalonia's native Wayland backend (`Avalonia.Wayland`, `UseWayland()`), which
renders the whole window with OpenGL ES through EGL, so no X server is needed and the UI stays
hardware accelerated. The window runs at up to 60 FPS (`GPU_TEST_MAX_FPS=<n>`, `0` for no limit);
`gpu-test --headless` measures the GPU without a window. **GPU Benchmark** in the launcher
(`gpu-benchmark`) runs glmark2 in a window and keeps its terminal open with the score.
Both log to the journal: `journalctl -t gpu-test`, `journalctl -t glmark2`.

**OpenCV from C#** uses [OpenCvSharp](https://github.com/shimat/opencvsharp) 4.6.0.20220608, the
release that matches the image's OpenCV 4.6. Its native part (`libOpenCvSharpExtern.so`) is built
against the image's OpenCV with OpenCL, so `UMat` operations run on the GPU. `opencv-cs-check`
prints the OpenCV build and times a blur on the CPU and with OpenCL. To use OpenCvSharp in your own
project, add the package from the offline feed in the image:

```sh
dotnet add package OpenCvSharp4 --version 4.6.0.20220608 --source /usr/share/nuget/opencvsharp-feed
```

Do not add the `OpenCvSharp4.runtime.*` packages: the native library comes with the image. The
source of `opencv-cs-check` is in `/usr/share/dotnet-examples/OpenCvCheck/` as an example.

### Java

The Java 25 (LTS) runtime is installed: the [Eclipse Temurin](https://adoptium.net) JRE (OpenJDK
HotSpot) in `/usr/lib/jvm/temurin-25-jre`, with `java`, `keytool`, `jfr`, `jwebserver`,
`jrunscript` and `rmiregistry` in the `PATH` and `JAVA_HOME` set for sh and fish. Run applications
with `java -jar app.jar` or `java -cp app.jar Main`. It is a runtime only, without the compiler
(`javac`, and `java Hello.java` source launching): build the JARs on the PC with any JDK up to 25.

Java programs run headless (console, network and server applications, image processing with
`java.awt.image`). AWT/Swing windows need an X server, and the desktop has no X11 or Xwayland.

### Configuration

| What | Where |
|---|---|
| Keyboard shortcuts, window behaviour, menu, autostart, touchscreen | `/etc/xdg/labwc/` (per user: `~/.config/labwc/`) |
| Panel | `/etc/xdg/waybar/` (per user: `~/.config/waybar/`) |
| Terminal and launcher | `/etc/xdg/foot/foot.ini`, `/etc/xdg/fuzzel/fuzzel.ini` |
| fish | `/etc/fish/` (per user: `~/.config/fish/`) |
| Session settings (renderer order, debug logging) | `/etc/default/labwc` |
| Display overlays | `/boot/overlays.txt` (reboot after a change) |
| Boot splash | Theme `/usr/share/plymouth/themes/argos/`, settings `/etc/plymouth/plymouthd.conf` |

After a desktop configuration change, select **Restart session** in the desktop menu, or run
`sudo systemctl restart labwc` as `alakai_admin`.

### Troubleshooting

System commands below need `sudo`, so run them as `alakai_admin`.

- **Show the desktop log:** `sudo journalctl -b -t labwc`
- **GPU Test or GPU Benchmark shows no window:** check `journalctl -b -t gpu-test` and
  `journalctl -b -t glmark2`, and check that the desktop runs the `gles2-render` renderer
  (`sudo labwc-hwaccel-check` runs both apps and reports `gl-client` and `glmark2`).
- **Black or garbled screen, or windows drawn wrong:** find a renderer that shows a picture with
  `sudo labwc-gpu-probe --kms`, then set it in `/etc/default/labwc`, for example
  `LABWC_RENDERERS="gles2-render pixman"` (or `WLR_RENDERER=pixman` for software rendering), and run
  `sudo systemctl restart labwc`. Please report the probe output.
- **The 7" display stays dark:** `labwc-hwaccel-check` reports `touch FAIL` when the display does
  not answer on I2C (kernel log: `sn65dsi83 ... failed to lock PLL` and
  `ilitek_ts ... protocol init failed`). Check the display's FFC cable and power. While the display
  is missing and HDMI is connected, the desktop turns the 7" output (`LVDS-1`) off so that windows
  don't open on an invisible screen.
- **Wrong date and time after power-up:** the kernel log shows `rtc-ds1307 ... oscillator failed`
  when the Mallow board's RTC backup battery is missing or empty. The clock is set from the network
  (NTP) once the board is online; insert a coin cell to keep the time without power.
- **Touch input lands in the wrong place with HDMI also connected:** touch is mapped to the 7"
  display (output `LVDS-1`). Check the output names with `wlr-randr` and adjust
  `<touch mapToOutput="..."/>` in `/etc/xdg/labwc/rc.xml`.
- **Using the Toradex DSI-to-HDMI adapter instead of the 7" display:** in `/boot/overlays.txt`, replace
  `verdin-imx8mp_panel-cap-touch-7inch-dsi_overlay.dtbo` with
  `verdin-imx8mp_dsi-to-hdmi_overlay.dtbo` and reboot.
- **Text console:** press **Ctrl+Alt+F2** for a login prompt (**Ctrl+Alt+F1** returns to the
  desktop), or stop the desktop with `sudo systemctl stop labwc`, which switches to it.
- **See the boot messages on the screen / boot without the splash:** on the board, run
  `sudo fw_setenv tdxargs "loglevel=7 plymouth.enable=0 console=tty1"` and reboot; remove it again
  with `sudo fw_setenv tdxargs`. The full kernel log is always available with `dmesg`, and on the
  serial console.

### Expected kernel messages

These boot messages are harmless on this hardware: `Fixed dependency cycle(s)`,
`supply ... not found, using dummy regulator`, `imx-bus-devfreq ... failed to fetch clk: -2`,
`fsl-aud2htx ... failed to pcm register` (the HDMI audio driver retries and succeeds),
`No soundcards found` (printed before the sound drivers load), `imx6q-pcie ... Phy link never came
up` (nothing in the PCIe slot), `tpm tpm0: A TPM error (256) ... starting up the TPM manually`,
`mwifiex_sdio ... unknown event id: 0x81` / `Unknown GET_HW_SPEC TLV type`,
`mwifiex_cfg80211_sched_scan_start : Invalid Sched_scan parameters` (background Wi-Fi scans fall
back to normal scans), `Hot alarm is canceled. GPU3D clock will return to 64/64`,
`memfd_create() called without MFD_EXEC` and a short USB disconnect/reconnect of the devices
behind the Mallow USB hub while its driver initializes it.

## License

This repository is released under the [MIT License](LICENSE).

The image also contains third-party software under its own licenses, including GPL/LGPL components and
NXP proprietary GPU/VPU firmware and libraries under the NXP Software License Agreement, which
Tezi shows before installing. License texts for every package are installed in
`/usr/share/common-licenses/` on the board.

The logos above are trademarks of their owners (OpenGL ES, OpenCL and OpenVX: The Khronos
Group; OpenCV: OpenCV team; TensorFlow: Google; .NET: Microsoft; Avalonia: AvaloniaUI OÜ; Qt: The
Qt Company; Wayland: its project) and only identify the software that the image uses.
