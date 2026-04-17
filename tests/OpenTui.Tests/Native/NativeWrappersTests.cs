using Xunit;

namespace OpenTui.Tests.Native;

public class NativeWrappersTests
{
    [Fact]
    public void Utf8String_Empty_HasZeroLength()
    {
        var s = new Utf8String("");
        Assert.Equal(0, s.Length);
    }

    [Fact]
    public void Utf8String_Ascii_CorrectLength()
    {
        var s = new Utf8String("Hello");
        Assert.Equal(5, s.Length);
    }

    [Fact]
    public void Utf8String_Unicode_CorrectByteLength()
    {
        var s = new Utf8String("🎉");
        Assert.Equal(4, s.Length); // 4 UTF-8 bytes for a 4-byte emoji
    }

    [Fact]
    public void Utf8String_Null_HasZeroLength()
    {
        var s = new Utf8String(null);
        Assert.Equal(0, s.Length);
    }

    [Fact]
    public void Utf8String_WithPtr_PassesNullForEmpty()
    {
        var s = new Utf8String("");
        nint capturedPtr = (nint)1;
        nuint capturedLen = 1;

        s.WithPtr((ptr, len) =>
        {
            capturedPtr = ptr;
            capturedLen = len;
        });

        Assert.Equal(nint.Zero, capturedPtr);
        Assert.Equal((nuint)0, capturedLen);
    }

    [Fact]
    public void Utf8String_WithPtr_PinsCorrectBytes()
    {
        var s = new Utf8String("AB");
        byte[]? captured = null;

        s.WithPtr((ptr, len) =>
        {
            Assert.Equal((nuint)2, len);
            unsafe
            {
                byte* p = (byte*)ptr;
                captured = [p[0], p[1]];
            }
        });

        Assert.NotNull(captured);
        Assert.Equal((byte)'A', captured![0]);
        Assert.Equal((byte)'B', captured[1]);
    }

    [Fact]
    public void RgbaMarshalling_WithColorPtr_PassesCorrectValues()
    {
        var color = new Rgba(1.0f, 0.5f, 0.25f, 1.0f);
        float[]? captured = null;

        RgbaMarshalling.WithColorPtr(color, ptr =>
        {
            unsafe
            {
                float* p = (float*)ptr;
                captured = [p[0], p[1], p[2], p[3]];
            }
        });

        Assert.NotNull(captured);
        Assert.Equal(1.0f, captured![0]);
        Assert.Equal(0.5f, captured[1]);
        Assert.Equal(0.25f, captured[2]);
        Assert.Equal(1.0f, captured[3]);
    }

    [Fact]
    public void RgbaMarshalling_WithColorPtrs_PassesBothColors()
    {
        var fg = new Rgba(1.0f, 0.0f, 0.0f, 1.0f);
        var bg = new Rgba(0.0f, 0.0f, 1.0f, 0.5f);
        float[]? capturedFg = null;
        float[]? capturedBg = null;

        RgbaMarshalling.WithColorPtrs(fg, bg, (fgPtr, bgPtr) =>
        {
            unsafe
            {
                float* pFg = (float*)fgPtr;
                float* pBg = (float*)bgPtr;
                capturedFg = [pFg[0], pFg[1], pFg[2], pFg[3]];
                capturedBg = [pBg[0], pBg[1], pBg[2], pBg[3]];
            }
        });

        Assert.NotNull(capturedFg);
        Assert.Equal(1.0f, capturedFg![0]);
        Assert.Equal(0.0f, capturedFg[1]);

        Assert.NotNull(capturedBg);
        Assert.Equal(0.0f, capturedBg![0]);
        Assert.Equal(1.0f, capturedBg[2]);
        Assert.Equal(0.5f, capturedBg[3]);
    }
}
