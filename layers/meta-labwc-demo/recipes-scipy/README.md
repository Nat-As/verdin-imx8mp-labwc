# recipes-scipy

SciPy and its build dependencies, copied from
[meta-python-ai](https://github.com/zboszor/meta-python-ai) (branch `scarthgap`,
commit `4f59c71ec2ce33f43f79732788c967b6f5436322`, MIT licensed, see
`LICENSE.meta-python-ai`). The whole layer is not used because it depends on a
clang layer that this image does not need.

Local changes:
- `python3-scipy`: the target build no longer depends on `python3-scipy-native`
  (which needs a host gfortran); the target is compiled with the cross gfortran
  enabled by `FORTRAN:forcevariable = ",fortran"` in `build/conf/local.conf`.
- `openblas`: `PACKAGECONFIG = "lapack cblas affinity dynarch"`, as required by
  SciPy (see the meta-python-ai README).

Requires in local.conf:

    FORTRAN:forcevariable = ",fortran"
    RUNTIMETARGET:append:pn-gcc-runtime = " libquadmath"
