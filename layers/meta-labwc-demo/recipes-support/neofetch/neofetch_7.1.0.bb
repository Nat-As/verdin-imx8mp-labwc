SUMMARY = "A command-line system information tool written in bash"
HOMEPAGE = "https://github.com/dylanaraps/neofetch"
SECTION = "console/utils"
LICENSE = "MIT"
LIC_FILES_CHKSUM = "file://LICENSE.md;md5=d300b86297c170b6498705fbb6794e3f"

SRC_URI = "git://github.com/dylanaraps/neofetch.git;protocol=https;branch=master"
# Tag 7.1.0 (last release; the project is archived upstream)
SRCREV = "60d07dee6b76769d8c487a40639fb7b5a1a7bc85"

S = "${WORKDIR}/git"

inherit allarch

do_compile[noexec] = "1"

# Not the upstream "make install": it uses "cp -p", which keeps the build
# host's file ownership.
do_install() {
    install -Dm0755 ${S}/neofetch ${D}${bindir}/neofetch
    install -Dm0644 ${S}/neofetch.1 ${D}${mandir}/man1/neofetch.1
}

RDEPENDS:${PN} = "bash"
