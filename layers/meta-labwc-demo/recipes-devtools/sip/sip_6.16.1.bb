# Backport of sip 6.16.1 (meta-openembedded master, MIT) for Yocto Scarthgap
# (which has 6.8.6): PyQt6 6.11 (meta-qt6 6.11) requires sip >= 6.15.
# Copyright (C) 2022 Khem Raj <raj.khem@gmail.com>
SUMMARY = "A Python bindings generator for C/C++ libraries"

HOMEPAGE = "https://github.com/Python-SIP/sip"
LICENSE = "BSD-2-Clause"
SECTION = "devel"
LIC_FILES_CHKSUM = "file://LICENSE;md5=236276327275fdb261636fb40b18d88d"

inherit pypi python_setuptools_build_meta python3native

SRC_URI[sha256sum] = "0a739c9cd2929de4e0884456d8caf3cfb22c10534757c7797bd8dc6bd9ed69bc"

DEPENDS += "python3-setuptools-scm-native"

RDEPENDS:${PN} = " \
    python3-core \
    python3-packaging \
    python3-logging \
    python3-tomllib \
    python3-setuptools \
"

# sip's own metadata uses PEP 639 (needs setuptools >= 77); Scarthgap ships
# setuptools 69, so use the equivalent older syntax for building sip itself.
do_compile:prepend() {
    sed -i -e 's/"setuptools *>= *[0-9.]*"/"setuptools"/' \
           -e 's/^license = "\(.*\)"$/license = {text = "\1"}/' \
           -e '/^license-files = /d' \
           ${S}/pyproject.toml
}

BBCLASSEXTEND = "native"
