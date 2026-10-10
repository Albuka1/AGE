// A layer that breathes: the image of the layer, brighter than the layer below it by an amount that follows the time of the
// frame. The fragment stage is written under the header of the engine, which gives it UV (the texture coordinate of the fragment),
// TEXTURE (the image of the quad) and TIME (the seconds since the renderer was created), and which takes COLOR (the colour the
// stage writes). sampleTexture reads the image of the quad with a coordinate, which is what the layer of a sprite does.
void main()
{
    // The image of the layer as the artist drew it. COLOR is the colour that is written and cannot be read before it is, so a
    // stage that shades an image starts from the image rather than from the tint of the sprite.
    vec4 image = sampleTexture(UV);

    // A pulse between nothing and the whole of the brightness of the layer again, a little over half a cycle a second: the alpha
    // swings from zero, where nothing of the layer shows, to one, where the layer below shows through it, and the image is
    // multiplied by one to two of its own brightness on the way. TIME is in seconds, so TIME * 4.0 is radians and a full turn
    // takes about 1.6 seconds. The two layers together are the picture that the sprite draws.
    float pulse = 0.5 + (0.5 * sin(TIME * 4.0));

    COLOR = vec4(image.rgb * (1.0 + pulse), image.a * pulse);
}
