# i.MX8M Plus / Vivante: build the GLES2 renderer against imx-gpu-viv
# (virtual/egl, virtual/libgles2, libgbm-imx) and fail loudly if it cannot be
# found instead of silently producing a pixman-only wlroots. The pixman
# renderer is always built and is used as the runtime fallback.
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
