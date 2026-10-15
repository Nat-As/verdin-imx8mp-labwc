SUMMARY = "fish - the friendly interactive shell"
HOMEPAGE = "https://fishshell.com/"
DESCRIPTION = "fish is a smart and user-friendly command line shell with \
syntax highlighting, autosuggestions and tab completions out of the box. \
3.7.x is the last C++ release series (4.x is written in Rust)."
SECTION = "base/shells"
LICENSE = "GPL-2.0-only & LGPL-2.0-or-later & MIT & ISC & PSF-2.0 & BSD-3-Clause"
LIC_FILES_CHKSUM = "file://COPYING;md5=62bf11ac21699b630f7c619c67090973"

SRC_URI = "https://github.com/fish-shell/fish-shell/releases/download/${PV}/fish-${PV}.tar.xz"
SRC_URI[sha256sum] = "614c9f5643cd0799df391395fa6bbc3649427bb839722ce3b114d3bbc1a3b250"

DEPENDS = "ncurses libpcre2"

inherit cmake pkgconfig

EXTRA_OECMAKE = " \
    -DFISH_USE_SYSTEM_PCRE2=ON \
    -DBUILD_DOCS=OFF \
    -DWITH_GETTEXT=OFF \
    -DCURSES_NEED_NCURSES=ON \
"

FILES:${PN} += " \
    ${datadir}/fish \
    ${datadir}/pkgconfig \
    ${datadir}/applications \
    ${datadir}/pixmaps \
"

# fish needs terminfo, man is used by the help system
RDEPENDS:${PN} = "ncurses-terminfo-base"
RRECOMMENDS:${PN} = "ncurses-terminfo"

# Register in /etc/shells so login daemons (dropbear, login) accept it
pkg_postinst:${PN} () {
    touch $D${sysconfdir}/shells
    grep -q "^${bindir}/fish$" $D${sysconfdir}/shells || echo "${bindir}/fish" >> $D${sysconfdir}/shells
}

pkg_postrm:${PN} () {
    sed -i "\#^${bindir}/fish\$#d" $D${sysconfdir}/shells
}
