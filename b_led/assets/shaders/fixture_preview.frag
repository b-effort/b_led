#version 330

in vec4 ledColor;
in vec2 ledCoord;

layout(location=0) out vec4 finalColor;

void main() {
//    finalColor = ledColor;
    finalColor = vec4(0.5, 0, 0.5, 1);
}
