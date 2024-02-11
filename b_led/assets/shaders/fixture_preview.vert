#version 330

layout(location=0) in vec4 color;
layout(location=1) in vec2 coord;

uniform vec2 bounds;

out vec4 ledColor;
out vec2 ledCoord;

void main() {
    ledColor = color;

    ledCoord = coord / bounds;
    ledCoord = (ledCoord - 0.5) * 1.5;

    gl_Position = vec4(ledCoord.xy, 0, 1);
    gl_PointSize = 1;
}
