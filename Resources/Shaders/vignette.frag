// A vignette: the frame that this pass is drawn into is read back, darkened towards the edges of it and written out again,
// which is what a post-process of a game looks like. The fragment stage is written under the header of the engine, which is what
// gives it UV (the texture coordinate of the fragment, from zero to one across the quad) and COLOR (the colour it writes), and
// which names the surface that is being drawn into as SCREEN_TEXTURE with sampleScreen, a reader of it that takes the
// coordinate of the frame: the copy of the surface is read from its bottom row upwards, so that reader flips the vertical axis.
uniform float uVignetteStrength;
uniform vec3 uVignetteColour;

void main()
{
    // How far the fragment is from the middle of the frame: the middle is zero and the corners are about a half of the diagonal.
    float distance = length(UV - vec2(0.5));

    // Nothing in the middle, the full strength of the shader at the corners, and a smooth change between the two.
    float falloff = smoothstep(0.25, 0.75, distance) * uVignetteStrength;

    // What the frame holds at this point of it, with the colour of the vignette mixed into the edges of it. A shader that reads
    // the surface reads the frame as it was before this draw, so what it writes back is the picture it took.
    vec3 frame = sampleScreen(UV).rgb;

    COLOR = vec4(mix(frame, uVignetteColour, falloff), 1.0);
}

