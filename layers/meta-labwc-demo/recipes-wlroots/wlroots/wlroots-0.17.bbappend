# i.MX8M Plus / Vivante: build the GLES2 and Vulkan renderers against the
# Vivante stack from imx-gpu-viv (libEGL/libGLESv2/libgbm, libvulkan_VSI) and
# fail loudly if they cannot be found instead of silently producing a
# pixman-only wlroots. The pixman renderer is always built and is used as the
# runtime fallback (see labwc-session).
FILESEXTRAPATHS:prepend := "${THISDIR}/files:"

SRC_URI += " \
    file://0001-egl-add-WLR_EGL_SKIP_DEVICE-and-log-EGL-setup-steps.patch \
    file://0002-vulkan-support-drivers-without-VK_EXT_physical_devic.patch \
    file://0003-gles2-add-WLR_RENDERER_FINISH-to-wait-for-the-GPU-af.patch \
    file://0004-vulkan-add-render-copy-no-foreign-and-finish-driver-.patch \
    file://0005-vulkan-add-WLR_VK_RENDER_BUFFER-copy-into-the-DMA-BU.patch \
"

PACKAGECONFIG:append = " opengl gbm vulkan"
PACKAGECONFIG:remove = "x11 xwayland"

EXTRA_OEMESON += " \
    -Drenderers=gles2,vulkan \
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
