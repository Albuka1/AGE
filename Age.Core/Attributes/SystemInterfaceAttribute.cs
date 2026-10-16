namespace Age.Core;

/// <summary>
/// Marks an interface as one that a system implements, which is what lets a class be registered without an attribute of
/// its own.
/// </summary>
/// <remarks>
/// A system is usually a class that answers an interface of the engine, and the interface is what says what the class is
/// for. Marking the interface is therefore the one place that says "a class which implements me is a system", and a class
/// of a game that answers it is registered the moment it is compiled.
/// </remarks>
[AttributeUsage(AttributeTargets.Interface, Inherited = false)]
public sealed class SystemInterfaceAttribute : Attribute;
