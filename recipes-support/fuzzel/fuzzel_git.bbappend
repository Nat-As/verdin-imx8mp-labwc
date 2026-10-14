# Use fuzzel's built-in nanosvg instead of librsvg (avoids a cross Rust
# toolchain just for launcher icons).
PACKAGECONFIG:remove = "librsvg"
