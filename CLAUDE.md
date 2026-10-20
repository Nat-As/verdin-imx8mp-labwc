# CLAUDE.md

Yocto image for the **Toradex Verdin iMX8M Plus** on the **Mallow** carrier board that boots into
a **labwc** Wayland desktop. Repository: https://github.com/Nat-As/verdin-imx8mp-labwc

## Rules (always follow)

1. **Versioning**: every release gets a new version `va.b.c`:
   - `c`: minor tweaks/fixes (e.g. package additions, config changes)
   - `b`: larger changes
   - `a`: major changes / large compatibility differences
   - We are in **pre-release**: versions are `v0.x.y`. The first full release will be **v1.0.0**.
   - Create an annotated git tag with exactly that name (`git tag -a v0.0.3 -m "..."`) for each release.
2. **README.md** must be updated for each release: correct information, and the download link,
   size and SHA-256 of the new release asset.
3. **Hardware verification before push**: after a release is committed, the user must test the
   image **on real hardware**. Do **not** `git push`, push tags, or create the GitHub release until
   the user confirms the hardware test passed.

## Release workflow

1. Change the layer/config, run `./build.sh` (log: `build-logs/latest.log`), fix failures.
2. Verify locally: inspect the rootfs (`build/tmp/work/verdin_imx8mp-tdx-linux/labwc-demo-image/1.0/rootfs`)
   and run the QEMU boot test (see "Testing").
3. Stage the release assets in `release/vX.Y.Z/` (git-ignored):
   - `Verdin-iMX8MP_labwc-Demo-Image-Tezi_vX.Y.Z.tar` (copy of
     `build/deploy/images/verdin-imx8mp/Verdin-iMX8MP_labwc-Demo-Image-Tezi.tar`; GitHub drops `+`
     from asset names, hence the rename)
   - `...tar.sha256`, `labwc-Demo-Image-vX.Y.Z.manifest.txt` (image `.manifest`), `release-notes.md`
4. Update README.md (version, link, size, SHA-256), commit locally, hand the tarball to the user.
5. **After the user confirms the hardware test**: `git push origin main`, wait for CI (`gh run watch`)
   to be green, `git tag -a vX.Y.Z` + `git push origin vX.Y.Z`,
   `gh release create vX.Y.Z --prerelease --notes-file release/vX.Y.Z/release-notes.md <assets>`,
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
| `setup.sh` | `repo init/sync`: Toradex BSP **7.7.0** (`tdxref/default.xml`) + `manifests/labwc-demo.xml` (meta-wayland, meta-qt6 6.11, pinned) |
| `build.sh` | Logged `bitbake -k labwc-demo-image` |
| `config/image-packages.conf`, `config/python-packages.conf` | Software lists (included from `build/conf/local.conf`) |
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

## Testing

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
- Backports in our layer exist for Scarthgap gaps: sip 6.16.1 + python3-packaging 24.2 (PyQt6 6.11),
  setuptools-69 compatibility seds (pyqt6-sip, pyqt-builder, sip), scipy `f2py` isolation wrapper,
  matplotlib qhull location. OE's offline `systemctl` only supports `enable`/`mask`/`preset-all`.
