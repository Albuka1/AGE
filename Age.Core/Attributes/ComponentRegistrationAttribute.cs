namespace Age.Core;

/// <summary>
/// Marks a class as the set of component registrations of one assembly, which is what lets the container find them
/// without a list.
/// </summary>
/// <remarks>
/// The class has to implement <see cref="IComponentRegistrations"/>, and the attribute is what a scan reads. A
/// registration is written once, next to the components it registers, and the assembly that holds it is registered with
/// one call rather than a line per class.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ComponentRegistrationAttribute : Attribute;
