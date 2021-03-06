#version 140

#include <Common/Lighting.fsh>

uniform vec4 uColor = vec4(1);
uniform float uScale = 20;
uniform float uScale2 = 5;
uniform float uSpeed = 1;
uniform float uSineOffset = -.4;
uniform float uXAlphaMin = .0;

in vec3 vObjPos;
in vec3 vEyePos;

in vec3 vRef1;
in vec3 vRef2;

OUTPUT_CHANNEL_Distortion(vec2)

void main()
{
    INIT_CHANNELS;

    vec3 worldPos = vec3(uInverseViewMatrix * vec4(vEyePos, 1.0));

    vec2 distort = vec2(0);

    float dis1 = distance(worldPos, vRef1);
    float dis2 = distance(worldPos, vRef2);
    //float dis1 = distance(worldPos.xz, vRef1.xz) + distance(worldPos.y, vRef1.y);
    //float dis2 = distance(worldPos.xz, vRef2.xz) + distance(worldPos.y, vRef2.y);

    const float phase = .982;
    float a1 = cos(dis1 * uScale * 2 + uRuntime * uSpeed + phase) + uSineOffset;
    float a2 = cos(dis2 * uScale * 2 + uRuntime * uSpeed + phase) + uSineOffset;

    distort += normalize(vec2(distance(worldPos.xz, vRef1.xz), distance(worldPos.y, vRef1.y))) * a1;
    distort += normalize(vec2(distance(worldPos.xz, vRef2.xz), distance(worldPos.y, vRef2.y))) * a2;

    float modX1 = max(uXAlphaMin, 1 - dis1 / uScale2);
    float modX2 = max(uXAlphaMin, 1 - dis2 / uScale2);
    distort *= max(modX1, modX2);

    //distort = vec2(cos(uRuntime), sin(uRuntime)) * 20;
    distort *= 20;

    OUTPUT_Distortion(distort);
}
