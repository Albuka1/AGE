# A vignette: the edges of the quad are darkened and its middle is left alone, which is what a game draws over a frame after
# the world and the interface of it. The fragment stage is written under the header of the engine, which is what gives it UV
# (the texture coordinate of the fragment, from zero to one across the quad) and COLOR (the colour it writes). A solid quad
# shows the whole of the white pixel it samples, so UV runs from one corner of the frame to the other.
uniform float uVignetteStrength;

void main()
{
    // How far the fragment is from the middle of the quad: the middle is zero and the corners are about a half of the diagonal.
    float distance = length(UV - vec2(0.5));

    // Nothing in the middle, the strength of the shader at the corners, and a smooth change between the two.
    COLOR = vec4(0.0, 0.0, 0.0, smoothstep(0.25, 0.75, distance) * uVignetteStrength);
}
