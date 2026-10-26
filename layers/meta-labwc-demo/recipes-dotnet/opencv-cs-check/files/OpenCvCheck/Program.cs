// opencv-cs-check: OpenCV from C# with hardware acceleration.
// Prints the OpenCV build, whether OpenCL (the GPU) is available, and times a
// small vision pipeline (grayscale, Gaussian blur, Canny) on a 1920x1080 frame
// on the CPU (Mat) and with OpenCL (UMat).
//
//   opencv-cs-check [-q]     -q: print only "opencl" or "cpu"
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OpenCvSharp;

internal static class Program
{
    // cv::ocl functions of OpenCV's core library (not wrapped by OpenCvSharp 4.6)
    private const string Core = "libopencv_core.so.406";
    [DllImport(Core, EntryPoint = "_ZN2cv3ocl10haveOpenCLEv")] private static extern bool HaveOpenCL();
    [DllImport(Core, EntryPoint = "_ZN2cv3ocl9useOpenCLEv")] private static extern bool UseOpenCL();
    [DllImport(Core, EntryPoint = "_ZN2cv3ocl12setUseOpenCLEb")] private static extern void SetUseOpenCL(bool flag);

    [DllImport("libc", EntryPoint = "access", SetLastError = true)]
    private static extern int Access(string path, int mode);

    // OpenCV reads OPENCV_OPENCL_RUNTIME from the native environment, which
    // Environment.SetEnvironmentVariable doesn't change on Linux
    [DllImport("libc", EntryPoint = "setenv")]
    private static extern int SetEnv(string name, string value, int overwrite);

    private static void DisableOpenCL() => SetEnv("OPENCV_OPENCL_RUNTIME", "disabled", 1);

    private static int Main(string[] args)
    {
        bool quiet = args.Contains("-q");
        // The GPU driver ends the process if it can't open the GPU device:
        // check the access first and fall back to the CPU
        const string gpu = "/dev/galcore";
        if (!File.Exists(gpu))
        {
            if (!quiet)
                Console.WriteLine($"No GPU device ({gpu}): using the CPU only.");
            DisableOpenCL();
        }
        else if (Access(gpu, 6 /* R_OK | W_OK */) != 0)
        {
            if (!quiet)
                Console.WriteLine($"No access to {gpu} (the GPU): run as a member of the video group or with sudo. Using the CPU only.");
            DisableOpenCL();
        }
        bool have = HaveOpenCL();
        SetUseOpenCL(true);
        bool use = UseOpenCL();
        if (quiet)
        {
            Console.WriteLine(have && use ? "opencl" : "cpu");
            return 0;
        }

        // (OpenCvSharp's assemblies carry no release number: 4.6.0.20220608 in this image)
        Console.WriteLine($"OpenCV {Cv2.GetVersionString()} via OpenCvSharp");
        string opencl = Cv2.GetBuildInformation().Split('\n')
            .FirstOrDefault(l => l.TrimStart().StartsWith("OpenCL:"))?.Trim() ?? "OpenCL: ?";
        Console.WriteLine($"Build: {opencl}");
        Console.WriteLine($"OpenCL available: {have}, in use: {use}");

        // A typical vision pipeline on a 1080p colour frame: grayscale, 7x7
        // Gaussian blur, Canny edges. Mat runs on the CPU, UMat with OpenCL on
        // the GPU. Two warm-up runs first (OpenCL compiles its kernels then).
        using var src = new Mat(1080, 1920, MatType.CV_8UC3);
        Cv2.Randu(src, new Scalar(0, 0, 0), new Scalar(255, 255, 255));
        const int runs = 10;

        double cpuMs = Time(runs, () =>
        {
            using var gray = new Mat();
            using var blur = new Mat();
            using var edges = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, blur, new Size(7, 7), 0);
            Cv2.Canny(blur, edges, 50, 150);
        });
        Console.WriteLine($"Gray + blur 7x7 + Canny, 1920x1080, CPU (Mat):     {cpuMs,7:0.0} ms");

        if (have && use)
        {
            using var usrc = src.GetUMat(AccessFlag.READ, UMatUsageFlags.None);
            double gpuMs = Time(runs, () =>
            {
                using var gray = new UMat();
                using var blur = new UMat();
                using var edges = new UMat();
                Cv2.CvtColor(usrc, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.GaussianBlur(gray, blur, new Size(7, 7), 0);
                Cv2.Canny(blur, edges, 50, 150);
                using var result = edges.GetMat(AccessFlag.READ);   // wait for the GPU
            });
            Console.WriteLine($"Gray + blur 7x7 + Canny, 1920x1080, OpenCL (UMat): {gpuMs,7:0.0} ms");
        }
        return 0;
    }

    // Average milliseconds per run, after two warm-up runs
    private static double Time(int runs, Action work)
    {
        work();
        work();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
            work();
        return sw.Elapsed.TotalMilliseconds / runs;
    }
}
