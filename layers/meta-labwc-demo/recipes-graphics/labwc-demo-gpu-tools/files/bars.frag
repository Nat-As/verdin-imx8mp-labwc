#version 450
// Four vertical colour bars (red, green, blue, white) for the on-display test
layout(push_constant) uniform PC { float width; } pc;
layout(location = 0) out vec4 color;
void main() {
	float q = gl_FragCoord.x / pc.width;
	color = q < 0.25 ? vec4(1, 0, 0, 1) : q < 0.5 ? vec4(0, 1, 0, 1) :
		q < 0.75 ? vec4(0, 0, 1, 1) : vec4(1, 1, 1, 1);
}
