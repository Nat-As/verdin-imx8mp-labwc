SUMMARY = "GPU Test: OpenGL ES demo in C# (.NET)"
DESCRIPTION = "Renders a slowly rotating cyclohexane molecule (chair \
conformation, ball-and-stick) with OpenGL ES in an Avalonia window (native \
Wayland backend, GPU rendered, 40 % opaque background) and shows the measured \
frame rate. Written in C#, runs on the .NET runtime. Launch it from the \
application launcher (GPU Test) or with gpu-test."
# The app is MIT; it ships Avalonia, NWayland, MicroCom, Tmds.DBus.Protocol,
# SkiaSharp and HarfBuzzSharp (MIT) with the native Skia (BSD-3-Clause) and
# HarfBuzz (MIT) libraries from NuGet
LICENSE = "MIT & BSD-3-Clause"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302 \
                    file://${COMMON_LICENSE_DIR}/BSD-3-Clause;md5=550794465ba0ec5312d6919e203a55f9"

SRC_URI = " \
    file://GpuTest/ \
    file://gpu-test.desktop \
    file://gpu-test.png \
    file://gpu-test.sh \
"

S = "${WORKDIR}"

# Avalonia 12.1.4 (Avalonia.Wayland) and its dependencies, from nuget.org.
# Regenerate after changing GpuTest.csproj: dotnet restore -r linux-arm64, then
# scripts/nuget-inc.py GpuTest/obj/project.assets.json gpu-test-nuget.inc
require gpu-test-nuget.inc

inherit labwc-demo-dotnet

DOTNET_PROJECT = "GpuTest/GpuTest.csproj"
# Framework-dependent for the board: only its native libraries (libSkiaSharp,
# libHarfBuzzSharp for linux-arm64) are copied from the NuGet packages
DOTNET_BUILD_ARGS = "-r ${DOTNET_RID} --self-contained false"
# Own launcher (logs to the journal when started from the desktop)
DOTNET_LAUNCHER = ""

do_install:append() {
    install -Dm0755 ${WORKDIR}/gpu-test.sh ${D}${bindir}/gpu-test
    install -Dm0644 ${WORKDIR}/gpu-test.desktop ${D}${datadir}/applications/gpu-test.desktop
    install -Dm0644 ${WORKDIR}/gpu-test.png ${D}${datadir}/icons/hicolor/128x128/apps/gpu-test.png
}

FILES:${PN} += "${datadir}/applications ${datadir}/icons"

# Loaded at run time (P/Invoke, dlopen) by Avalonia.Wayland and GPU Test;
# EGL/OpenGL ES and GBM come with the GPU driver
RDEPENDS:${PN} += "wayland libxkbcommon glib-2.0 libdrm fontconfig"
