SUMMARY = ".NET runtime and SDK"
DESCRIPTION = ".NET 10 (LTS) from Microsoft's binary release: the dotnet host and \
the Microsoft.NETCore.App and Microsoft.AspNetCore.App runtimes in ${PN}, the \
SDK (to build and run C# projects on the board) in ${PN}-dev. The native \
variant builds the image's C# applications. Based on the dotnet recipe of \
meta-mono (scarthgap), which only provides .NET 8.0.7."
HOMEPAGE = "https://dotnet.microsoft.com"
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://LICENSE.txt;md5=9fc642ff452b28d62ab19b7eea50dfb9"

DOTNET_RUNTIME_VERSION = "10.0.14"

# https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json
SRC_ARCH:aarch64 = "arm64"
SRC_ARCH:x86-64 = "x64"
SRC_SHA512SUM:aarch64 = "0966aa2b70f4677b8319be9df447a78a0dcd6164836425b55a68fd714f4023feb87b4cac21b9a996b79472ad54487cd0adcd2a9dfd570e2b2a1d26423a2baa9b"
SRC_SHA512SUM:x86-64 = "b7b3d8461cf98b07d2552ce9199702fa3ad010ac2671a5f8cd5136f7534e50f6855ba7bbb02b11494563f1d04156276a31cda1103b900e5eecd0d9473dc7b9c5"

SRC_URI = "https://builds.dotnet.microsoft.com/dotnet/Sdk/${PV}/dotnet-sdk-${PV}-linux-${SRC_ARCH}.tar.gz;subdir=dotnet-${PV}"
SRC_URI[sha512sum] = "${SRC_SHA512SUM}"
SRC_URI[vardeps] += "SRC_SHA512SUM SRC_ARCH"

S = "${WORKDIR}/dotnet-${PV}"

COMPATIBLE_HOST = "(x86_64|aarch64).*-linux"

DEPENDS = "patchelf-native"

# libssl: TLS and cryptography (loaded at run time); libgssapi-krb5: HTTP
# authentication; icu: globalization
RDEPENDS:${PN}:class-target = "icu zlib libgssapi-krb5 libssl"
RDEPENDS:${PN}:class-native = "zlib"
RDEPENDS:${PN}:class-nativesdk = "icu zlib"

do_configure[noexec] = "1"
do_compile[noexec] = "1"

do_install[vardeps] += "DOTNET_RUNTIME_VERSION"

do_install() {
    install -d ${D}${datadir}/dotnet ${D}${bindir}
    install -m 0755 ${S}/dotnet ${D}${datadir}/dotnet/dotnet
    ln -rs ${D}${datadir}/dotnet/dotnet ${D}${bindir}/dotnet
    install -m 0644 ${S}/LICENSE.txt ${S}/ThirdPartyNotices.txt ${D}${datadir}/dotnet/

    install -d ${D}${datadir}/dotnet/host/fxr ${D}${datadir}/dotnet/shared
    cp -r --no-preserve=ownership ${S}/host/fxr/${DOTNET_RUNTIME_VERSION} ${D}${datadir}/dotnet/host/fxr/
    for fw in Microsoft.NETCore.App Microsoft.AspNetCore.App; do
        install -d ${D}${datadir}/dotnet/shared/$fw
        cp -r --no-preserve=ownership ${S}/shared/$fw/${DOTNET_RUNTIME_VERSION} ${D}${datadir}/dotnet/shared/$fw/
    done

    # SDK
    cp -r --no-preserve=ownership ${S}/sdk ${S}/sdk-manifests ${S}/templates ${D}${datadir}/dotnet/
    install -m 0755 ${S}/dnx ${D}${datadir}/dotnet/dnx
    ln -rs ${D}${datadir}/dotnet/dnx ${D}${bindir}/dnx
    if [ -d ${S}/packs ]; then
        cp -r --no-preserve=ownership ${S}/packs ${D}${datadir}/dotnet/
    fi

    # The LTTng tracing provider needs liblttng-ust.so.0, which the image
    # doesn't have; tracing is optional
    for f in $(find ${D}${datadir}/dotnet -name libcoreclrtraceptprovider.so); do
        patchelf --remove-needed liblttng-ust.so.0 $f || true
        patchelf --remove-needed liblttng-ust.so.1 $f || true
    done

    install -d ${D}${libdir}
    ln -rs ${D}${datadir}/dotnet/host/fxr/${DOTNET_RUNTIME_VERSION}/libhostfxr.so ${D}${libdir}/libhostfxr.so
}

# Where the dotnet host looks for the runtime when started via a symlink
do_install:append:class-target() {
    install -d ${D}${sysconfdir}/dotnet
    echo "${datadir}/dotnet" > ${D}${sysconfdir}/dotnet/install_location
}

FILES:${PN} += " \
    ${datadir}/dotnet/dotnet \
    ${datadir}/dotnet/*.txt \
    ${datadir}/dotnet/host \
    ${datadir}/dotnet/shared \
    ${libdir}/libhostfxr.so \
    ${sysconfdir}/dotnet \
"
FILES:${PN}-dev = " \
    ${bindir}/dnx \
    ${datadir}/dotnet/dnx \
    ${datadir}/dotnet/sdk \
    ${datadir}/dotnet/sdk-manifests \
    ${datadir}/dotnet/templates \
    ${datadir}/dotnet/packs \
"
FILES:${PN}-dbg = "${datadir}/dotnet/.debug"
RRECOMMENDS:${PN}-dev[nodeprrecs] = "1"
RDEPENDS:${PN}-dev = "${PN}"

INSANE_SKIP:${PN} = "already-stripped libdir staticdev textrel dev-so file-rdeps"
INSANE_SKIP:${PN}-dev = "already-stripped libdir staticdev textrel file-rdeps dev-elf"
INSANE_SKIP:${PN}-dbg = "libdir"

BBCLASSEXTEND = "native nativesdk"
