# Build OpenCV's OpenCL backend (cv::UMat / T-API) against the Vivante OpenCL
# 3.0 driver from imx-gpu-viv (which provides opencl-headers and
# virtual/opencl-icd on i.MX8). OpenCV loads libOpenCL.so at runtime, so
# OpenCL can still be disabled per process with OPENCV_OPENCL_RUNTIME=disabled.
PACKAGECONFIG:append:mx8-nxp-bsp = " opencl"
