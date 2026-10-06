namespace Age.Core;

/// <summary>
/// Defines a unit of behaviour that a <see cref="SystemPipeline"/> runs once per frame.
/// </summary>
public interface ISystem
{
    /// <summary>Advances the system by one step.</summary>
    void Update(World world, in GameTime time);
}
