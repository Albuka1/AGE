using Age.Assets;
using Age.Content.Sheets;
using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>The sheet the repository ships, read the way a game reads it, which is what proves its image is one the loader decodes.</summary>
public sealed class ShippedSpriteSheetTests
{
    [Fact]
    public void ShippedSpriteSheet_TheGoblin_IsADocumentAndAnImageAGameCanRead()
    {
        var assets = new NullAssetLoader();
        assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));

        SpriteSheet sheet = SpriteSheetReader.Read(
            assets.Load<string>("Textures/Entities/goblin.yml"),
            "Textures/Entities/goblin.yml");

        ImageData image = new StbImageLoader(assets).Load(sheet.Image);

        sheet.Cell.Should().Be(new Vector2(16f, 16f));
        sheet.States.Keys.Should().Equal("idle", "walk", "attack");
        image.Width.Should().Be(64, "the grid of the sheet is four cells of sixteen pixels across");
        image.Height.Should().Be(32, "and two rows of sixteen pixels down");
        image.Pixels.Length.Should().Be(64 * 32 * 4);

        Rect last = sheet.Region(sheet.States["attack"], frame: 2);

        last.X.Should().Be(0.5f);
        last.Y.Should().Be(0.5f);
        last.Width.Should().Be(0.25f);
        last.Height.Should().Be(0.5f);
    }
}
