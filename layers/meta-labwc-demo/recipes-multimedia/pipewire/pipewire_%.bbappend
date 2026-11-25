# The GStreamer plugin (pipewiresrc, package gstreamer1.0-pipewire) is how
# RustDesk reads the screen capture stream of the portal (argos-remote).
# meta-freescale removes the "gstreamer" option for i.MX 8
# (PACKAGECONFIG:remove:mx8-nxp-bsp), and a :remove cannot be undone, so the
# option is enabled under another name; the original one is neutralised so
# that it does not pass -Dgstreamer=disabled.
PACKAGECONFIG[gstreamer] = ""
PACKAGECONFIG[gst-plugin] = "-Dgstreamer=enabled,-Dgstreamer=disabled,glib-2.0 gstreamer1.0 gstreamer1.0-plugins-base,,gstreamer1.0-pipewire"
PACKAGECONFIG:append = " gst-plugin"
