# Build the tkinter module (python3-tkinter). Note: Tk is an X11 toolkit, so
# tkinter windows need an X server (Xwayland), which this image does not run.
PACKAGECONFIG:append:class-target = " tk"
