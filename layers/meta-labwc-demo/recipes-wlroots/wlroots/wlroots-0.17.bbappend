# i.MX8M Plus / Vivante: build the GLES2 renderer against the Vivante stack
# from imx-gpu-viv (libEGL/libGLESv2/libgbm) and fail loudly if it cannot be
# found instead of silently producing a pixman-only wlroots. The pixman
# renderer is always built and is used as the runtime fallback (see
# labwc-session). The Vulkan renderer is not built: the Vivante Vulkan driver
# cannot render into the display buffers wlroots allocates (v0.1.x tests).
FILESEXTRAPATHS:prepend := "${THISDIR}/files:"

SRC_URI += " \
    file://0001-egl-add-WLR_EGL_SKIP_DEVICE-and-log-EGL-setup-steps.patch \
    file://0002-gles2-add-WLR_RENDERER_FINISH-to-wait-for-the-GPU-af.patch \
"

PACKAGECONFIG:append = " opengl gbm"
PACKAGECONFIG:remove = "vulkan x11 xwayland"

EXTRA_OEMESON += " \
    -Drenderers=gles2 \
    -Dallocators=gbm \
    -Dbackends=drm,libinput \
    -Dsession=enabled \
    -Dxwayland=disabled \
"

# The recipe installs the tinywl example; we don't ship it
do_install:append() {
    rm -f ${D}${bindir}/tinywl-0.17
    rmdir --ignore-fail-on-non-empty ${D}${bindir}
}
