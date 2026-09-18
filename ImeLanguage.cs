using System;

static class ImeLanguage
{
    public static bool IsSupported(ushort languageId) => (languageId & 0x03FF) is 0x04 or 0x11 or 0x12;

    public static int NativeConversion(ushort languageId, int current)
    {
        // Chinese and Korean must not inherit Japanese full-width/katakana flags.
        int native = (current | 0x0001) & ~0x0100;
        return (languageId & 0x03FF) == 0x11 ? (native | 0x0008) & ~0x0002 : native;
    }
}
