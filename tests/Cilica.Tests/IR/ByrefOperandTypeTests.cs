namespace Cilica.Tests.IR;

using Cilica.AsmIO;
using Cilica.IR;

// Mixed byref operand typing, ECMA335 III.1.5 tables 2 (binary numeric), 4 (comparison) and 7 (overflow arithmetic)
public class ByrefOperandTypeTests
{
    [Theory]
    [InlineData(BinaryOp.Sub, TypeKind.Int32)]
    [InlineData(BinaryOp.Sub, TypeKind.IntPtr)]
    [InlineData(BinaryOp.Sub, TypeKind.UIntPtr)]
    [InlineData(BinaryOp.USubOvf, TypeKind.Int32)]
    [InlineData(BinaryOp.USubOvf, TypeKind.IntPtr)]
    public void ByrefMinusIntegerYieldsTheByref(BinaryOp op, TypeKind offsetKind)
    {
        var (byref, offset) = CreateArgs(PrimType.Byte.CreateByref(), PrimType.GetFromKind(offsetKind));
        var inst = new BinaryInst(op, byref, offset);
        Assert.Same(byref.ResultType, inst.ResultType);
    }

    [Theory]
    [InlineData(BinaryOp.Sub, TypeKind.Int32)]
    [InlineData(BinaryOp.Sub, TypeKind.IntPtr)]
    [InlineData(BinaryOp.USubOvf, TypeKind.Int32)]
    [InlineData(BinaryOp.USubOvf, TypeKind.IntPtr)]
    public void IntegerMinusByrefIsRejected(BinaryOp op, TypeKind offsetKind)
    {
        var (byref, offset) = CreateArgs(PrimType.Byte.CreateByref(), PrimType.GetFromKind(offsetKind));
        Assert.Throws<InvalidOperationException>(() => new BinaryInst(op, offset, byref));
    }

    [Theory]
    [InlineData(BinaryOp.Sub)]
    [InlineData(BinaryOp.USubOvf)]
    public void ByrefMinusByrefYieldsNativeInt(BinaryOp op)
    {
        var (left, right) = CreateArgs(PrimType.Byte.CreateByref(), PrimType.Int32.CreateByref());
        Assert.Same(PrimType.IntPtr, new BinaryInst(op, left, right).ResultType);
        Assert.Same(PrimType.IntPtr, new BinaryInst(op, right, left).ResultType);
    }

    [Theory]
    [InlineData(BinaryOp.Add, TypeKind.Int32, false)]
    [InlineData(BinaryOp.Add, TypeKind.Int32, true)]
    [InlineData(BinaryOp.Add, TypeKind.IntPtr, false)]
    [InlineData(BinaryOp.Add, TypeKind.IntPtr, true)]
    [InlineData(BinaryOp.UAddOvf, TypeKind.Int32, false)]
    [InlineData(BinaryOp.UAddOvf, TypeKind.Int32, true)]
    [InlineData(BinaryOp.UAddOvf, TypeKind.IntPtr, false)]
    [InlineData(BinaryOp.UAddOvf, TypeKind.IntPtr, true)]
    public void IntegerPlusByrefYieldsTheByref(BinaryOp op, TypeKind offsetKind, bool byrefFirst)
    {
        var (byref, offset) = CreateArgs(PrimType.Byte.CreateByref(), PrimType.GetFromKind(offsetKind));
        var inst = byrefFirst ? new BinaryInst(op, byref, offset) : new BinaryInst(op, offset, byref);
        Assert.Same(byref.ResultType, inst.ResultType);
    }

    [Theory]
    [InlineData(BinaryOp.SubOvf, TypeKind.Int32)]   // only sub.ovf.un is defined for & - int
    [InlineData(BinaryOp.SubOvf, TypeKind.IntPtr)]
    [InlineData(BinaryOp.Mul, TypeKind.IntPtr)]
    [InlineData(BinaryOp.SDiv, TypeKind.Int32)]
    [InlineData(BinaryOp.Sub, TypeKind.Int64)]
    [InlineData(BinaryOp.Add, TypeKind.Int64)]
    public void ByrefWithIntegerOutsideTheTablesIsRejected(BinaryOp op, TypeKind offsetKind)
    {
        var (byref, offset) = CreateArgs(PrimType.Byte.CreateByref(), PrimType.GetFromKind(offsetKind));
        Assert.Throws<InvalidOperationException>(() => new BinaryInst(op, byref, offset));
    }

    [Theory]
    [InlineData(BinaryOp.SubOvf)]   // only sub.ovf.un is defined for & - &
    [InlineData(BinaryOp.Add)]
    [InlineData(BinaryOp.UAddOvf)]
    public void ByrefWithByrefOutsideTheTablesIsRejected(BinaryOp op)
    {
        var (left, right) = CreateArgs(PrimType.Byte.CreateByref(), PrimType.Byte.CreateByref());
        Assert.Throws<InvalidOperationException>(() => new BinaryInst(op, left, right));
    }

    [Theory]
    [InlineData(CompareOp.Eq, TypeKind.IntPtr, false)]
    [InlineData(CompareOp.Eq, TypeKind.IntPtr, true)]
    [InlineData(CompareOp.Ne, TypeKind.IntPtr, false)]
    [InlineData(CompareOp.Ne, TypeKind.IntPtr, true)]
    [InlineData(CompareOp.Eq, TypeKind.UIntPtr, false)]
    [InlineData(CompareOp.Ne, TypeKind.UIntPtr, true)]
    public void NativeIntAndByrefCompareForEquality(CompareOp op, TypeKind nativeKind, bool byrefFirst)
    {
        var (byref, native) = CreateArgs(PrimType.Byte.CreateByref(), PrimType.GetFromKind(nativeKind));
        var inst = byrefFirst ? new CompareInst(op, byref, native) : new CompareInst(op, native, byref);
        Assert.Same(PrimType.Bool, inst.ResultType);
    }

    [Theory]
    [InlineData(CompareOp.Slt, false)]
    [InlineData(CompareOp.Slt, true)]
    [InlineData(CompareOp.Ult, false)]
    [InlineData(CompareOp.Ult, true)]
    [InlineData(CompareOp.Sge, true)]
    [InlineData(CompareOp.Ugt, false)]
    public void NativeIntAndByrefRejectOrderingComparisons(CompareOp op, bool byrefFirst)
    {
        var (byref, native) = CreateArgs(PrimType.Byte.CreateByref(), PrimType.IntPtr);
        Assert.Throws<InvalidOperationException>(() => byrefFirst ? new CompareInst(op, byref, native) : new CompareInst(op, native, byref));
    }

    [Theory]
    [InlineData(CompareOp.Eq, false)]
    [InlineData(CompareOp.Eq, true)]
    [InlineData(CompareOp.Ne, false)]
    [InlineData(CompareOp.Ult, true)]
    public void Int32AndByrefComparisonIsRejected(CompareOp op, bool byrefFirst)
    {
        var (byref, value) = CreateArgs(PrimType.Byte.CreateByref(), PrimType.Int32);
        Assert.Throws<InvalidOperationException>(() => byrefFirst ? new CompareInst(op, byref, value) : new CompareInst(op, value, byref));
    }

    private static (Value, Value) CreateArgs(TypeDesc first, TypeDesc second)
    {
        var body = Utils.CreateDummyMethodBody(PrimType.Void, first, second);
        return (body.Args[0], body.Args[1]);
    }
}
