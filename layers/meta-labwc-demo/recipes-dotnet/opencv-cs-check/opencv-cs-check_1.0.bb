SUMMARY = "OpenCV from C# (OpenCvSharp) with OpenCL: check and example"
DESCRIPTION = "Small C# program that uses OpenCV through OpenCvSharp: prints \
the OpenCV build and OpenCL status and times a small vision pipeline \
(grayscale, Gaussian blur, Canny) on the CPU and on the GPU (OpenCL, UMat). Its source is installed as an example project in \
${datadir}/dotnet-examples/OpenCvCheck."
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://${COMMON_LICENSE_DIR}/MIT;md5=0835ade698e0bcf8506ecda2f7b4f302"

SRC_URI = "file://OpenCvCheck/"

S = "${WORKDIR}"

DEPENDS = "opencvsharp"

inherit labwc-demo-dotnet

DOTNET_PROJECT = "OpenCvCheck/OpenCvCheck.csproj"
DOTNET_BUILD_ARGS = "-p:OpenCvSharpDir=${STAGING_LIBDIR}/opencvsharp"

do_install:append() {
    rm -f ${D}${libdir}/${DOTNET_APP_NAME}/*.xml
    install -d ${D}${datadir}/dotnet-examples/OpenCvCheck
    install -m 0644 ${WORKDIR}/OpenCvCheck/*.cs ${WORKDIR}/OpenCvCheck/*.csproj \
        ${D}${datadir}/dotnet-examples/OpenCvCheck/
}

FILES:${PN} += "${datadir}/dotnet-examples"
RDEPENDS:${PN} += "opencvsharp"
