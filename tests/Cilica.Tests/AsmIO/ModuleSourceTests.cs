namespace Cilica.Tests.AsmIO;

using System.Reflection;
using Cilica.AsmIO;
using Cilica.Frontend;
using Cilica.IR;
using Cilica.IR.Utils;

public class ModuleSourceTests
{
    [Fact]
    public void CreateOverASourceDefinesNoTypesAndResolvesNothing()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var name = new AssemblyName("Empty") { Version = new(1, 2, 3, 4) };
        var module = resolver.Create(name, source);

        Assert.Empty(resolver.Requests);
        Assert.Empty(module.TypeDefs);
        Assert.Empty(module.ExportedTypes);
        Assert.Empty(source.Calls);
        Assert.Same(name, module.AsmName);
        Assert.Equal("Empty.dll", module.ModName);
        Assert.Same(module, resolver.Resolve("Empty"));
        Assert.Empty(resolver.Requests);
    }

    [Fact]
    public void CreateOverASourceUsesTheGivenModuleName()
    {
        using var resolver = new SourceResolver();
        var module = resolver.Create(new AssemblyName("Named"), new FakeSource(), "Named.netmodule");

        Assert.Equal("Named.netmodule", module.ModName);
    }

    [Fact]
    public void CreateOverASourceRequiresAnAssemblyName()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();

        Assert.Throws<InvalidOperationException>(() => resolver.Create(new AssemblyName(), source));
        resolver.Dispose();
        Assert.Throws<ObjectDisposedException>(() => resolver.Create(new AssemblyName("Late"), source));
        Assert.Equal(0, source.DisposeCount);
    }

    [Fact]
    public void FailedCreateLeavesTheSourceWithTheCaller()
    {
        var resolver = new SourceResolver();
        var first = new FakeSource();
        var second = new FakeSource();
        var module = resolver.Create(new AssemblyName("Twice"), first);

        Assert.Throws<ArgumentException>(() => resolver.Create(new AssemblyName("Twice"), second));
        Assert.Same(module, resolver.Resolve("Twice"));

        resolver.Dispose();
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(0, second.DisposeCount);
    }

    [Fact]
    public void ResolveImplCanCreateTheCoreLibOverASource()
    {
        using var resolver = new SourceResolver();
        var other = resolver.Create(new AssemblyName("Other"), new FakeSource());
        var foreign = other.DefineSourceType("System", "Object", TypeAttributes.Public);

        var coreLib = resolver.CoreLib;

        Assert.Equal("System.Private.CoreLib", Assert.Single(resolver.Requests));
        Assert.NotNull(resolver.CoreLibSource);
        Assert.Same(coreLib, resolver.CoreLib);
        Assert.Same(coreLib, resolver.Resolve("System.Private.CoreLib"));
        Assert.Single(resolver.Requests);

        var obj = coreLib.FindType("System", "Object", throwIfNotFound: true);
        Assert.True(obj.IsCorelibType());
        Assert.True(obj.IsCorelibType(typeof(object)));
        Assert.Same(obj, resolver.SysTypes.Object);
        Assert.True(PrimType.Int32.Equals(resolver.SysTypes.Int32));
        Assert.False(foreign.IsCorelibType());
    }

    [Theory]
    [InlineData(nameof(TypeDef.BaseType), nameof(IModuleSource.GetBaseType))]
    [InlineData(nameof(TypeDef.GenericParams), nameof(IModuleSource.LoadGenericParams))]
    [InlineData(nameof(TypeDef.Fields), nameof(IModuleSource.LoadFields))]
    [InlineData(nameof(TypeDef.Methods), nameof(IModuleSource.LoadMethods))]
    [InlineData(nameof(TypeDef.Interfaces), nameof(IModuleSource.LoadInterfaces))]
    [InlineData(nameof(TypeDef.MethodImpls), nameof(IModuleSource.LoadMethodImpls))]
    [InlineData(nameof(TypeDef.Properties), nameof(IModuleSource.LoadProperties))]
    [InlineData(nameof(TypeDef.Events), nameof(IModuleSource.LoadEvents))]
    public void LazyMembersOfSourceTypesAreRequestedOnce(string member, string sourceMember)
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Lazy"), source);
        var baseType = module.DefineSourceType("N", "Base", TypeAttributes.Public);
        var type = module.DefineSourceType("N", "Lazy", TypeAttributes.Public);
        MethodDef? method = null;
        source.OnGetBaseType = _ => baseType;
        source.OnLoadGenericParams = (TypeDef _, out GenericParamType[] pars) => pars = [new GenericParamType(0, isMethodParam: false, "T")];
        source.OnLoadMethods = t => [method = new MethodDef(t, PrimType.Void, [], "Run", MethodAttributes.Public | MethodAttributes.Static)];
        Func<TypeDef, object?> get = member switch {
            nameof(TypeDef.BaseType) => t => t.BaseType,
            nameof(TypeDef.GenericParams) => t => t.GenericParams,
            nameof(TypeDef.Fields) => t => t.Fields,
            nameof(TypeDef.Methods) => t => t.Methods,
            nameof(TypeDef.Interfaces) => t => t.Interfaces,
            nameof(TypeDef.MethodImpls) => t => t.MethodImpls,
            nameof(TypeDef.Properties) => t => t.Properties,
            nameof(TypeDef.Events) => t => t.Events,
            _ => throw new ArgumentOutOfRangeException(nameof(member)),
        };

        var first = get(type);
        var second = get(type);

        var call = Assert.Single(source.Calls);
        Assert.Equal(sourceMember, call.Member);
        Assert.Same(type, call.Target);
        Assert.NotNull(first);
        Assert.Same(first, second);
        if (member == nameof(TypeDef.Methods)) {
            Assert.Same(method, Assert.Single(type.Methods));
        } else {
            Assert.Same(call.Result, first);
        }
    }

    [Fact]
    public void NullMemberListsFromTheSourceBecomeEmpty()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource {
            OnLoadGenericParams = (TypeDef _, out GenericParamType[] pars) => pars = null!,
            OnLoadFields = _ => null!,
            OnLoadMethods = _ => null!,
            OnLoadInterfaces = _ => null!,
            OnLoadMethodImpls = _ => null!,
            OnLoadProperties = _ => null!,
            OnLoadEvents = _ => null!,
        };
        var module = resolver.Create(new AssemblyName("Nulls"), source);
        var type = module.DefineSourceType("N", "Nulls", TypeAttributes.Public);

        Assert.Empty(type.GenericParams);
        Assert.Empty(type.Fields);
        Assert.Empty(type.Methods);
        Assert.Empty(type.Interfaces);
        Assert.Empty(type.MethodImpls);
        Assert.Empty(type.Properties);
        Assert.Empty(type.Events);
    }

    [Fact]
    public void NullBaseTypeIsRequestedAgain()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Bases"), source);
        var baseType = module.DefineSourceType("N", "Base", TypeAttributes.Public);
        var type = module.DefineSourceType("N", "Derived", TypeAttributes.Public);

        Assert.Null(type.BaseType);
        Assert.Null(type.BaseType);
        Assert.Equal(2, source.Count(nameof(IModuleSource.GetBaseType)));

        source.OnGetBaseType = _ => baseType;
        Assert.Same(baseType, type.BaseType);
        Assert.Same(baseType, type.BaseType);
        Assert.Equal(3, source.Count(nameof(IModuleSource.GetBaseType)));
    }

    [Fact]
    public void SourceExceptionsAreNotCached()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Throwing"), source);
        var type = module.DefineSourceType("N", "Throwing", TypeAttributes.Public);
        var fields = new List<FieldDef>();
        source.OnLoadFields = _ => throw new IOException("transient");

        Assert.Throws<IOException>(() => type.Fields);
        source.OnLoadFields = _ => fields;

        Assert.Same(fields, type.Fields);
        Assert.Same(fields, type.Fields);
        Assert.Equal(2, source.Count(nameof(IModuleSource.LoadFields)));
    }

    [Fact]
    public void DefaultSourceMembersYieldNoExportsAndEmptyLists()
    {
        using var resolver = new SourceResolver();
        var source = new MinimalSource();
        var module = resolver.Create(new AssemblyName("Minimal"), source);
        var type = module.DefineSourceType("N", "Minimal", TypeAttributes.Public);

        Assert.Null(module.FindType("N", "Absent"));
        Assert.Throws<InvalidOperationException>(() => module.FindType("N", "Absent", throwIfNotFound: true));
        Assert.Empty(type.MethodImpls);
        Assert.Empty(type.Properties);
        Assert.Empty(type.Events);
        Assert.Same(type.MethodImpls, type.MethodImpls);
        Assert.Same(type.Properties, type.Properties);
        Assert.Same(type.Events, type.Events);
    }

    [Fact]
    public void SelfReferentialConstraintLoadsGenericParamsOnce()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Constraints"), source);
        var foo = module.DefineSourceType("N", "Foo`1", TypeAttributes.Public);
        int depth = 0;
        source.OnLoadGenericParams = (TypeDef type, out GenericParamType[] pars) => {
            Assert.True(++depth == 1, "LoadGenericParams was re-entered while resolving a constraint");
            try {
                var t = new GenericParamType(0, isMethodParam: false, "T");
                pars = [t]; // published before the constraint re-reads type.GenericParams
                t.Constraints = [new GenericParamConstraint(type.GetSpec([t]), Array.Empty<CustomAttrib>())];
            } finally {
                depth--;
            }
        };

        var pars = foo.GenericParams;

        Assert.Equal(1, source.Count(nameof(IModuleSource.LoadGenericParams)));
        var constraint = Assert.IsType<TypeSpec>(Assert.Single(Assert.Single(pars).Constraints).Sig.Type);
        Assert.Same(foo, constraint.Definition);
        Assert.Equal<TypeDesc>(pars[0], Assert.Single(constraint.GenericParams));
        Assert.Same(pars, foo.GenericParams);
        Assert.Equal(1, source.Count(nameof(IModuleSource.LoadGenericParams)));
    }

    [Fact]
    public void GenericParamsPublishedBeforeAFailedConstraintAreDiscarded()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Retry"), source);
        var foo = module.DefineSourceType("N", "Foo`1", TypeAttributes.Public);
        var failure = new InvalidOperationException("constraint could not be resolved");
        GenericParamType[]? failedPars = null;
        TypeSpec? staleSpec = null;
        source.OnLoadGenericParams = (TypeDef type, out GenericParamType[] pars) => {
            var t = new GenericParamType(0, isMethodParam: false, "T");
            pars = [t];
            var spec = type.GetSpec([t]);
            if (failedPars == null) {
                failedPars = pars;
                staleSpec = spec;
                throw failure;
            }
            t.Constraints = [new GenericParamConstraint(spec, Array.Empty<CustomAttrib>())];
        };

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => foo.GenericParams));

        var pars = foo.GenericParams;

        Assert.Equal(2, source.Count(nameof(IModuleSource.LoadGenericParams)));
        Assert.NotSame(failedPars, pars);
        var constraint = Assert.IsType<TypeSpec>(Assert.Single(Assert.Single(pars).Constraints).Sig.Type);
        Assert.Same(foo, constraint.Definition);
        // The spec built over the discarded parameters shares a cache key with the new ones; it must not be handed back.
        Assert.NotNull(staleSpec);
        Assert.NotSame(staleSpec, constraint);
        Assert.Same(constraint, foo.GetSpec([pars[0]]));
        Assert.Same(pars, foo.GenericParams);
        Assert.Equal(2, source.Count(nameof(IModuleSource.LoadGenericParams)));
    }

    [Fact]
    public void MutuallyReferentialConstraintsLoadEachTypeOnce()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Mutual"), source);
        var foo = module.DefineSourceType("N", "Foo`1", TypeAttributes.Public);
        var bar = module.DefineSourceType("N", "Bar`1", TypeAttributes.Public);
        var active = new HashSet<TypeDef>();
        source.OnLoadGenericParams = (TypeDef type, out GenericParamType[] pars) => {
            Assert.True(active.Add(type), $"LoadGenericParams was re-entered for {type.Name}");
            Assert.True(active.Count <= 2, "LoadGenericParams recursed past the two types of the cycle");
            try {
                var t = new GenericParamType(0, isMethodParam: false, "T");
                pars = [t];
                t.Constraints = [new GenericParamConstraint((type == foo ? bar : foo).GetSpec([t]), Array.Empty<CustomAttrib>())];
            } finally {
                active.Remove(type);
            }
        };

        var fooConstraint = Assert.IsType<TypeSpec>(Assert.Single(Assert.Single(foo.GenericParams).Constraints).Sig.Type);
        var barConstraint = Assert.IsType<TypeSpec>(Assert.Single(Assert.Single(bar.GenericParams).Constraints).Sig.Type);

        Assert.Same(bar, fooConstraint.Definition);
        Assert.Same(foo, barConstraint.Definition);
        Assert.Equal(2, source.Count(nameof(IModuleSource.LoadGenericParams)));
        Assert.Single(source.Calls, c => ReferenceEquals(c.Target, foo));
        Assert.Single(source.Calls, c => ReferenceEquals(c.Target, bar));
    }

    [Fact]
    public void ILBodyOfASourceMethodComesFromTheSourceOnce()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Bodies"), source);
        var type = module.DefineSourceType("N", "Bodies", TypeAttributes.Public);
        var body = ReturnArgBody();
        source.OnLoadMethods = t => [StaticIdentity(t)];
        source.OnLoadBody = _ => body;
        var method = Assert.Single(type.Methods);

        Assert.Same(body, method.ILBody);
        Assert.Same(body, method.ILBody);
        var call = Assert.Single(source.Calls, c => c.Member == nameof(IModuleSource.LoadBody));
        Assert.Same(method, call.Target);
    }

    [Fact]
    public void ExplicitILBodyWinsOverTheSource()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Explicit"), source);
        var type = module.DefineSourceType("N", "Explicit", TypeAttributes.Public);
        var explicitBody = ReturnArgBody();
        source.OnLoadMethods = t => [StaticIdentity(t)];
        source.OnLoadBody = _ => ReturnArgBody();
        var method = Assert.Single(type.Methods);

        method.ILBody = explicitBody;

        Assert.Same(explicitBody, method.ILBody);
        Assert.Equal(0, source.Count(nameof(IModuleSource.LoadBody)));
    }

    [Fact]
    public void MethodsCreatedOnNonSourceTypesNeverConsultTheSource()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource { OnLoadBody = _ => ReturnArgBody() };
        var module = resolver.Create(new AssemblyName("Created"), source);
        var plain = module.CreateType("N", "Plain");
        var method = plain.CreateMethod("Identity", PrimType.Int32, [new ParamDef(PrimType.Int32, "value")],
            MethodAttributes.Public | MethodAttributes.Static);

        Assert.Null(method.ILBody);
        Assert.Same(method, Assert.Single(plain.Methods));
        AssertNoMemberRequests(source);
    }

    [Fact]
    public void MethodsCreatedOnSourceTypesRequestTheirBody()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Added"), source);
        var type = module.DefineSourceType("N", "Added", TypeAttributes.Public);
        var method = type.CreateMethod("Identity", PrimType.Int32, [new ParamDef(PrimType.Int32, "value")],
            MethodAttributes.Public | MethodAttributes.Static);

        Assert.Null(method.ILBody);
        Assert.Null(method.ILBody);

        Assert.Equal(1, source.Count(nameof(IModuleSource.LoadMethods)));
        Assert.Equal(2, source.Count(nameof(IModuleSource.LoadBody)));
        Assert.All(source.Calls.Where(c => c.Member == nameof(IModuleSource.LoadBody)), c => Assert.Same(method, c.Target));
        Assert.Same(method, Assert.Single(type.Methods));
    }

    [Fact]
    public void NestedSourceTypesAreFiledUnderTheirDeclaringType()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Nesting"), source);
        var outer = module.DefineSourceType("N", "Outer", TypeAttributes.Public);

        var inner = module.DefineSourceType(null, "Inner", TypeAttributes.NestedPublic, outer);

        Assert.True(inner.IsNested);
        Assert.Same(outer, inner.DeclaringType);
        Assert.Same(inner, Assert.Single(outer.NestedTypes));
        Assert.Same(inner, outer.FindNestedType("Inner"));
        Assert.Contains(inner, module.TypeDefs);
        Assert.Equal(2, module.TypeDefs.Count);
        Assert.Null(module.FindType(null, "Inner"));
        Assert.Null(module.FindType("N", "Inner"));
    }

    [Theory]
    [InlineData(TypeAttributes.Public, true)]
    [InlineData(TypeAttributes.NestedPublic, false)]
    [InlineData(TypeAttributes.NestedPrivate, false)]
    public void NestingMustMatchTheAttributes(TypeAttributes attrs, bool withDeclaringType)
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Mismatch"), source);
        var outer = module.DefineSourceType("N", "Outer", TypeAttributes.Public);
        var types = module.TypeDefs.ToArray();

        Assert.Throws<InvalidOperationException>(() => module.DefineSourceType(null, "Bad", attrs, withDeclaringType ? outer : null));

        Assert.Empty(outer.NestedTypes);
        Assert.Equal(types, module.TypeDefs);
        Assert.Null(module.FindType(null, "Bad", includeExports: false));
    }

    [Fact]
    public void DuplicateNestedSourceTypeThrowsWithoutPartialState()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("NestedTwice"), source);
        var outer = module.DefineSourceType("N", "Outer", TypeAttributes.Public);
        var inner = module.DefineSourceType(null, "Inner", TypeAttributes.NestedPublic, outer);
        var types = module.TypeDefs.ToArray();

        Assert.Throws<InvalidOperationException>(() => module.DefineSourceType(null, "Inner", TypeAttributes.NestedPrivate, outer));

        Assert.Same(inner, Assert.Single(outer.NestedTypes));
        Assert.Equal(types, module.TypeDefs);
    }

    [Fact]
    public void NestedNamesAreScopedToTheirDeclaringType()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Scopes"), source);
        var root = module.DefineSourceType(null, "Inner", TypeAttributes.Public);
        var first = module.DefineSourceType("N", "First", TypeAttributes.Public);
        var second = module.DefineSourceType("N", "Second", TypeAttributes.Public);

        var firstInner = module.DefineSourceType(null, "Inner", TypeAttributes.NestedPublic, first);
        var secondInner = module.DefineSourceType(null, "Inner", TypeAttributes.NestedPublic, second);

        Assert.Same(firstInner, first.FindNestedType("Inner"));
        Assert.Same(secondInner, second.FindNestedType("Inner"));
        Assert.Same(root, module.FindType(null, "Inner"));
        Assert.Equal(5, module.TypeDefs.Count);
    }

    [Fact]
    public void DeclaringTypeMustBelongToTheModule()
    {
        using var resolver = new SourceResolver();
        var module = resolver.Create(new AssemblyName("Here"), new FakeSource());
        var elsewhere = resolver.Create(new AssemblyName("Elsewhere"), new FakeSource());
        var foreign = elsewhere.DefineSourceType("N", "Foreign", TypeAttributes.Public);

        Assert.Throws<InvalidOperationException>(() => module.DefineSourceType(null, "Inner", TypeAttributes.NestedPublic, foreign));

        Assert.Empty(foreign.NestedTypes);
        Assert.Empty(module.TypeDefs);
        Assert.Single(elsewhere.TypeDefs);
    }

    [Fact]
    public void DuplicateRootSourceTypeThrowsWithoutPartialState()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("RootTwice"), source);
        var type = module.DefineSourceType("N", "Twice", TypeAttributes.Public);
        var other = module.DefineSourceType("M", "Twice", TypeAttributes.Public);
        var types = module.TypeDefs.ToArray();

        Assert.Throws<InvalidOperationException>(() => module.DefineSourceType("N", "Twice", TypeAttributes.NotPublic));

        Assert.Equal(types, module.TypeDefs);
        Assert.Same(type, module.FindType("N", "Twice"));
        Assert.Same(other, module.FindType("M", "Twice"));
    }

    [Fact]
    public void DefineSourceTypeRequiresASourceModule()
    {
        using var resolver = new ModuleResolver();
        resolver.AddTrustedSearchPaths(); // Create(string) defines <Module> over SysTypes.Object
        var created = resolver.Create("Plain");
        var loaded = resolver.Load(typeof(object).Assembly.Location);
        var createdTypes = created.TypeDefs.ToArray();
        int loadedCount = loaded.TypeDefs.Count;

        Assert.Throws<InvalidOperationException>(() => created.DefineSourceType("N", "Defined", TypeAttributes.Public));
        Assert.Throws<InvalidOperationException>(() => loaded.DefineSourceType("N", "Defined", TypeAttributes.Public));

        Assert.Equal(createdTypes, created.TypeDefs);
        Assert.Equal(loadedCount, loaded.TypeDefs.Count);
    }

    [Fact]
    public void FindTypeFallsBackToTheSourceExports()
    {
        using var resolver = new SourceResolver();
        var target = resolver.Create(new AssemblyName("Target"), new FakeSource());
        var forwarded = target.DefineSourceType("N", "Forwarded", TypeAttributes.Public);
        var source = new FakeSource { OnFindExportedType = (ns, name) => ns == "N" && name == "Forwarded" ? forwarded : null };
        var module = resolver.Create(new AssemblyName("Forwarder"), source);
        var local = module.DefineSourceType("N", "Local", TypeAttributes.Public);

        Assert.Same(local, module.FindType("N", "Local"));
        Assert.Empty(source.Calls);

        Assert.Same(forwarded, module.FindType("N", "Forwarded"));
        var call = Assert.Single(source.Calls);
        Assert.Equal(nameof(IModuleSource.FindExportedType), call.Member);
        Assert.Equal(("N", "Forwarded"), call.Target);

        Assert.Null(module.FindType("N", "Missing"));
        Assert.Equal(2, source.Count(nameof(IModuleSource.FindExportedType)));
        Assert.Empty(module.ExportedTypes);
    }

    [Fact]
    public void FindTypeWithoutExportsDoesNotConsultTheSource()
    {
        using var resolver = new SourceResolver();
        var target = resolver.Create(new AssemblyName("Target"), new FakeSource());
        var forwarded = target.DefineSourceType("N", "Forwarded", TypeAttributes.Public);
        var source = new FakeSource { OnFindExportedType = (_, _) => forwarded };
        var module = resolver.Create(new AssemblyName("Forwarder"), source);

        Assert.Null(module.FindType("N", "Forwarded", includeExports: false));
        Assert.Throws<InvalidOperationException>(() => module.FindType("N", "Forwarded", includeExports: false, throwIfNotFound: true));
        Assert.Empty(source.Calls);
    }

    [Fact]
    public void CreatedTypesInASourceModuleNeverConsultTheSource()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Mixed"), source);
        var defined = module.DefineSourceType("N", "Defined", TypeAttributes.Public);
        var explicitBase = module.CreateType("N", "ExplicitBase", baseType: defined);
        var defaultBase = module.CreateType("N", "DefaultBase");
        var nested = defined.CreateNestedType("Nested", baseType: defined);

        foreach (var type in new[] { explicitBase, defaultBase, nested }) {
            _ = type.GenericParams;
            _ = type.Fields;
            _ = type.Methods;
            _ = type.Interfaces;
            _ = type.MethodImpls;
            _ = type.Properties;
            _ = type.Events;
        }

        Assert.Same(defined, explicitBase.BaseType);
        Assert.Same(resolver.SysTypes.Object, defaultBase.BaseType);
        Assert.Same(defined, nested.BaseType);
        AssertNoMemberRequests(source);
        Assert.DoesNotContain(resolver.CoreLibSource!.Calls,
            c => ReferenceEquals(c.Target, explicitBase) || ReferenceEquals(c.Target, defaultBase) || ReferenceEquals(c.Target, nested));
    }

    [Fact]
    public void DisposingTheResolverDisposesTheSourceOnce()
    {
        var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Disposed"), source);
        var type = module.DefineSourceType("N", "Disposed", TypeAttributes.Public);

        resolver.Dispose();
        resolver.Dispose();

        Assert.Equal(1, source.DisposeCount);
        _ = type.BaseType;
        _ = type.Fields;
        Assert.Empty(source.Calls);
    }

    [Fact]
    public void NonDisposableSourcesAreReleasedQuietly()
    {
        var resolver = new SourceResolver();
        resolver.Create(new AssemblyName("Quiet"), new MinimalSource());

        Assert.Null(Record.Exception(() => resolver.Dispose()));
    }

    [Fact]
    public void ImporterParsesASourceMethodBody()
    {
        using var resolver = new SourceResolver();
        var source = new FakeSource();
        var module = resolver.Create(new AssemblyName("Smoke"), source);
        var type = module.DefineSourceType("N", "Program", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        source.OnGetBaseType = _ => resolver.SysTypes.Object;
        source.OnLoadMethods = t => [StaticIdentity(t)];
        source.OnLoadBody = _ => ReturnArgBody();
        var method = Assert.Single(type.Methods);

        var body = ILImporter.ParseCode(method);
        IRVerifier.Verify(body);

        var ret = Assert.Single(body.Instructions().OfType<ReturnInst>());
        Assert.Same(Assert.Single(body.Args), ret.Value);
        Assert.Single(source.Calls, c => c.Member == nameof(IModuleSource.LoadBody));
    }

    static MethodDef StaticIdentity(TypeDef type)
        => new(type, PrimType.Int32, [new ParamDef(PrimType.Int32, "value")], "Identity", MethodAttributes.Public | MethodAttributes.Static);

    // ldarg.0; ret
    static ILMethodBody ReturnArgBody()
        => new() { Instructions = new ILInstruction[] { new(ILCode.Ldarg_0) { Offset = 0 }, new(ILCode.Ret) { Offset = 1 } }, MaxStack = 1 };

    // CreateType's duplicate check looks for a forwarded type of the same name; nothing else may reach the source.
    static void AssertNoMemberRequests(FakeSource source)
        => Assert.DoesNotContain(source.Calls, c => c.Member != nameof(IModuleSource.FindExportedType));

    /// <summary> Counts resolutions, and creates a source-backed <c>System.Private.CoreLib</c> on demand. </summary>
    sealed class SourceResolver : ModuleResolver
    {
        static readonly string[] s_systemTypeNames = typeof(SystemTypes).GetFields()
            .Where(f => f.FieldType == typeof(TypeDef)).Select(f => f.Name).ToArray();

        public List<string> Requests { get; } = new();
        public FakeSource? CoreLibSource { get; private set; }

        protected override ModuleDef? ResolveImpl(string name)
        {
            Requests.Add(name);
            if (name != "System.Private.CoreLib") {
                return null;
            }
            CoreLibSource = new FakeSource {
                OnGetBaseType = type => type.Name switch {
                    "Object" => null,
                    "String" or "Array" or "Type" or "Delegate" or "ValueType" => type.Module.FindType("System", "Object"),
                    _ => type.Module.FindType("System", "ValueType"),
                }
            };
            var coreLib = Create(new AssemblyName(name), CoreLibSource);
            foreach (string typeName in s_systemTypeNames) {
                coreLib.DefineSourceType("System", typeName, TypeAttributes.Public);
            }
            return coreLib;
        }
    }

    sealed class Call(string member, object target)
    {
        public string Member { get; } = member;
        public object Target { get; } = target;
        public object? Result { get; set; }
    }

    sealed class FakeSource : IModuleSource, IDisposable
    {
        public delegate void GenericParamsLoader(TypeDef type, out GenericParamType[] genericParams);

        public List<Call> Calls { get; } = new();
        public int DisposeCount { get; private set; }

        public Func<TypeDef, TypeDefOrSpec?> OnGetBaseType { get; set; } = _ => null;
        public GenericParamsLoader OnLoadGenericParams { get; set; } = (TypeDef _, out GenericParamType[] pars) => pars = [];
        public Func<TypeDef, List<FieldDef>> OnLoadFields { get; set; } = _ => new();
        public Func<TypeDef, List<MethodDef>> OnLoadMethods { get; set; } = _ => new();
        public Func<TypeDef, List<TypeDesc>> OnLoadInterfaces { get; set; } = _ => new();
        public Func<MethodDef, ILMethodBody?> OnLoadBody { get; set; } = _ => null;
        public Func<string?, string, TypeDef?> OnFindExportedType { get; set; } = (_, _) => null;
        public Func<TypeDef, Dictionary<MethodDesc, MethodDef>> OnLoadMethodImpls { get; set; } = _ => new();
        public Func<TypeDef, List<PropertyDef>> OnLoadProperties { get; set; } = _ => new();
        public Func<TypeDef, List<EventDef>> OnLoadEvents { get; set; } = _ => new();

        public int Count(string member) => Calls.Count(c => c.Member == member);

        public TypeDefOrSpec? GetBaseType(TypeDef type) => Record(nameof(GetBaseType), type, OnGetBaseType);

        public void LoadGenericParams(TypeDef type, out GenericParamType[] genericParams)
        {
            var call = new Call(nameof(LoadGenericParams), type);
            Calls.Add(call);
            OnLoadGenericParams(type, out genericParams);
            call.Result = genericParams;
        }

        public List<FieldDef> LoadFields(TypeDef type) => Record(nameof(LoadFields), type, OnLoadFields);
        public List<MethodDef> LoadMethods(TypeDef type) => Record(nameof(LoadMethods), type, OnLoadMethods);
        public List<TypeDesc> LoadInterfaces(TypeDef type) => Record(nameof(LoadInterfaces), type, OnLoadInterfaces);
        public ILMethodBody? LoadBody(MethodDef method) => Record(nameof(LoadBody), method, OnLoadBody);
        public TypeDef? FindExportedType(string? ns, string name) => Record(nameof(FindExportedType), (ns, name), _ => OnFindExportedType(ns, name));
        public Dictionary<MethodDesc, MethodDef> LoadMethodImpls(TypeDef type) => Record(nameof(LoadMethodImpls), type, OnLoadMethodImpls);
        public List<PropertyDef> LoadProperties(TypeDef type) => Record(nameof(LoadProperties), type, OnLoadProperties);
        public List<EventDef> LoadEvents(TypeDef type) => Record(nameof(LoadEvents), type, OnLoadEvents);

        public void Dispose() => DisposeCount++;

        TResult Record<TTarget, TResult>(string member, TTarget target, Func<TTarget, TResult> load) where TTarget : notnull
        {
            var call = new Call(member, target);
            Calls.Add(call);
            var result = load(target);
            call.Result = result;
            return result;
        }
    }

    /// <summary> Implements only the required members, relying on the interface defaults; not disposable. </summary>
    sealed class MinimalSource : IModuleSource
    {
        public TypeDefOrSpec? GetBaseType(TypeDef type) => null;
        public void LoadGenericParams(TypeDef type, out GenericParamType[] genericParams) => genericParams = [];
        public List<FieldDef> LoadFields(TypeDef type) => new();
        public List<MethodDef> LoadMethods(TypeDef type) => new();
        public List<TypeDesc> LoadInterfaces(TypeDef type) => new();
        public ILMethodBody? LoadBody(MethodDef method) => null;
    }
}
