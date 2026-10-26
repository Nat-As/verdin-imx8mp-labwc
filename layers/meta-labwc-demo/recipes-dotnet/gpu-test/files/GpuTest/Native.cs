// P/Invoke bindings for the parts of EGL, GBM and OpenGL ES that GPU Test uses
// (the window comes from Avalonia; the off-screen mode uses EGL directly).
using System;
using System.Runtime.InteropServices;

namespace GpuTest
{
    internal static class Egl
    {
        private const string Lib = "libEGL.so.1";
        public const int NONE = 0x3038, SURFACE_TYPE = 0x3033, PBUFFER_BIT = 0x1,
            RENDERABLE_TYPE = 0x3040, OPENGL_ES2_BIT = 0x4, RED_SIZE = 0x3024, GREEN_SIZE = 0x3023,
            BLUE_SIZE = 0x3022, DEPTH_SIZE = 0x3025, WIDTH = 0x3057, HEIGHT = 0x3056,
            CONTEXT_CLIENT_VERSION = 0x3098;
        public const uint OPENGL_ES_API = 0x30A0;

        public const int PLATFORM_GBM_KHR = 0x31D7;
        [DllImport(Lib, EntryPoint = "eglGetDisplay")] public static extern IntPtr GetDisplay(IntPtr nativeDisplay);
        // EGL 1.5
        [DllImport(Lib, EntryPoint = "eglGetPlatformDisplay")]
        public static extern IntPtr GetPlatformDisplay(int platform, IntPtr nativeDisplay, IntPtr attribs);
        [DllImport(Lib, EntryPoint = "eglInitialize")] public static extern uint Initialize(IntPtr dpy, out int major, out int minor);
        [DllImport(Lib, EntryPoint = "eglBindAPI")] public static extern uint BindAPI(uint api);
        [DllImport(Lib, EntryPoint = "eglChooseConfig")]
        public static extern uint ChooseConfig(IntPtr dpy, int[] attribs, out IntPtr config, int size, out int count);
        [DllImport(Lib, EntryPoint = "eglCreatePbufferSurface")]
        public static extern IntPtr CreatePbufferSurface(IntPtr dpy, IntPtr config, int[] attribs);
        [DllImport(Lib, EntryPoint = "eglCreateContext")]
        public static extern IntPtr CreateContext(IntPtr dpy, IntPtr config, IntPtr share, int[] attribs);
        [DllImport(Lib, EntryPoint = "eglMakeCurrent")]
        public static extern uint MakeCurrent(IntPtr dpy, IntPtr draw, IntPtr read, IntPtr ctx);
        [DllImport(Lib, EntryPoint = "eglSwapBuffers")] public static extern uint SwapBuffers(IntPtr dpy, IntPtr surface);
        [DllImport(Lib, EntryPoint = "eglGetError")] public static extern int GetError();
    }

    // GBM on the GPU render node, for --headless (the Vivante EGL has no
    // default display outside Wayland)
    internal static class Gbm
    {
        [DllImport("libgbm.so.1", EntryPoint = "gbm_create_device")] public static extern IntPtr CreateDevice(int fd);
        [DllImport("libc", EntryPoint = "open")] public static extern int Open(string path, int flags);
        public const int O_RDWR = 2, O_CLOEXEC = 0x80000;
    }

    internal static unsafe class Gl
    {
        private const string Lib = "libGLESv2.so.2";

        public const uint COLOR_BUFFER_BIT = 0x4000, DEPTH_BUFFER_BIT = 0x100;
        public const uint DEPTH_TEST = 0x0B71, CULL_FACE = 0x0B44, BLEND = 0x0BE2;
        public const uint LEQUAL = 0x0203;
        public const uint VERTEX_SHADER = 0x8B31, FRAGMENT_SHADER = 0x8B30;
        public const uint COMPILE_STATUS = 0x8B81, LINK_STATUS = 0x8B82;
        public const uint ARRAY_BUFFER = 0x8892, ELEMENT_ARRAY_BUFFER = 0x8893;
        public const uint STATIC_DRAW = 0x88E4, DYNAMIC_DRAW = 0x88E8;
        public const uint FLOAT = 0x1406, UNSIGNED_SHORT = 0x1403, UNSIGNED_BYTE = 0x1401;
        public const uint TRIANGLES = 0x0004;
        public const uint RGBA = 0x1908;
        public const uint VENDOR = 0x1F00, RENDERER = 0x1F01, VERSION = 0x1F02;
        public const uint FRAMEBUFFER = 0x8D40, RENDERBUFFER = 0x8D41, COLOR_ATTACHMENT0 = 0x8CE0,
            DEPTH_ATTACHMENT = 0x8D00, FRAMEBUFFER_COMPLETE = 0x8CD5, RGBA8_OES = 0x8058,
            DEPTH_COMPONENT16 = 0x81A5;
        // OpenGL ES 3.0: multisampled render target, resolved with a blit
        public const uint READ_FRAMEBUFFER = 0x8CA8, DRAW_FRAMEBUFFER = 0x8CA9, MAX_SAMPLES = 0x8D57,
            NEAREST = 0x2600;

        [DllImport(Lib, EntryPoint = "glClearColor")] public static extern void ClearColor(float r, float g, float b, float a);
        [DllImport(Lib, EntryPoint = "glClear")] public static extern void Clear(uint mask);
        [DllImport(Lib, EntryPoint = "glViewport")] public static extern void Viewport(int x, int y, int w, int h);
        [DllImport(Lib, EntryPoint = "glEnable")] public static extern void Enable(uint cap);
        [DllImport(Lib, EntryPoint = "glDisable")] public static extern void Disable(uint cap);
        [DllImport(Lib, EntryPoint = "glDepthFunc")] public static extern void DepthFunc(uint func);
        [DllImport(Lib, EntryPoint = "glCreateShader")] public static extern uint CreateShader(uint type);
        [DllImport(Lib, EntryPoint = "glShaderSource")]
        public static extern void ShaderSource(uint shader, int count,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] src, int* lengths);
        [DllImport(Lib, EntryPoint = "glCompileShader")] public static extern void CompileShader(uint shader);
        [DllImport(Lib, EntryPoint = "glGetShaderiv")] public static extern void GetShaderiv(uint shader, uint pname, out int value);
        [DllImport(Lib, EntryPoint = "glGetShaderInfoLog")]
        public static extern void GetShaderInfoLog(uint shader, int size, out int length, byte* log);
        [DllImport(Lib, EntryPoint = "glCreateProgram")] public static extern uint CreateProgram();
        [DllImport(Lib, EntryPoint = "glAttachShader")] public static extern void AttachShader(uint program, uint shader);
        [DllImport(Lib, EntryPoint = "glBindAttribLocation")] public static extern void BindAttribLocation(uint program, uint index, string name);
        [DllImport(Lib, EntryPoint = "glLinkProgram")] public static extern void LinkProgram(uint program);
        [DllImport(Lib, EntryPoint = "glGetProgramiv")] public static extern void GetProgramiv(uint program, uint pname, out int value);
        [DllImport(Lib, EntryPoint = "glGetProgramInfoLog")]
        public static extern void GetProgramInfoLog(uint program, int size, out int length, byte* log);
        [DllImport(Lib, EntryPoint = "glUseProgram")] public static extern void UseProgram(uint program);
        [DllImport(Lib, EntryPoint = "glGetUniformLocation")] public static extern int GetUniformLocation(uint program, string name);
        [DllImport(Lib, EntryPoint = "glUniformMatrix4fv")]
        public static extern void UniformMatrix4fv(int location, int count, byte transpose, float* value);
        [DllImport(Lib, EntryPoint = "glUniform3f")] public static extern void Uniform3f(int location, float x, float y, float z);
        [DllImport(Lib, EntryPoint = "glUniform2f")] public static extern void Uniform2f(int location, float x, float y);
        [DllImport(Lib, EntryPoint = "glGenBuffers")] public static extern void GenBuffers(int n, out uint buffer);
        [DllImport(Lib, EntryPoint = "glBindBuffer")] public static extern void BindBuffer(uint target, uint buffer);
        [DllImport(Lib, EntryPoint = "glBufferData")] public static extern void BufferData(uint target, IntPtr size, void* data, uint usage);
        [DllImport(Lib, EntryPoint = "glVertexAttribPointer")]
        public static extern void VertexAttribPointer(uint index, int size, uint type, byte normalized, int stride, IntPtr offset);
        [DllImport(Lib, EntryPoint = "glEnableVertexAttribArray")] public static extern void EnableVertexAttribArray(uint index);
        [DllImport(Lib, EntryPoint = "glDisableVertexAttribArray")] public static extern void DisableVertexAttribArray(uint index);
        [DllImport(Lib, EntryPoint = "glDrawElements")] public static extern void DrawElements(uint mode, int count, uint type, IntPtr offset);
        [DllImport(Lib, EntryPoint = "glDrawArrays")] public static extern void DrawArrays(uint mode, int first, int count);
        [DllImport(Lib, EntryPoint = "glGetString")] private static extern IntPtr GetStringPtr(uint name);
        public static string GetString(uint name) => Marshal.PtrToStringAnsi(GetStringPtr(name)) ?? "";
        [DllImport(Lib, EntryPoint = "glReadPixels")]
        public static extern void ReadPixels(int x, int y, int w, int h, uint format, uint type, void* data);
        [DllImport(Lib, EntryPoint = "glFinish")] public static extern void Finish();
        [DllImport(Lib, EntryPoint = "glFlush")] public static extern void Flush();
        [DllImport(Lib, EntryPoint = "glGenFramebuffers")] public static extern void GenFramebuffers(int n, out uint fb);
        [DllImport(Lib, EntryPoint = "glBindFramebuffer")] public static extern void BindFramebuffer(uint target, uint fb);
        [DllImport(Lib, EntryPoint = "glGenRenderbuffers")] public static extern void GenRenderbuffers(int n, out uint rb);
        [DllImport(Lib, EntryPoint = "glBindRenderbuffer")] public static extern void BindRenderbuffer(uint target, uint rb);
        [DllImport(Lib, EntryPoint = "glRenderbufferStorage")]
        public static extern void RenderbufferStorage(uint target, uint format, int w, int h);
        [DllImport(Lib, EntryPoint = "glFramebufferRenderbuffer")]
        public static extern void FramebufferRenderbuffer(uint target, uint attachment, uint rbTarget, uint rb);
        [DllImport(Lib, EntryPoint = "glCheckFramebufferStatus")] public static extern uint CheckFramebufferStatus(uint target);
        [DllImport(Lib, EntryPoint = "glDeleteFramebuffers")] public static extern void DeleteFramebuffers(int n, ref uint fb);
        [DllImport(Lib, EntryPoint = "glDeleteRenderbuffers")] public static extern void DeleteRenderbuffers(int n, ref uint rb);
        [DllImport(Lib, EntryPoint = "glGetIntegerv")] public static extern void GetIntegerv(uint pname, out int value);
        [DllImport(Lib, EntryPoint = "glRenderbufferStorageMultisample")]
        public static extern void RenderbufferStorageMultisample(uint target, int samples, uint format, int w, int h);
        [DllImport(Lib, EntryPoint = "glBlitFramebuffer")]
        public static extern void BlitFramebuffer(int sx0, int sy0, int sx1, int sy1, int dx0, int dy0, int dx1, int dy1, uint mask, uint filter);
    }
}
