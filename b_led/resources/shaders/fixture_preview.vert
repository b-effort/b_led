#version 330
#extension GL_ARB_explicit_uniform_location : enable

layout(location=0) in vec4 color;
layout(location=1) in vec2 coord;

layout(location=0) uniform mat4 projection;

out vec4 ledColor;
out vec2 ledCoord;

void main() {
    ledColor = color;

    ledCoord = coord;
    gl_Position = vec4(ledCoord, 0, 1) * projection;
}
