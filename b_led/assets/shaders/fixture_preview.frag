#version 330

in vec2 ledCoord;
in vec4 ledColor;

out vec4 finalColor;

void main() {
    finalColor = ledColor;
}
