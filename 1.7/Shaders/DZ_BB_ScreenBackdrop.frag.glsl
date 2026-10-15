#version 450

// Map edge clipper shader that ignores the quad's own UVs and samples _MainTex by screen
// position instead, so the picture stays fixed to the screen while the camera moves.
// The clipper quads only decide where the picture shows through (outside the map).
// The texture is scaled to cover the whole screen, keeping its aspect ratio (excess is cropped).
// _ColorTwo is an optional tint: rgb is the hue to shift toward, alpha how far (0 = untouched).
// The tint keeps each pixel's brightness, so it recolors without darkening the picture.

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;        // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _ColorTwo;     // @color @default = vec4(1.0, 1.0, 1.0, 0.0)
    vec4 _ScreenParams; // global, published by MapRenderFeature
} uMaterial;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

void main()
{
    vec2 screen = max(uMaterial._ScreenParams.xy, vec2(1.0));
    vec2 tex = vec2(textureSize(_MainTex, 0));

    float screenAspect = screen.x / screen.y;
    float texAspect = tex.x / tex.y;
    vec2 scale = screenAspect > texAspect
        ? vec2(1.0, texAspect / screenAspect)   // screen is wider: crop top and bottom
        : vec2(screenAspect / texAspect, 1.0);  // screen is taller: crop the sides

    vec2 uv = gl_FragCoord.xy / screen;
    uv = (uv - 0.5) * scale + 0.5;

    vec3 c = texture(_MainTex, uv).rgb;

    const vec3 lumWeights = vec3(0.299, 0.587, 0.114);
    vec3 tint = uMaterial._ColorTwo.rgb;
    float tintLum = max(dot(tint, lumWeights), 0.001);
    vec3 tinted = min(dot(c, lumWeights) * tint / tintLum, vec3(1.0));
    c = mix(c, tinted, uMaterial._ColorTwo.a);

    outColor = vec4(c * uMaterial._Color.rgb, 1.0);
}
