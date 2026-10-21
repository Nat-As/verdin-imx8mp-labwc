#version 450
// Encodes the pixel position, so that the test can tell where each pixel
// ended up in memory. B8G8R8A8_UNORM stores (r, g, b, a) as the 32-bit word
// a << 24 | r << 16 | g << 8 | b: r = x & 255, g = y & 255,
// b = (x >> 8) & 15 | ((y >> 8) & 15) << 4.
layout(location = 0) out vec4 color;
void main() {
	ivec2 p = ivec2(gl_FragCoord.xy);
	color = vec4(float(p.x & 255) / 255.0, float(p.y & 255) / 255.0,
		float(((p.x >> 8) & 15) | (((p.y >> 8) & 15) << 4)) / 255.0, 1.0);
}
