SUMMARY = "OpenCvSharp: OpenCV for .NET (C#)"
DESCRIPTION = "OpenCvSharp wrapper for C# and .NET, matching the image's OpenCV \
4.6 (NXP i.MX build with OpenCL): the native OpenCvSharpExtern library built \
against the system OpenCV, the managed OpenCvSharp.dll (net6.0 build, runs on \
.NET 10), and the NuGet packages as a local feed for C# projects on the board."
HOMEPAGE = "https://github.com/shimat/opencvsharp"
# OpenCvSharp: Apache-2.0; System.Memory, System.Runtime.CompilerServices.Unsafe
# (feed only): MIT
LICENSE = "Apache-2.0 & MIT"
LIC_FILES_CHKSUM = "file://LICENSE;md5=86d3f3a95c324c9479bd8986968f4327"

# The OpenCvSharp release for OpenCV 4.6: the native and managed parts must
# come from the same release
SRC_URI = " \
    git://github.com/shimat/opencvsharp.git;protocol=https;nobranch=1;name=src \
    https://www.nuget.org/api/v2/package/OpenCvSharp4/${PV};downloadfilename=opencvsharp4.${PV}.nupkg;unpack=0;name=opencvsharp4 \
    https://www.nuget.org/api/v2/package/System.Memory/4.5.4;downloadfilename=system.memory.4.5.4.nupkg;unpack=0;name=systemmemory \
    https://www.nuget.org/api/v2/package/System.Runtime.CompilerServices.Unsafe/6.0.0;downloadfilename=system.runtime.compilerservices.unsafe.6.0.0.nupkg;unpack=0;name=unsafe \
"
# Tag 4.6.0.20220608
SRCREV_src = "df902f4497d02811752eb3910784b26ae47399c5"
SRC_URI[opencvsharp4.sha256sum] = "f7a3472df2d5ea719c72f5d38186ffb320cedc26e712122b931ea1ccebab538f"
SRC_URI[systemmemory.sha256sum] = "dec0847f33b8823e4260672d97d657411461c00ada3107ec7bbcb32a845eeb91"
SRC_URI[unsafe.sha256sum] = "6c41b53e70e9eee298cff3a02ce5acdd15b04125589be0273f0566026720a762"

S = "${WORKDIR}/git"

DEPENDS = "opencv unzip-native"

inherit cmake

OECMAKE_SOURCEPATH = "${S}/src"
EXTRA_OECMAKE = "-DCMAKE_BUILD_TYPE=Release"

FEED_DIR = "${datadir}/nuget/opencvsharp-feed"

do_install:append() {
    # Native library: the managed code loads "OpenCvSharpExtern"
    # (libOpenCvSharpExtern.so) from the library path
    if [ -d ${D}${prefix}/lib ] && [ "${prefix}/lib" != "${libdir}" ]; then
        mv ${D}${prefix}/lib/* ${D}${libdir}/ && rmdir ${D}${prefix}/lib
    fi

    # Managed assembly, for projects that reference it directly
    install -d ${D}${libdir}/opencvsharp
    unzip -o -j ${WORKDIR}/opencvsharp4.${PV}.nupkg 'lib/net6.0/OpenCvSharp.dll' 'lib/net6.0/OpenCvSharp.xml' \
        -d ${D}${libdir}/opencvsharp
    chmod 0644 ${D}${libdir}/opencvsharp/*

    # Offline NuGet feed with the managed package and its dependencies
    install -d ${D}${FEED_DIR}
    install -m 0644 ${WORKDIR}/opencvsharp4.${PV}.nupkg \
        ${WORKDIR}/system.memory.4.5.4.nupkg \
        ${WORKDIR}/system.runtime.compilerservices.unsafe.6.0.0.nupkg \
        ${D}${FEED_DIR}/
}

PACKAGES =+ "${PN}-nuget"

# libOpenCvSharpExtern.so is unversioned and loaded by name at run time
FILES_SOLIBSDEV = ""
FILES:${PN} += "${libdir}/libOpenCvSharpExtern.so ${libdir}/opencvsharp"
FILES:${PN}-nuget = "${FEED_DIR}"
INSANE_SKIP:${PN} += "dev-so"

RDEPENDS:${PN} = "dotnet"
