# Display drivers built in, fbcon deferred takeover: see display-builtin.cfg
FILESEXTRAPATHS:prepend := "${THISDIR}/files:"
SRC_URI += "file://display-builtin.cfg"
