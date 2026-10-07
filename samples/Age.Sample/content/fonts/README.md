# Fonts

| File | Source | License |
| --- | --- | --- |
| `Cousine-Regular.ttf` | [Cousine](https://github.com/google/fonts/tree/main/ofl/cousine) by Steve Matteson, fetched from `google/fonts` | [SIL Open Font License 1.1](OFL.txt) |

The font is the one the engine tests bake a glyph atlas from, and the one the sample draws text with, so it has to
travel with the repository: the OFL allows that as long as the license text and the copyright notice stay with the
font, which is what `OFL.txt` next to it is for. The sample and the tests share this single copy, so there is nothing to
keep in step.
