# Also build QtWidgets (needed by matplotlib's Qt backend and most PyQt
# applications), QtPrintSupport, QtSvg and QtQuick.
PYQT_MODULES:append = " \
    QtWidgets \
    QtPrintSupport \
    QtQuick \
    QtSvg \
    QtSvgWidgets \
"
DEPENDS:append = " qtsvg"
RDEPENDS:${PN}:append = " qtsvg"

# The distro has the x11 feature, but qtbase is built without the XCB
# platform here (Wayland only), so QNativeInterface::QX11Application does not
# exist: do not build PyQt's X11 native interface.
DISABLED_FEATURES:append = " PyQt_XCB"
