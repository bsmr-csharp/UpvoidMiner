#version 140

#include <Common/Camera.csh>

uniform mat4 uModelMatrix;
uniform float uRepX;

in vec3 aPosition;
in vec3 aNormal;

out vec3 vObjPos;
out vec3 vEyePos;
out vec4 vScreenPos;
out vec3 vRef1;
out vec3 vRef2;
out float vScaleX;

void main()
{
    // world space position:
    vObjPos = aPosition;
    vEyePos = vec3(uViewMatrix * uModelMatrix * vec4(aPosition, 1.0));

    vRef1 = vec3(uModelMatrix * vec4(-1, 0, 0, 1));
    vRef2 = vec3(uModelMatrix * vec4(1, 0, 0, 1));
    float vLength = distance(vRef1, vRef2);

    float d = vLength / uRepX;
    d = d - fract(d);

    vScaleX = (d * uRepX) / vLength;

    // projected vertex position used for the interpolation
    vec4 eyePos = vec4(vEyePos, 1.0);
    eyePos.z += .02; // Workaround for z-Fighting with terrain.
    vec4 screenPos  = uProjectionMatrix * eyePos;
    vScreenPos = screenPos;
    gl_Position = screenPos;
}
