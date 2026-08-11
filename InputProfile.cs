using System;

enum InputProfileType
{
    InputProcessor,
    KeyboardLayout
}

sealed class InputProfile
{
    public InputProfileType Type { get; init; }

    public string DisplayName { get; init; } = "";

    public ushort LanguageId { get; init; }

    public Guid Clsid { get; init; }

    public Guid ProfileGuid { get; init; }

    public IntPtr Hkl { get; init; }

    public bool IsEnabled { get; init; }

    public bool IsActive { get; init; }
}