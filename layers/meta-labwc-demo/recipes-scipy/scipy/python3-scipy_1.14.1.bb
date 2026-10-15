# Copied from meta-python-ai (scarthgap, 4f59c71e), MIT. See ../README.md.
SUMMARY = "SciPy: Scientific Library for Python"
HOMEPAGE = "https://www.scipy.org"
LICENSE = "BSD-3-Clause & BSD-2-Clause & MIT & BSL-1.0 & Qhull & BSD-3-Clause-LBNL"
LIC_FILES_CHKSUM = " \
	file://LICENSE.txt;md5=5f477c3073ea2d02a70a764319f5f873 \
	file://LICENSES_bundled.txt;md5=339da992791ab835a9b0c61a6b958ea0 \
"

inherit pkgconfig pypi python_mesonpy

SRC_URI[sha256sum] = "5a275584e726026a5699459aa72f828a610821006228e841b94275c4a7c08417"

SRC_URI += " \
	file://use-unversioned-numpy.patch \
	file://0001-Conditionally-import-C-modules.patch \
"

DEPENDS += " \
	python3-numpy-native \
	python3-pybind11-native \
	python3-pythran-native \
	python3-gast-native \
	python3-beniget-native \
	python3-ply-native \
	xsimd \
"

# meta-labwc-demo: python3-scipy-native dropped (needs a host gfortran)
DEPENDS:append:class-target = " \
	python3-numpy \
"

RDEPENDS:${PN} += " \
	python3-numpy \
"

PACKAGECONFIG ?= "openblas pythran"

PACKAGECONFIG[openblas] = "-Dblas=openblas -Dlapack=openblas,,openblas,openblas"
PACKAGECONFIG[lapack] = "-Dblas=lapack -Dlapack=lapack,,lapack,lapack"
PACKAGECONFIG[f77] = "-Duse-g77-abi=true,,,"
PACKAGECONFIG[pythran] = "-Duse-pythran=true,-Duse-pythran=false,python3-pythran-native python3-pythran,python3-pythran"

CLEANBROKEN = "1"

export LAPACK = "${STAGING_LIBDIR}"
export BLAS = "${STAGING_LIBDIR}"

F90:class-native = "${FC}"
F90:class-target = "${TARGET_PREFIX}gfortran"
export F90

export F77 = "${TARGET_PREFIX}gfortran"

BBCLASSEXTEND = "native nativesdk"

do_configure:append:class-target() {
	if ! grep -q "numpy-include-dir" "${WORKDIR}/meson.cross" ; then
		sed -i "s,\[properties\],\[properties\]\nnumpy-include-dir = \'${RECIPE_SYSROOT}${PYTHON_SITEPACKAGES_DIR}/numpy/core/include/\' ,g" "${WORKDIR}/meson.cross"
	fi
}

# meta-labwc-demo: Scarthgap ships pybind11 2.11.1; SciPy 1.14 pins
# 2.12.x pre-emptively ("upper bound is pre-emptive only") and builds fine
# with 2.11. Relax the pin in the build requirements and the meson check.
do_configure:prepend() {
	sed -i -e 's/"pybind11>=2.12.0,<2.13.0"/"pybind11>=2.11.0"/' ${S}/pyproject.toml
	sed -i -e "s/dependency('pybind11', version: '>=2.12.0')/dependency('pybind11', version: '>=2.11.0')/" ${S}/scipy/meson.build
}

# meta-labwc-demo: meson runs the native f2py from the scipy/ source
# subdirectory, and the build environment puts that directory on sys.path, so
# "import signal" in the standard library picks up scipy/signal/ and f2py
# fails (meta-python-ai hid this by building python3-scipy-native). Run f2py
# through a wrapper in isolated mode (-I: ignore PYTHONPATH, no cwd on
# sys.path); numpy is found in the native sysroot's site-packages.
do_compile:prepend() {
	mkdir -p ${WORKDIR}/f2py-isolated
	cat > ${WORKDIR}/f2py-isolated/f2py <<'WRAPPER'
#!/bin/sh
exec nativepython3 -I -c 'import sys; from numpy.f2py.f2py2e import main; sys.argv[0] = "f2py"; sys.exit(main())' "$@"
WRAPPER
	chmod +x ${WORKDIR}/f2py-isolated/f2py
	export PATH="${WORKDIR}/f2py-isolated:$PATH"
}
