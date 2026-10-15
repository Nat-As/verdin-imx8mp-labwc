# meta-qt6 (6.11) PyQt-builder uses PEP 639 metadata (SPDX string in
# project.license, project.license-files), which needs setuptools >= 77.
# Yocto Scarthgap ships setuptools 69: use the equivalent older syntax.
do_compile:prepend() {
    sed -i -e 's/"setuptools *>= *[0-9.]*"/"setuptools"/' \
           -e 's/^license = "\(.*\)"$/license = {text = "\1"}/' \
           -e '/^license-files = /d' \
           ${S}/pyproject.toml
}
