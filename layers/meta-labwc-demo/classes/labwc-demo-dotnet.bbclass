# Build a C# (.NET) application with the host .NET SDK (dotnet-native) and
# install it to ${libdir}/${DOTNET_APP_NAME}, with a launcher in ${bindir}.
#
# The application is built framework-dependent and architecture-neutral (IL):
# it runs on the board's .NET runtime (dotnet package). No native app host is
# built (it would be one for the build machine).
#
#   DOTNET_PROJECT   path of the .csproj, relative to ${S}
#   DOTNET_APP_NAME  install directory name (default ${BPN})
#   DOTNET_APP_DLL   main assembly (default: the project name + .dll)
#   DOTNET_LAUNCHER  launcher name in ${bindir} (default ${BPN}; empty: none)
#   DOTNET_BUILD_ARGS extra arguments for "dotnet build"
#   DOTNET_RID       .NET runtime identifier of the board (for "-r", e.g. to
#                    build framework-dependent apps with the board's native
#                    libraries from NuGet packages only)
#
# NuGet packages: a recipe lists them in SRC_URI with subdir=nuget-feed (an
# .inc generated with scripts/nuget-inc.py); they are then restored offline
# from ${WORKDIR}/nuget-feed only.

DEPENDS:append = " dotnet-native"
RDEPENDS:${PN}:append = " dotnet"

DOTNET_PROJECT ??= ""
DOTNET_APP_NAME ??= "${BPN}"
DOTNET_APP_DLL ??= "${@os.path.splitext(os.path.basename(d.getVar('DOTNET_PROJECT')))[0]}.dll"
DOTNET_LAUNCHER ??= "${BPN}"
DOTNET_BUILD_ARGS ??= ""
DOTNET_RID ??= "linux-${@{'aarch64': 'arm64', 'arm': 'arm', 'x86_64': 'x64'}.get(d.getVar('TARGET_ARCH'), d.getVar('TARGET_ARCH'))}"
DOTNET_NUGET_FEED = "${WORKDIR}/nuget-feed"

export HOME = "${WORKDIR}/dotnet-home"
export DOTNET_CLI_HOME = "${WORKDIR}/dotnet-home"
export DOTNET_NOLOGO = "1"
export DOTNET_CLI_TELEMETRY_OPTOUT = "1"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
export DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = "1"
export DOTNET_GENERATE_ASPNET_CERTIFICATE = "false"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT = "1"
export NUGET_PACKAGES = "${WORKDIR}/nuget-packages"
export MSBUILDDISABLENODEREUSE = "1"
export AVALONIA_TELEMETRY_OPTOUT = "1"

do_configure[noexec] = "1"

do_compile() {
    feed=""
    if [ -d ${DOTNET_NUGET_FEED} ]; then
        feed="--source ${DOTNET_NUGET_FEED}"
    fi
    dotnet build ${S}/${DOTNET_PROJECT} --configuration Release $feed \
        --output ${B}/out -nodeReuse:false \
        -p:UseAppHost=false -p:UseSharedCompilation=false \
        -p:Deterministic=true -p:PathMap=${S}=/usr/src/${BPN} \
        ${DOTNET_BUILD_ARGS}
    dotnet build-server shutdown >/dev/null 2>&1 || true
}

do_install() {
    install -d ${D}${libdir}/${DOTNET_APP_NAME}
    cp -r --no-preserve=ownership ${B}/out/. ${D}${libdir}/${DOTNET_APP_NAME}/
    find ${D}${libdir}/${DOTNET_APP_NAME} -name '*.pdb' -delete
    if [ -n "${DOTNET_LAUNCHER}" ]; then
        install -d ${D}${bindir}
        printf '#!/bin/sh\nexec %s %s "$@"\n' "${bindir}/dotnet" \
            "${libdir}/${DOTNET_APP_NAME}/${DOTNET_APP_DLL}" > ${D}${bindir}/${DOTNET_LAUNCHER}
        chmod 0755 ${D}${bindir}/${DOTNET_LAUNCHER}
    fi
}

FILES:${PN} += "${libdir}/${DOTNET_APP_NAME}"
INSANE_SKIP:${PN} += "libdir"
