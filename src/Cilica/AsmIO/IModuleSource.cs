namespace Cilica.AsmIO;

/// <summary>
/// Supplies the lazily loaded members of a module that was created over an external metadata reader,
/// see <see cref="ModuleResolver.Create(System.Reflection.AssemblyName, IModuleSource, string?)"/>.
/// </summary>
/// <remarks>
/// The module's whole type table, nested types and <c>&lt;Module&gt;</c> included, is defined up front by the caller with
/// <see cref="ModuleDef.DefineSourceType"/>; only the members of those types, and the IL bodies of their methods, are
/// requested from the source on first access. Results are cached by the type or method, and a null list is cached as
/// empty; but a null base type or body, or an exception (including one thrown by <see cref="LoadGenericParams"/> after it
/// published its array), is requested again on the next access, so sources should memoise those answers themselves.
/// Returned lists become owned (and may be mutated) by the type: return a fresh list per call. <see cref="LoadBody"/> may
/// also be called for methods the source did not produce (created later on a source type); return null for those.
/// Modules loaded from a
/// file by <see cref="ModuleResolver.Load(string)"/> never consult a source.
/// </remarks>
public interface IModuleSource
{
    TypeDefOrSpec? GetBaseType(TypeDef type);

    /// <summary>
    /// Loads the generic parameters of <paramref name="type"/>. Assign <paramref name="genericParams"/> <b>before</b>
    /// resolving their constraints: a constraint may refer back to <paramref name="type"/> (e.g. <c>T : IFoo&lt;T&gt;</c>),
    /// and the type's getter reads the published array to break that cycle.
    /// </summary>
    void LoadGenericParams(TypeDef type, out GenericParamType[] genericParams);

    List<FieldDef> LoadFields(TypeDef type);
    List<MethodDef> LoadMethods(TypeDef type);
    List<TypeDesc> LoadInterfaces(TypeDef type);

    /// <summary> Returns the IL body of <paramref name="method"/>, or null if it has none. </summary>
    ILMethodBody? LoadBody(MethodDef method);

    /// <summary> Finds a type this module forwards to another module, or null. </summary>
    TypeDef? FindExportedType(string? ns, string name) => null;

    Dictionary<MethodDesc, MethodDef> LoadMethodImpls(TypeDef type) => new();
    List<PropertyDef> LoadProperties(TypeDef type) => new();
    List<EventDef> LoadEvents(TypeDef type) => new();
}
