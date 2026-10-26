SUMMARY = "GPU Test: OpenGL ES demo in C# (.NET)"
DESCRIPTION = "Renders a slowly rotating cyclohexane molecule (chair \
conformation, ball-and-stick) with OpenGL ES 2.0 in an SDL2 Wayland window and \
shows the measured frame rate. Written in C#, runs on the .NET runtime. \
Launch it from the application launcher (GPU Test) or with gpu-test."
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302"

SRC_URI = " \
    file://GpuTest/ \
    file://gpu-test.desktop \
    file://gpu-test.png \
    file://gpu-test.sh \
"

S = "${WORKDIR}"

inherit labwc-demo-dotnet

DOTNET_PROJECT = "GpuTest/GpuTest.csproj"
# Own launcher (logs to the journal when started from the desktop)
DOTNET_LAUNCHER = ""

do_install:append() {
    install -Dm0755 ${WORKDIR}/gpu-test.sh ${D}${bindir}/gpu-test
    install -Dm0644 ${WORKDIR}/gpu-test.desktop ${D}${datadir}/applications/gpu-test.desktop
    install -Dm0644 ${WORKDIR}/gpu-test.png ${D}${datadir}/icons/hicolor/128x128/apps/gpu-test.png
}

FILES:${PN} += "${datadir}/applications ${datadir}/icons"

# Loaded at run time (P/Invoke): SDL2 (Wayland window); EGL/OpenGL ES come
# with the GPU driver
RDEPENDS:${PN} += "libsdl2"
