# meta-qt6 (6.11) pins setuptools >= 75.8.1, but Yocto Scarthgap ships
# setuptools 69. The package builds fine with it (setup.py uses the classic
# license keyword), so relax the build requirement.
do_compile:prepend() {
    sed -i -e 's/"setuptools *>= *[0-9.]*"/"setuptools"/' ${S}/pyproject.toml
}
