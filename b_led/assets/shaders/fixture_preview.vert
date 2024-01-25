#version 330

layout (location = 0) in vec4 color;
layout (location = 1) in vec2 coords;

out vec2 ledCoord;
out vec4 ledColor;

void main() {
    gl_Position = vec4(ledCoord.x, ledCoord.y, 0, 1);
}
