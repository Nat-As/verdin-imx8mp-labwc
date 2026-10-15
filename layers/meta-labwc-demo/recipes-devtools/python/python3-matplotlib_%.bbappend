# The meta-python recipe unpacks the bundled qhull sources into
# ${S}/build, but Scarthgap's setuptools3 class cleans ${S}/build at
# do_configure, so matplotlib then tries (and fails) to download qhull during
# do_compile. Unpack qhull elsewhere and copy it into build/ right before
# compiling.
QHULL_TARBALL = "http://www.qhull.org/download/qhull-2020-src-8.0.2.tgz"
SRC_URI:remove = "${QHULL_TARBALL};name=qhull;subdir=matplotlib-${PV}/build"
SRC_URI += "${QHULL_TARBALL};name=qhull;subdir=matplotlib-${PV}/extern"

do_compile:prepend() {
    mkdir -p ${S}/build
    rm -rf ${S}/build/qhull-2020.2
    cp -r ${S}/extern/qhull-2020.2 ${S}/build/
}
