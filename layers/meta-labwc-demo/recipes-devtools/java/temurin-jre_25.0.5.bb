SUMMARY = "Java Runtime Environment (Eclipse Temurin)"
DESCRIPTION = "Java 25 (LTS) runtime from Eclipse Adoptium's Temurin binary \
release (OpenJDK HotSpot JRE for aarch64): java, keytool, jfr, jwebserver, \
jrunscript and rmiregistry, with JAVA_HOME set for sh and fish."
HOMEPAGE = "https://adoptium.net"
LICENSE = "GPL-2.0-with-classpath-exception"
LIC_FILES_CHKSUM = " \
    file://legal/java.base/LICENSE;md5=3e0b59f8fac05c3c03d4a26bbda13f8f \
    file://legal/java.base/ASSEMBLY_EXCEPTION;md5=d54e5b1b11ce92d8abc22e8e756f54bd \
"

JAVA_FEATURE = "${@d.getVar('PV').split('.')[0]}"
JAVA_BUILD = "8"
JAVA_HOME_DIR = "${libdir}/jvm/temurin-${JAVA_FEATURE}-jre"

# https://api.adoptium.net/v3/assets/latest/25/hotspot?architecture=aarch64&image_type=jre&os=linux
SRC_URI = " \
    https://github.com/adoptium/temurin${JAVA_FEATURE}-binaries/releases/download/jdk-${PV}%2B${JAVA_BUILD}/OpenJDK${JAVA_FEATURE}U-jre_aarch64_linux_hotspot_${PV}_${JAVA_BUILD}.tar.gz;subdir=jre \
    file://java.sh \
    file://java.fish \
"
SRC_URI[sha256sum] = "963931723891302ebb2687178532c3d793d71e2577cd058061c7efb396140a86"

S = "${WORKDIR}/jre/jdk-${PV}+${JAVA_BUILD}-jre"

COMPATIBLE_HOST = "aarch64.*-linux"

# libfreetype is bundled; X11 for AWT/Swing (needs an X server, e.g.
# Xwayland), ALSA for javax.sound
RDEPENDS:${PN} = "zlib libx11 libxext libxi libxrender libxtst alsa-lib"

do_configure[noexec] = "1"
do_compile[noexec] = "1"

do_install() {
    install -d ${D}${JAVA_HOME_DIR} ${D}${bindir}
    cp -r --no-preserve=ownership ${S}/. ${D}${JAVA_HOME_DIR}/
    for tool in ${S}/bin/*; do
        ln -rs ${D}${JAVA_HOME_DIR}/bin/$(basename $tool) ${D}${bindir}/$(basename $tool)
    done

    install -d ${D}${sysconfdir}/profile.d ${D}${sysconfdir}/fish/conf.d
    for f in java.sh java.fish; do
        sed 's,@JAVA_HOME@,${JAVA_HOME_DIR},' ${WORKDIR}/$f > ${WORKDIR}/$f.out
    done
    install -m 0644 ${WORKDIR}/java.sh.out ${D}${sysconfdir}/profile.d/java.sh
    install -m 0644 ${WORKDIR}/java.fish.out ${D}${sysconfdir}/fish/conf.d/java.fish
}

FILES:${PN} += "${JAVA_HOME_DIR}"

# Keep the binaries as Adoptium ships them, and don't offer the JRE's private
# libraries (libjava.so, its own libfreetype.so, ...) to other packages
INHIBIT_PACKAGE_STRIP = "1"
INHIBIT_PACKAGE_DEBUG_SPLIT = "1"
EXCLUDE_FROM_SHLIBS = "1"

INSANE_SKIP:${PN} = "already-stripped ldflags textrel file-rdeps libdir"
