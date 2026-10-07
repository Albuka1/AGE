# Sound fixtures

Two short real recordings that the sound tests decode. They are the first two seconds of two public
samples, cut at a page boundary (`sample.ogg`) and at a frame boundary (`sample.mp3`) so that each
file stays a valid stream:

- `sample.ogg` — [Example.ogg](https://commons.wikimedia.org/wiki/File:Example.ogg), Wikimedia Commons.
- `sample.mp3` — [SoundHelix Song 1](https://www.soundhelix.com/audio-examples), T. Schürger, SoundHelix,
  licensed under Creative Commons Attribution.

Both are decoded by the same `SoundLoader` that a game uses, and the tests assert the rate, the channel
count, the length and that the samples actually carry audio.
