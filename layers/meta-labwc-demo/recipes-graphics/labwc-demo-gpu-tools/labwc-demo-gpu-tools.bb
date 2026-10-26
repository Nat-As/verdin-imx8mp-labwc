SUMMARY = "GPU/NPU diagnostics for the labwc demo image"
DESCRIPTION = "labwc-gpu-probe starts labwc with each GPU renderer strategy \
(headless or on the displays) and reports crashes with a gdb backtrace. \
labwc-hwaccel-check reports the state of the GPU (OpenGL ES, OpenCL, G2D, \
OpenGL ES applications on the desktop), NPU (OpenVX, TensorFlow Lite VX \
delegate), VPU and board hardware."
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302"

SRC_URI = " \
    file://labwc-gpu-probe \
    file://labwc-hwaccel-check \
"

S = "${WORKDIR}"

do_install() {
    install -Dm0755 ${WORKDIR}/labwc-gpu-probe ${D}${bindir}/labwc-gpu-probe
    install -Dm0755 ${WORKDIR}/labwc-hwaccel-check ${D}${bindir}/labwc-hwaccel-check
}

RDEPENDS:${PN} = " \
    bash \
    coreutils \
    gawk \
    grep \
    sed \
    gdb \
    util-linux-runuser \
    python3-core \
    python3-ctypes \
    labwc \
    foot \
    clinfo \
"
