SUMMARY = "GPU/NPU diagnostics for the labwc demo image"
DESCRIPTION = "labwc-gpu-probe starts labwc with each GPU renderer strategy \
(headless or on the displays) and reports crashes with a gdb backtrace. \
labwc-hwaccel-check reports the state of the GPU (Vulkan, OpenGL ES, OpenCL, \
G2D), NPU (OpenVX, TensorFlow Lite VX delegate), VPU and board hardware. \
vk-scanout-test checks which Vulkan operations write to display buffers."
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302"

SRC_URI = " \
    file://labwc-gpu-probe \
    file://labwc-hwaccel-check \
    file://vk-scanout-test.c \
    file://fill.vert \
    file://fill.frag \
    file://pattern.frag \
"

S = "${WORKDIR}"

DEPENDS = "vulkan-loader vulkan-headers virtual/libgbm libdrm glslang-native"

inherit pkgconfig

# Links against the i.MX GPU's libgbm
PACKAGE_ARCH = "${MACHINE_SOCARCH}"

do_compile() {
    glslangValidator -V --vn fill_vert -o fill_vert.h ${S}/fill.vert
    glslangValidator -V --vn fill_frag -o fill_frag.h ${S}/fill.frag
    glslangValidator -V --vn pattern_frag -o pattern_frag.h ${S}/pattern.frag
    ${CC} ${CFLAGS} ${LDFLAGS} -Wall -Wextra -Wno-unused-parameter -I. \
        $(pkg-config --cflags libdrm) ${S}/vk-scanout-test.c -o vk-scanout-test \
        -lvulkan -lgbm -ldrm
}

do_install() {
    install -Dm0755 ${WORKDIR}/labwc-gpu-probe ${D}${bindir}/labwc-gpu-probe
    install -Dm0755 ${WORKDIR}/labwc-hwaccel-check ${D}${bindir}/labwc-hwaccel-check
    install -Dm0755 ${B}/vk-scanout-test ${D}${bindir}/vk-scanout-test
}

RDEPENDS:${PN} = " \
    bash \
    coreutils \
    gawk \
    grep \
    sed \
    gdb \
    python3-core \
    python3-ctypes \
    labwc \
    foot \
    vulkan-tools \
    clinfo \
"
