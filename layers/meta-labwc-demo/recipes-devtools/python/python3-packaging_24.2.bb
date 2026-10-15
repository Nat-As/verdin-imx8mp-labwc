# Backport of python3-packaging 24.2 (OE-core walnascar) for Yocto Scarthgap
# (which has 23.2). Needed by sip >= 6.10 (packaging.licenses), which PyQt6 6.11
# (meta-qt6 6.11) requires. 24.x is backward compatible with 23.x.
SUMMARY = "Core utilities for Python packages"
HOMEPAGE = "https://github.com/pypa/packaging"
LICENSE = "Apache-2.0 | BSD-2-Clause"
LIC_FILES_CHKSUM = "file://LICENSE;md5=faadaedca9251a90b205c9167578ce91"

SRC_URI[sha256sum] = "c228a6dc5e932d346bc5739379109d49e8853dd8223571c7c5b55260edc0b97f"

inherit pypi python_flit_core

BBCLASSEXTEND = "native nativesdk"

# Bootstrap the native build
DEPENDS:remove:class-native = "python3-build-native"
RDEPENDS:${PN} += "python3-profile"

do_compile:class-native () {
    python_flit_core_do_manual_build
}
