#!/usr/bin/env python3
"""Boot the labwc demo image's rootfs in qemu-system-aarch64 and check that the
labwc desktop comes up from systemd at boot.

Test tooling only (not part of the image). Uses the generic qemuarm64 kernel
from build-qemu-test, the image's .wic (rootfs = partition 2), virtio-gpu as
the DRM/KMS device and virtio keyboard/tablet as input. The i.MX-specific
hardware (Vivante GPU, LCDIF/HDMI) cannot be emulated, so wlroots falls back
to the pixman renderer here.

Usage: qemu-boot-test.py <Image> <disk.wic> <workdir>
Produces <workdir>/serial.log, <workdir>/diag.txt and <workdir>/screen-*.png.
"""
import os
import socket
import subprocess
import sys
import time

kernel, disk, work = sys.argv[1:4]
os.makedirs(work, exist_ok=True)
# AF_UNIX paths are limited to 108 bytes: keep the sockets in a short dir
sock_dir = os.environ.get("XDG_RUNTIME_DIR") or "/tmp"
ser_path = os.path.join(sock_dir, f"lwq-{os.getpid()}-serial.sock")
mon_path = os.path.join(sock_dir, f"lwq-{os.getpid()}-monitor.sock")
for p in (ser_path, mon_path):
    if os.path.exists(p):
        os.unlink(p)

qemu = subprocess.Popen([
    "qemu-system-aarch64", "-M", "virt", "-cpu", "cortex-a57", "-smp", "4", "-m", "2048",
    "-kernel", kernel,
    "-append", "root=/dev/vda2 rw rootwait console=ttyAMA0 console=tty1",
    "-drive", f"file={disk},if=virtio,format={'qcow2' if disk.endswith('.qcow2') else 'raw'}",
    "-device", "virtio-gpu-pci,xres=1280,yres=800",
    "-device", "virtio-keyboard-pci", "-device", "virtio-tablet-pci",
    "-netdev", "user,id=n0", "-device", "virtio-net-pci,netdev=n0",
    "-display", "none",
    "-serial", f"unix:{ser_path},server=on,wait=off",
    "-monitor", f"unix:{mon_path},server=on,wait=off",
], stdout=open(os.path.join(work, "qemu.out"), "w"), stderr=subprocess.STDOUT)

for _ in range(100):
    if os.path.exists(ser_path) and os.path.exists(mon_path):
        break
    time.sleep(0.1)

ser = socket.socket(socket.AF_UNIX)
ser.connect(ser_path)
ser.settimeout(1)
log = open(os.path.join(work, "serial.log"), "wb")
buf = b""


def read_until(token, timeout):
    global buf
    end = time.time() + timeout
    while time.time() < end:
        if token.encode() in buf:
            i = buf.index(token.encode()) + len(token)
            out, buf = buf[:i], buf[i:]
            return out.decode(errors="replace")
        try:
            data = ser.recv(4096)
        except socket.timeout:
            continue
        if not data:
            break
        log.write(data)
        log.flush()
        buf += data
    raise TimeoutError(f"timeout waiting for {token!r}")


def send(line):
    ser.sendall(line.encode() + b"\n")


def screendump(name):
    mon = socket.socket(socket.AF_UNIX)
    mon.connect(mon_path)
    ppm = os.path.join(work, name + ".ppm")
    mon.sendall(f"screendump {ppm}\n".encode())
    time.sleep(3)
    mon.close()
    subprocess.run(["convert", ppm, os.path.join(work, name + ".png")], check=False)


MARK = "__CMD_DONE__"


def run(cmd, timeout=120):
    # The marker is split in the command line so that the terminal's echo of
    # the command never matches it; only the command's own output does.
    send(f"{cmd}; echo __CMD\"\"_DONE__")
    return read_until(MARK, timeout)


try:
    read_until("login:", 900)
    t_login = time.time()
    send("root")
    read_until("# ", 120)
    run("export SYSTEMD_PAGER= SYSTEMD_COLORS=0 TERM=dumb; stty cols 200")
    # Give labwc and the autostart clients time to come up under TCG
    time.sleep(150)
    screendump("screen-boot")
    diag = []
    for cmd in [
        "systemctl is-system-running; systemctl get-default",
        "systemctl --no-pager status seatd labwc | head -40",
        "loginctl list-sessions --no-legend; cat /sys/class/tty/tty0/active",
        "ps -eo user,pid,args | grep -E 'seatd|labwc|waybar|swaybg|dbus-daemon' | grep -v grep",
        "ls -la /run/user/1000/ 2>&1 | head; cat /run/user/1000/labwc-renderer",
        "journalctl -b -t labwc --no-pager -o cat | grep -E 'labwc-session|Creating .* renderer|Seat opened|ERROR' | head -30",
        "journalctl -b -u seatd --no-pager | tail -15",
        "ls -la /dev/dri; id dev",
        "env | grep DISPLAY; sudo -u dev -i sh -c 'env | grep -E DISPLAY'",
        "systemd-cgls --no-pager -u user.slice | head -20; systemctl status labwc --no-pager | grep -A3 CGroup",
        "journalctl -b --no-pager -o verbose -g labwc-session | grep -E '_SYSTEMD_UNIT|_SYSTEMD_USER_UNIT|SYSLOG_IDENTIFIER|MESSAGE=' | head -12",
        "journalctl -b --no-pager _UID=1000 -o cat | grep -E 'labwc-session|Creating .* renderer|GLES2|EGL|Failed|ERROR' | head -30",
    ]:
        diag.append(f"$ {cmd}\n{run(cmd)}\n")
    # Launch a terminal into the running session as dev to check client startup
    run("runuser -u dev -- env XDG_RUNTIME_DIR=/run/user/1000 WAYLAND_DISPLAY=wayland-0 LANG=en_US.UTF-8 sh -c 'foot -T htop htop >/tmp/foot.log 2>&1 &'")
    time.sleep(60)
    screendump("screen-foot")
    diag.append(f"$ cat /tmp/foot.log\n{run('cat /tmp/foot.log')}\n")
    # Restart the session to make sure the unit is restartable
    diag.append(f"$ systemctl restart labwc\n{run('systemctl restart labwc; sleep 5; systemctl is-active labwc')}\n")
    with open(os.path.join(work, "diag.txt"), "w") as f:
        f.write("".join(diag))
    send("poweroff")
    time.sleep(20)
finally:
    qemu.kill()
    log.close()
