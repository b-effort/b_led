#version 330

layout(location=0) in vec4 color;
layout(location=1) in vec2 coord;

uniform vec2 bounds;

out vec4 ledColor;
out vec2 ledCoord;

void main() {
//    ledColor = color;
    ledColor = vec4(0.5, 0, 0.5, 1);
    ledCoord = coord / bounds;

    gl_Position = vec4(ledCoord.x, ledCoord.y, 0, 1);
}
