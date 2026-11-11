namespace Cilica.Tests.AsmIO;

using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Cilica.AsmIO;

// A member signature that cannot be resolved leaves the lazy Methods/Fields property unset, so the next
// access, or a lookup by token, loads the type's members again. That load must start clean and fail
// with the same cause, rather than meet the members cached by the failed attempt.
public class ModuleLoaderTests : IDisposable
{
    public enum Dependency { Missing, LacksType }

    const string DependencyName = "MissingDependency";
    const int CompleteRow = 1, BrokenRow = 3, ImplementerRow = 5; // first field and method row of each fixture type

    // Debug builds assert "Trying to replace already existing entity" on a stale cache entry.
    readonly AssertTrap _assertTrap = new();
    readonly TemporaryDirectory _directory = new();
    readonly ModuleResolver _resolver = new();

    public ModuleLoaderTests()
    {
        _resolver.AddTrustedSearchPaths();
    }

    public void Dispose()
    {
        _resolver.Dispose();
        _directory.Dispose();
        _assertTrap.Dispose();
    }

    [Theory]
    [InlineData(Dependency.Missing)]
    [InlineData(Dependency.LacksType)]
    public void Methods_WhenALaterSignatureIsUnresolvable_FailWithTheSameCauseOnEveryAccess(Dependency dependency)
    {
        var type = LoadFixture(dependency).FindType("Test", "Broken")!;

        var first = AssertUnresolvable(dependency, () => type.Methods);
        var second = AssertUnresolvable(dependency, () => type.Methods);
        Assert.Equal(first.Message, second.Message);
    }

    [Theory]
    [InlineData(Dependency.Missing)]
    [InlineData(Dependency.LacksType)]
    public void Fields_WhenALaterSignatureIsUnresolvable_FailWithTheSameCauseOnEveryAccess(Dependency dependency)
    {
        var type = LoadFixture(dependency).FindType("Test", "Broken")!;

        var first = AssertUnresolvable(dependency, () => type.Fields);
        var second = AssertUnresolvable(dependency, () => type.Fields);
        Assert.Equal(first.Message, second.Message);
    }

    [Fact]
    public void MethodByToken_AfterAFailedLoad_FailsInsteadOfReturningALeftoverInstance()
    {
        var module = LoadFixture(Dependency.Missing);
        var type = module.FindType("Test", "Broken")!;

        AssertUnresolvable(Dependency.Missing, () => type.Methods);
        AssertUnresolvable(Dependency.Missing, () => MethodByToken(module, BrokenRow));
        AssertUnresolvable(Dependency.Missing, () => type.Methods);
    }

    [Fact]
    public void FieldByToken_AfterAFailedLoad_FailsInsteadOfReturningALeftoverInstance()
    {
        var module = LoadFixture(Dependency.Missing);
        var type = module.FindType("Test", "Broken")!;

        AssertUnresolvable(Dependency.Missing, () => type.Fields);
        AssertUnresolvable(Dependency.Missing, () => FieldByToken(module, BrokenRow));
        AssertUnresolvable(Dependency.Missing, () => type.Fields);
    }

    [Fact]
    public void Methods_WhenTheDependencyBecomesResolvable_LoadFullyAndMatchTheInstancesByToken()
    {
        var module = LoadFixture(Dependency.Missing);
        var type = module.FindType("Test", "Broken")!;
        AssertUnresolvable(Dependency.Missing, () => type.Methods);
        AssertUnresolvable(Dependency.Missing, () => MethodByToken(module, BrokenRow));

        AddDependency(withType: true);

        var methods = type.Methods;
        Assert.Equal(["First", "Second"], methods.Select(m => m.Name));
        Assert.Same(methods[0], MethodByToken(module, BrokenRow));
        Assert.Same(methods[1], MethodByToken(module, BrokenRow + 1));
        Assert.Same(methods, type.Methods);
    }

    [Fact]
    public void Fields_WhenTheDependencyBecomesResolvable_LoadFullyAndMatchTheInstancesByToken()
    {
        var module = LoadFixture(Dependency.Missing);
        var type = module.FindType("Test", "Broken")!;
        AssertUnresolvable(Dependency.Missing, () => type.Fields);
        AssertUnresolvable(Dependency.Missing, () => FieldByToken(module, BrokenRow));

        AddDependency(withType: true);

        var fields = type.Fields;
        Assert.Equal(["First", "Second"], fields.Select(f => f.Name));
        Assert.Equal("Type", fields[1].Type.Name);
        Assert.Same(fields[0], FieldByToken(module, BrokenRow));
        Assert.Same(fields[1], FieldByToken(module, BrokenRow + 1));
        Assert.Same(fields, type.Fields);
    }

    [Fact]
    public void Methods_WhenAllSignaturesResolve_AreTheSameByTokenAndOnEveryAccess()
    {
        var module = LoadFixture(Dependency.Missing);
        var type = module.FindType("Test", "Complete")!;

        var second = MethodByToken(module, CompleteRow + 1); // loads the type's methods
        var methods = type.Methods;

        Assert.Equal(["First", "Second"], methods.Select(m => m.Name));
        Assert.Same(methods[0], MethodByToken(module, CompleteRow));
        Assert.Same(methods[1], second);
        Assert.Same(methods, type.Methods);
    }

    [Fact]
    public void Fields_WhenAllSignaturesResolve_AreTheSameByTokenAndOnEveryAccess()
    {
        var module = LoadFixture(Dependency.Missing);
        var type = module.FindType("Test", "Complete")!;

        var second = FieldByToken(module, CompleteRow + 1); // loads the type's fields
        var fields = type.Fields;

        Assert.Equal(["First", "Second"], fields.Select(f => f.Name));
        Assert.Same(fields[0], FieldByToken(module, CompleteRow));
        Assert.Same(fields[1], second);
        Assert.Same(fields, type.Fields);
    }

    [Fact]
    public void Interfaces_WhenALaterInterfaceIsUnresolvable_FailWithTheSameCauseOnEveryAccess()
    {
        var type = LoadFixture(Dependency.Missing).FindType("Test", "Implementer")!;

        AssertUnresolvable(Dependency.Missing, () => type.Interfaces);
        AssertUnresolvable(Dependency.Missing, () => type.Interfaces);
    }

    [Fact]
    public void Interfaces_WhenTheDependencyBecomesResolvable_LoadFullyWithTheirAttributes()
    {
        var type = LoadFixture(Dependency.Missing).FindType("Test", "Implementer")!;
        AssertUnresolvable(Dependency.Missing, () => type.Interfaces);

        AddDependency(withType: true);

        var interfaces = type.Interfaces;
        Assert.Equal(["IDisposable", "Type"], interfaces.Select(itf => itf.Name));
        Assert.All(interfaces, itf => Assert.Equal("ObsoleteAttribute", Assert.Single(type.GetCustomAttribs(itf)).Type.Name));
        Assert.Same(interfaces, type.Interfaces);
    }

    [Fact]
    public void MethodImpls_WhenALaterDeclarationIsUnresolvable_FailWithTheSameCauseOnEveryAccess()
    {
        var type = LoadFixture(Dependency.Missing).FindType("Test", "Implementer")!;

        AssertUnresolvable(Dependency.Missing, () => type.MethodImpls);
        AssertUnresolvable(Dependency.Missing, () => type.MethodImpls);
    }

    [Fact]
    public void MethodImpls_WhenTheDependencyBecomesResolvable_LoadFully()
    {
        var module = LoadFixture(Dependency.Missing);
        var type = module.FindType("Test", "Implementer")!;
        AssertUnresolvable(Dependency.Missing, () => type.MethodImpls);

        AddDependency(withType: true);

        var impls = type.MethodImpls;
        Assert.Equal(["Dispose", "Run"], impls.Keys.Select(decl => decl.Name).Order());
        Assert.Equal("IDisposable", impls.Keys.Single(decl => decl.Name == "Dispose").DeclaringType.Name);
        Assert.All(impls.Values, body => Assert.Same(MethodByToken(module, ImplementerRow), body));
        Assert.Same(impls, type.MethodImpls);
    }

    [Fact]
    public void Fields_WhenAnOverlappingTypeFailsToLoad_KeepTheEntryOfTheTypeThatLoaded()
    {
        var module = LoadOverlapFixture();
        var shared = Assert.Single(module.FindType("Test", "Owner")!.Fields);

        // Debug builds assert on replacing the shared entry, before the unresolvable field is reached, and the
        // assert stops the write; so this test discriminates only in Release builds, where the shared entry is
        // overwritten and the rollback must restore it (it fails there against a rollback that only clears new
        // entries). A green Debug run is not coverage of the restore.
        var ex = Record.Exception(() => module.FindType("Test", "Overlapping")!.Fields);
        Assert.True(
            ex is FileNotFoundException || ex is DebugAssertException d && d.Message.Contains("Trying to replace already existing entity"),
            $"Unexpected exception: {ex}");
        Assert.Same(shared, FieldByToken(module, 1));
    }

    static Exception AssertUnresolvable(Dependency dependency, Func<object> load)
    {
        var ex = dependency == Dependency.Missing
            ? (Exception)Assert.Throws<FileNotFoundException>(load)
            : Assert.Throws<InvalidOperationException>(load);
        Assert.Contains(dependency == Dependency.Missing ? DependencyName : "Missing.Type", ex.Message);
        return ex;
    }

    static MethodDef MethodByToken(ModuleDef module, int row)
        => module._loader!.GetMethod(MetadataTokens.MethodDefinitionHandle(row));

    static FieldDef FieldByToken(ModuleDef module, int row)
        => (FieldDef)module._loader!.GetEntity(MetadataTokens.FieldDefinitionHandle(row));

    ModuleDef LoadFixture(Dependency dependency)
    {
        string path = Path.Combine(_directory.Path, "LoaderFixture.dll");
        Utils.WriteMetadataModule(path, WriteFixtureTypes);

        if (dependency == Dependency.LacksType) {
            AddDependency(withType: false);
        }
        return _resolver.Load(path);
    }

    // Hostile member ranges: Test.Owner has field 1, and Test.Overlapping, the last type, has fields 1-2
    // because its FieldList goes back to row 1 (Test.Spacer between them has none).
    ModuleDef LoadOverlapFixture()
    {
        string path = Path.Combine(_directory.Path, "OverlapFixture.dll");

        Utils.WriteMetadataModule(path, metadata => {
            var dependency = metadata.AddAssemblyReference(metadata.GetOrAddString(DependencyName), new Version(1, 0), default, default, 0, default);
            var missing = metadata.AddTypeReference(dependency, metadata.GetOrAddString("Missing"), metadata.GetOrAddString("Type"));

            foreach (var (name, fieldList) in new[] { ("Owner", 1), ("Spacer", 2), ("Overlapping", 1) }) {
                metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Test"), metadata.GetOrAddString(name), default,
                    MetadataTokens.FieldDefinitionHandle(fieldList), MetadataTokens.MethodDefinitionHandle(1));
            }
            foreach (var (name, type) in new (string, Action<SignatureTypeEncoder>)[] { ("Shared", t => t.Int32()), ("Fails", t => t.Type(missing, isValueType: false)) }) {
                var sig = new BlobBuilder();
                type(new BlobEncoder(sig).FieldSignature());
                metadata.AddFieldDefinition(FieldAttributes.Public | FieldAttributes.Static, metadata.GetOrAddString(name), metadata.GetOrAddBlob(sig));
            }
        });
        return _resolver.Load(path);
    }

    void AddDependency(bool withType)
    {
        string directory = Path.Combine(_directory.Path, withType ? "complete" : "incomplete");
        Directory.CreateDirectory(directory);

        Utils.WriteMetadataModule(Path.Combine(directory, DependencyName + ".dll"), metadata => {
            if (withType) {
                metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Missing"), metadata.GetOrAddString("Type"), default,
                    MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
            }
        });
        _resolver.AddSearchPaths([directory]);
    }

    // Test.Complete    { static int32 First; static int32 Second;        static void First(int32); static void Second(int32); }
    // Test.Broken      { static int32 First; static Missing.Type Second; static void First(int32); static void Second(Missing.Type); }
    // Test.Implementer : [Obsolete] IDisposable, [Obsolete] Missing.Type {
    //     void M() { .override IDisposable::Dispose  .override Missing.Type::Run }
    // }
    static void WriteFixtureTypes(MetadataBuilder metadata)
    {
        var runtime = metadata.AddAssemblyReference(metadata.GetOrAddString("System.Runtime"), new Version(10, 0), default, default, 0, default);
        var dependency = metadata.AddAssemblyReference(metadata.GetOrAddString(DependencyName), new Version(1, 0), default, default, 0, default);
        // InterfaceImpl rows are sorted by interface token, so the resolvable interface must have the lower TypeRef row.
        var disposable = metadata.AddTypeReference(runtime, metadata.GetOrAddString("System"), metadata.GetOrAddString("IDisposable"));
        var missing = metadata.AddTypeReference(dependency, metadata.GetOrAddString("Missing"), metadata.GetOrAddString("Type"));
        var obsolete = metadata.AddTypeReference(runtime, metadata.GetOrAddString("System"), metadata.GetOrAddString("ObsoleteAttribute"));

        var instanceVoid = new BlobBuilder();
        new BlobEncoder(instanceVoid).MethodSignature(isInstanceMethod: true).Parameters(0, ret => ret.Void(), pars => { });
        var instanceVoidSig = metadata.GetOrAddBlob(instanceVoid);
        var attribValue = metadata.GetOrAddBlob(new byte[] { 1, 0, 0, 0 }); // prolog, no named arguments
        var obsoleteCtor = metadata.AddMemberReference(obsolete, metadata.GetOrAddString(".ctor"), instanceVoidSig);

        AddType("Complete", CompleteRow, t => t.Int32());
        AddType("Broken", BrokenRow, t => t.Type(missing, isValueType: false));
        var implementer = AddTypeDef("Implementer", ImplementerRow);

        foreach (var itf in new[] { disposable, missing }) {
            metadata.AddCustomAttribute(metadata.AddInterfaceImplementation(implementer, itf), obsoleteCtor, attribValue);
        }
        var method = metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.HideBySig,
            MethodImplAttributes.IL, metadata.GetOrAddString("M"), instanceVoidSig, -1, MetadataTokens.ParameterHandle(1));
        // MethodImpl rows are sorted by class only, so rows of one class keep this order.
        metadata.AddMethodImplementation(implementer, method, metadata.AddMemberReference(disposable, metadata.GetOrAddString("Dispose"), instanceVoidSig));
        metadata.AddMethodImplementation(implementer, method, metadata.AddMemberReference(missing, metadata.GetOrAddString("Run"), instanceVoidSig));

        void AddType(string name, int firstRow, Action<SignatureTypeEncoder> secondType)
        {
            AddTypeDef(name, firstRow);
            AddMembers("First", t => t.Int32());
            AddMembers("Second", secondType);
        }
        TypeDefinitionHandle AddTypeDef(string name, int firstRow)
            => metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Test"), metadata.GetOrAddString(name), default,
                MetadataTokens.FieldDefinitionHandle(firstRow), MetadataTokens.MethodDefinitionHandle(firstRow));

        void AddMembers(string name, Action<SignatureTypeEncoder> type)
        {
            var fieldSig = new BlobBuilder();
            type(new BlobEncoder(fieldSig).FieldSignature());
            metadata.AddFieldDefinition(FieldAttributes.Public | FieldAttributes.Static, metadata.GetOrAddString(name), metadata.GetOrAddBlob(fieldSig));

            var methodSig = new BlobBuilder();
            new BlobEncoder(methodSig).MethodSignature().Parameters(1, ret => ret.Void(), pars => type(pars.AddParameter().Type()));
            metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.IL,
                metadata.GetOrAddString(name), metadata.GetOrAddBlob(methodSig), -1, MetadataTokens.ParameterHandle(1));
        }
    }

    /// <summary> Turns a failed <c>Debug.Assert</c> on the creating thread into an exception, instead of a FailFast of the test host. </summary>
    sealed class AssertTrap : TraceListener
    {
        readonly int _threadId = Environment.CurrentManagedThreadId;

        public AssertTrap() => Trace.Listeners.Insert(0, this);

        public override void Fail(string? message, string? detailMessage)
        {
            if (Environment.CurrentManagedThreadId == _threadId) {
                throw new DebugAssertException(message);
            }
        }
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { }

        protected override void Dispose(bool disposing)
        {
            Trace.Listeners.Remove(this);
            base.Dispose(disposing);
        }
    }
    sealed class DebugAssertException(string? message) : Exception("Debug.Assert failed: " + message);
}
