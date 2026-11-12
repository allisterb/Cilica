.class public ImporterCases
{
    .method public static native unsigned int ObjectByteOffset(object origin, uint8& target)
    {
        .locals (object pinned owner, int32& originRef)
        ldarg.1
        ldarg.0
        stloc.0
        ldloc.0
        conv.u
        stloc.1
        ldloc.1
        sub
        ret
    }

    .method public static native unsigned int ObjectByteOffsetAcrossBlocks(object origin, uint8& target)
    {
        .locals (object pinned owner, int32& originRef)
        ldarg.0
        stloc.0
        ldloc.0
        conv.u
        stloc.1
        br.s subtract
    subtract:
        ldarg.1
        ldloc.1
        sub
        ret
    }

    .method public static native int ReinterpretedByrefOffset(uint8& origin, uint8& target)
    {
        .locals (uint8& originRef)
        ldarg.0
        conv.u
        stloc.0
        ldarg.1
        ldloc.0
        sub
        ret
    }

    .method public static native int NativeLocalFromByref(uint8& address)
    {
        .locals (native int pointer)
        ldarg.0
        stloc.0
        ldloc.0
        ldc.i4.1
        and
        ret
    }

    .method public static uint8& InitializedByrefLocal()
    {
        .locals init (uint8& address)
        ldloc.0
        ret
    }

    .method public static native int LoadPointerField(native int address)
    {
        ldarg.0
        ldind.i
        ldfld native int NativeFieldPair::Value
        ret
    }

    .method public static void StorePointerField(native int address, native int input)
    {
        ldarg.0
        ldind.i
        ldarg.1
        stfld native int NativeFieldPair::Value
        ret
    }

    .method public static native int AddressPointerField(native int address)
    {
        ldarg.0
        ldind.i
        ldflda native int NativeFieldPair::Value
        conv.i
        ret
    }

    .method public static object ReadObjectFromUntypedPointer(void* pointer)
    {
        ldarg.0
        ldind.ref
        ret
    }

    .method public static int32 InitializedLocalBeforeStore(int32 input)
    {
        .locals init (int32 temp)
        ldloc.0
        ldarg.0
        stloc.0
        ldloc.0
        add
        ret
    }

    .method public static int32 InitializedLocalOnUnassignedPath(bool assign)
    {
        .locals init (int32 temp)
        ldarg.0
        brfalse.s done
        ldc.i4.s 17
        stloc.0
    done:
        ldloc.0
        ret
    }

    .method public static !!T InitializedGeneric<T>()
    {
        .locals init (!!T temp)
        ldloc.0
        ret
    }

    .field public static int32 StaticValue

    .method public static int32 StaticFieldWithInstanceOpcodes(int32 input)
    {
        ldnull
        ldarg.0
        stfld int32 ImporterCases::StaticValue
        ldnull
        ldflda int32 ImporterCases::StaticValue
        dup
        ldind.i4
        ldc.i4.1
        add
        stind.i4
        ldnull
        ldfld int32 ImporterCases::StaticValue
        ret
    }

    .method public static int32 UncheckedStringCall(object input)
    {
        ldarg.0
        callvirt instance int32 [System.Runtime]System.String::get_Length()
        ret
    }

    .method public static int32 UncheckedInterfaceCall(class [System.Runtime]System.IComparable input)
    {
        ldarg.0
        callvirt instance int32 [System.Runtime]System.String::get_Length()
        ret
    }

    .method public static string UncheckedStringStore(object input)
    {
        .locals init (string temp)
        ldloca.s temp
        ldarg.0
        stind.ref
        ldloc.0
        ret
    }

    .method public static string UncheckedStringReturn(object input)
    {
        ldarg.0
        ret
    }

    .method public static void PinnedVar1(int32& x)
    {
        .locals ( int32& pinned temp )
        
        ldarg x
        stloc temp

        ldloc temp
        call void ImporterCases::MutPtr(int32*)

        ldc.i4 0
        stloc temp

        ret
    }

    .method public static object CallSiteOffsets<T>(!!T item, string text)
    {
        .locals init (string temp)
        ldarg.1
        stloc.0
        br.s Next
    Next:
        ldloc.0
        callvirt instance int32 [System.Runtime]System.String::get_Length()
        pop
        ldarga.s item
        constrained. !!T
        callvirt instance string [System.Runtime]System.Object::ToString()
        pop
        ldftn void ImporterCases::MutPtr(int32*)
        pop
        ldloc.0
        ldvirtftn instance string [System.Runtime]System.Object::ToString()
        pop
        newobj instance void [System.Runtime]System.Object::.ctor()
        ret
    }

    .method private static void MutPtr(int32* ptr)
    {
        //*ptr ^= 0x5555
        ldarg ptr
        dup
        ldind.i4

        ldc.i4 0x5555
        xor
        stind.i4
        
        ret
    }

    // Unsafe.Subtract<T>(ref T, int)
    .method public static !!T& SubtractElementOffset<T>(!!T& source, int32 elementOffset)
    {
        ldarg.0
        ldarg.1
        sizeof !!T
        conv.i
        mul
        sub
        ret
    }

    // Unsafe.SubtractByteOffset<T>(ref T, nint)
    .method public static uint8& SubtractByteOffset(uint8& source, native int byteOffset)
    {
        ldarg.0
        ldarg.1
        sub
        ret
    }

    .method public static uint8& SubtractInt32Offset(uint8& source, int32 byteOffset)
    {
        ldarg.0
        ldarg.1
        sub
        ret
    }

    .method public static uint8& SubtractByteOffsetUnsignedChecked(uint8& source, native int byteOffset)
    {
        ldarg.0
        ldarg.1
        sub.ovf.un
        ret
    }

    .method public static native int ByrefDistanceUnsignedChecked(uint8& origin, uint8& target)
    {
        ldarg.1
        ldarg.0
        sub.ovf.un
        ret
    }

    .method public static uint8& AddByteOffsetUnsignedChecked(native int byteOffset, uint8& source)
    {
        ldarg.0
        ldarg.1
        add.ovf.un
        ret
    }

    // Unsafe.IsNullRef<T>(ref T)
    .method public static bool IsNullRef(uint8& source)
    {
        ldarg.0
        ldc.i4.0
        conv.u
        ceq
        ret
    }

    .method public static bool IsNotNullRefBranch(uint8& source)
    {
        ldarg.0
        ldc.i4.0
        conv.u
        bne.un.s notNull
        ldc.i4.0
        ret
    notNull:
        ldc.i4.1
        ret
    }

    // Invalid: int32 vs & is not in ECMA335 III.1.5 table 4
    .method public static bool ByrefEqualsInt32(uint8& source)
    {
        ldarg.0
        ldc.i4.0
        ceq
        ret
    }

    // Invalid: int32 vs & is not in ECMA335 III.1.5 table 4
    .method public static bool ByrefNotEqualsInt32Branch(uint8& source)
    {
        ldarg.0
        ldc.i4.0
        bne.un.s notEqual
        ldc.i4.0
        ret
    notEqual:
        ldc.i4.1
        ret
    }

    // int32 vs native int is valid, and unmanaged pointers are native int on the stack
    .method public static bool PointerEqualsInt32(uint8* pointer)
    {
        ldarg.0
        ldc.i4.0
        ceq
        ret
    }
}

.class public sequential ansi sealed beforefieldinit NativeFieldPair extends [System.Runtime]System.ValueType
{
    .field public native int Padding
    .field public native int Value
}
