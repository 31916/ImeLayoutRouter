using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Globalization;

static class TsfProfileEnumerator
{
    // Windows TSFの
    // CLSID_TF_InputProcessorProfiles
    private static readonly Guid CLSID_TF_InputProcessorProfiles =
        new Guid("33C53A50-F456-4884-B049-85FD643ECFED");
    
    // TSFのキーボード入力プロファイルカテゴリ
    private static readonly Guid GUID_TFCAT_TIP_KEYBOARD =
        new Guid("34745C63-B2F0-4784-8B67-5E12C8701A31");

    private const uint TF_PROFILETYPE_INPUTPROCESSOR = 0x0001;
    private const uint TF_PROFILETYPE_KEYBOARDLAYOUT = 0x0002;
    private const ushort JAPANESE_LANGUAGE_ID = 0x0411;

    public static void PrintAllProfiles()
    {
        Console.WriteLine(
            "IME Layout Router - TSF Profile Enumeration"
        );

        Console.WriteLine();

        Type? comType =
            Type.GetTypeFromCLSID(
                CLSID_TF_InputProcessorProfiles
            );

        if (comType == null)
        {
            Console.WriteLine(
                "TSF InputProcessorProfiles COM class was not found."
            );

            return;
        }

        object? managerObject = null;
        IEnumTfInputProcessorProfiles? enumerator = null;

        try
        {
            managerObject =
                Activator.CreateInstance(comType);

            if (
                managerObject
                is not ITfInputProcessorProfileMgr manager
            )
            {
                Console.WriteLine(
                    "ITfInputProcessorProfileMgr could not be obtained."
                );

                return;
            }
            if (
                managerObject
                is not ITfInputProcessorProfiles profiles
            )
            {
                Console.WriteLine(
                    "ITfInputProcessorProfiles could not be obtained."
                );

                return;
            }

            // LANGID = 0
            // → Windowsに登録されている全プロファイル
            int hr =
                manager.EnumProfiles(
                    0,
                    out enumerator
                );

            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            int index = 1;

            while (true)
            {
                hr =
                    enumerator.Next(
                        1,
                        out TF_INPUTPROCESSORPROFILE profile,
                        out uint fetched
                    );

                if (
                    fetched == 0
                    || hr != 0
                )
                {
                    break;
                }

                PrintProfile(
                    index,
                    profile,
                    profiles
                );

                index++;
            }

            Console.WriteLine(
                $"Total profiles: {index - 1}"
            );
        }
        finally
        {
            if (
                enumerator != null
                && Marshal.IsComObject(enumerator)
            )
            {
                Marshal.FinalReleaseComObject(
                    enumerator
                );
            }

            if (
                managerObject != null
                && Marshal.IsComObject(managerObject)
            )
            {
                Marshal.FinalReleaseComObject(
                    managerObject
                );
            }
        }
    }

    private static void PrintProfile(
        int index,
        TF_INPUTPROCESSORPROFILE profile,
        ITfInputProcessorProfiles profiles
    )
    {
        string typeName =
            profile.dwProfileType switch
            {
                TF_PROFILETYPE_INPUTPROCESSOR
                    => "Input Processor / IME",

                TF_PROFILETYPE_KEYBOARDLAYOUT
                    => "Keyboard Layout",

                _
                    => $"Unknown ({profile.dwProfileType})"
            };

        Console.WriteLine(
            $"=== Profile {index} ==="
        );

        Console.WriteLine(
            $"Type: {typeName}"
        );

        Console.WriteLine(
            $"Language ID: 0x{profile.langid:X4}"
        );
        if (
            profile.dwProfileType
            == TF_PROFILETYPE_INPUTPROCESSOR
        )
        {
            string? description =
                GetProfileDescription(
                    profiles,
                    profile
                );

            Console.WriteLine(
                $"Description: {description ?? "(not available)"}"
            );
        }

        Console.WriteLine(
            $"CLSID: {profile.clsid}"
        );

        Console.WriteLine(
            $"Profile GUID: {profile.guidProfile}"
        );

        Console.WriteLine(
            $"Category GUID: {profile.catid}"
        );

        Console.WriteLine(
            $"HKL: 0x{profile.hkl.ToInt64():X}"
        );

        Console.WriteLine(
            $"Substitute HKL: 0x{profile.hklSubstitute.ToInt64():X}"
        );

        Console.WriteLine(
            $"Capabilities: 0x{profile.dwCaps:X8}"
        );

        Console.WriteLine(
            $"Flags: 0x{profile.dwFlags:X8}"
        );
        bool isActive =
            (profile.dwFlags & 0x00000001) != 0;

        bool isEnabled =
            (profile.dwFlags & 0x00000002) != 0;

        Console.WriteLine(
            $"Active: {isActive}"
        );

        Console.WriteLine(
            $"Enabled: {isEnabled}"
        );

        Console.WriteLine();
    }

    private static bool IsEnabled(
    TF_INPUTPROCESSORPROFILE profile
    )
    {
        return (
            profile.dwFlags
            & 0x00000002
        ) != 0;
    }

    private static string GetLanguageName(
        ushort languageId
    )
    {
        try
        {
            CultureInfo culture =
                CultureInfo.GetCultureInfo(
                    languageId
                );

            return culture.NativeName;
        }
        catch (CultureNotFoundException)
        {
            return $"Language 0x{languageId:X4}";
        }
    }

    public static (
    List<InputProfile> Sources,
    List<InputProfile> Targets
    ) GetSelectableProfiles()
    {
        List<InputProfile> allProfiles =
            GetAllInputProfiles();

        List<InputProfile> sources =
            new List<InputProfile>();

        List<InputProfile> targets =
            new List<InputProfile>();

        foreach (
            InputProfile profile
            in allProfiles
        )
        {
            if (
                profile.Type
                    == InputProfileType.InputProcessor
                &&
                profile.LanguageId
                    == JAPANESE_LANGUAGE_ID
                &&
                profile.IsEnabled
            )
            {
                sources.Add(profile);
            }

            if (
                profile.Type
                    == InputProfileType.KeyboardLayout
                &&
                profile.LanguageId
                    != JAPANESE_LANGUAGE_ID
                &&
                profile.IsEnabled
            )
            {
                targets.Add(profile);
            }
        }

        return (
            sources,
            targets
        );
    }


    private static List<InputProfile> GetAllInputProfiles()
    {
        List<InputProfile> result =
            new List<InputProfile>();

        Type? comType =
            Type.GetTypeFromCLSID(
                CLSID_TF_InputProcessorProfiles
            );

        if (comType == null)
        {
            return result;
        }

        object? managerObject = null;

        IEnumTfInputProcessorProfiles? enumerator =
            null;

        try
        {
            managerObject =
                Activator.CreateInstance(
                    comType
                );

            if (
                managerObject
                is not ITfInputProcessorProfileMgr manager
            )
            {
                return result;
            }

            if (
                managerObject
                is not ITfInputProcessorProfiles profiles
            )
            {
                return result;
            }

            int hr =
                manager.EnumProfiles(
                    0,
                    out enumerator
                );

            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            while (true)
            {
                hr =
                    enumerator.Next(
                        1,
                        out TF_INPUTPROCESSORPROFILE profile,
                        out uint fetched
                    );

                if (
                    fetched == 0
                    ||
                    hr != 0
                )
                {
                    break;
                }

                if (
                    profile.dwProfileType
                        != TF_PROFILETYPE_INPUTPROCESSOR
                    &&
                    profile.dwProfileType
                        != TF_PROFILETYPE_KEYBOARDLAYOUT
                )
                {
                    continue;
                }

                result.Add(
                    ConvertToInputProfile(
                        profile,
                        profiles
                    )
                );
            }
        }
        finally
        {
            if (
                enumerator != null
                &&
                Marshal.IsComObject(
                    enumerator
                )
            )
            {
                Marshal.FinalReleaseComObject(
                    enumerator
                );
            }

            if (
                managerObject != null
                &&
                Marshal.IsComObject(
                    managerObject
                )
            )
            {
                Marshal.FinalReleaseComObject(
                    managerObject
                );
            }
        }

        return result;
    }


    private static InputProfile ConvertToInputProfile(
        TF_INPUTPROCESSORPROFILE profile,
        ITfInputProcessorProfiles profiles
    )
    {
        InputProfileType type =
            profile.dwProfileType
                == TF_PROFILETYPE_INPUTPROCESSOR
            ? InputProfileType.InputProcessor
            : InputProfileType.KeyboardLayout;

        string displayName;

        if (
            type
            == InputProfileType.InputProcessor
        )
        {
            displayName =
                GetProfileDescription(
                    profiles,
                    profile
                )
                ?? $"Input Processor 0x{profile.langid:X4}";
        }
        else
        {
            displayName =
                GetLanguageName(
                    profile.langid
                );
        }

        return new InputProfile
        {
            Type = type,

            DisplayName =
                displayName,

            LanguageId =
                profile.langid,

            Clsid =
                profile.clsid,

            ProfileGuid =
                profile.guidProfile,

            Hkl =
                profile.hkl,

            IsEnabled =
                IsEnabled(profile),

            IsActive =
                (
                    profile.dwFlags
                    & 0x00000001
                ) != 0
        };
    }

    public static void PrintSelectableProfiles()
    {
        Console.WriteLine(
            "IME Layout Router - Selectable Profiles"
        );

        Console.WriteLine();

        var candidates =
            GetSelectableProfiles();

        Console.WriteLine(
            "=== Source Japanese IMEs ==="
        );

        if (candidates.Sources.Count == 0)
        {
            Console.WriteLine(
                "(No enabled Japanese IME found)"
            );
        }
        else
        {
            for (
                int i = 0;
                i < candidates.Sources.Count;
                i++
            )
            {
                InputProfile profile =
                    candidates.Sources[i];

                Console.WriteLine(
                    $"[{i + 1}] {profile.DisplayName}"
                );

                Console.WriteLine(
                    $"    Language ID: 0x{profile.LanguageId:X4}"
                );

                Console.WriteLine(
                    $"    CLSID: {profile.Clsid}"
                );

                Console.WriteLine(
                    $"    Profile GUID: {profile.ProfileGuid}"
                );
            }
        }

        Console.WriteLine();

        Console.WriteLine(
            "=== Target Keyboard Layouts ==="
        );

        if (candidates.Targets.Count == 0)
        {
            Console.WriteLine(
                "(No enabled target keyboard layout found)"
            );
        }
        else
        {
            for (
                int i = 0;
                i < candidates.Targets.Count;
                i++
            )
            {
                InputProfile profile =
                    candidates.Targets[i];

                Console.WriteLine(
                    $"[{i + 1}] {profile.DisplayName}"
                );

                Console.WriteLine(
                    $"    Language ID: 0x{profile.LanguageId:X4}"
                );

                Console.WriteLine(
                    $"    HKL: 0x{profile.Hkl.ToInt64():X}"
                );
            }
        }
    }
    public static void PrintActiveProfile()
    {
    Console.WriteLine(
        "IME Layout Router - Active TSF Profile"
    );

    Console.WriteLine();

    Type? comType =
        Type.GetTypeFromCLSID(
            CLSID_TF_InputProcessorProfiles
        );

    if (comType == null)
    {
        Console.WriteLine(
            "TSF InputProcessorProfiles COM class was not found."
        );

        return;
    }

    object? managerObject = null;

    try
    {
        managerObject =
            Activator.CreateInstance(comType);

        if (
            managerObject
            is not ITfInputProcessorProfileMgr manager
        )
        {
            Console.WriteLine(
                "ITfInputProcessorProfileMgr could not be obtained."
            );

            return;
        }

        if (
            managerObject
            is not ITfInputProcessorProfiles profiles
        )
        {
            Console.WriteLine(
                "ITfInputProcessorProfiles could not be obtained."
            );

            return;
        }

        Guid category =
            GUID_TFCAT_TIP_KEYBOARD;

        int hr =
            manager.GetActiveProfile(
                ref category,
                out TF_INPUTPROCESSORPROFILE profile
            );

        // S_FALSE
        if (hr == 1)
        {
            Console.WriteLine(
                "No active keyboard input profile was found."
            );

            return;
        }

        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        PrintProfile(
            1,
            profile,
            profiles
        );
    }
    finally
    {
        if (
            managerObject != null
            && Marshal.IsComObject(managerObject)
        )
        {
            Marshal.FinalReleaseComObject(
                managerObject
            );
        }
    }
}

    private static string? GetProfileDescription(
    ITfInputProcessorProfiles profiles,
    TF_INPUTPROCESSORPROFILE profile
    )
    {
        Guid clsid =
            profile.clsid;

        Guid profileGuid =
            profile.guidProfile;

        int hr =
            profiles.GetLanguageProfileDescription(
                ref clsid,
                profile.langid,
                ref profileGuid,
                out string description
            );

        if (hr < 0)
        {
            return null;
        }

        return description;
    }
}


// ============================================================
// TSF structures
// ============================================================

[StructLayout(LayoutKind.Sequential)]
struct TF_INPUTPROCESSORPROFILE
{
    public uint dwProfileType;

    public ushort langid;

    public Guid clsid;

    public Guid guidProfile;

    public Guid catid;

    public IntPtr hklSubstitute;

    public uint dwCaps;

    public IntPtr hkl;

    public uint dwFlags;
}


// ============================================================
// ITfInputProcessorProfileMgr
// ============================================================

[ComImport]
[Guid("71C6E74C-0F28-11D8-A82A-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ITfInputProcessorProfileMgr
{
    [PreserveSig]
    int ActivateProfile(
        uint dwProfileType,
        ushort langid,
        ref Guid clsid,
        ref Guid guidProfile,
        IntPtr hkl,
        uint dwFlags
    );

    [PreserveSig]
    int DeactivateProfile(
        uint dwProfileType,
        ushort langid,
        ref Guid clsid,
        ref Guid guidProfile,
        IntPtr hkl,
        uint dwFlags
    );

    [PreserveSig]
    int GetProfile(
        uint dwProfileType,
        ushort langid,
        ref Guid clsid,
        ref Guid guidProfile,
        IntPtr hkl,
        out TF_INPUTPROCESSORPROFILE profile
    );

    [PreserveSig]
    int EnumProfiles(
        ushort langid,
        out IEnumTfInputProcessorProfiles enumerator
    );

    [PreserveSig]
    int ReleaseInputProcessor(
        ref Guid clsid,
        uint dwFlags
    );

    [PreserveSig]
    int RegisterProfile(
        ref Guid clsid,
        ushort langid,
        ref Guid guidProfile,
        IntPtr description,
        uint descriptionLength,
        IntPtr iconFile,
        uint iconFileLength,
        uint iconIndex,
        IntPtr substituteHkl,
        uint preferredLayout,
        int enabledByDefault,
        uint dwFlags
    );

    [PreserveSig]
    int UnregisterProfile(
        ref Guid clsid,
        ushort langid,
        ref Guid guidProfile,
        uint dwFlags
    );

    [PreserveSig]
    int GetActiveProfile(
        ref Guid category,
        out TF_INPUTPROCESSORPROFILE profile
    );
}


// ============================================================
// IEnumTfInputProcessorProfiles
// ============================================================

[ComImport]
[Guid("71C6E74D-0F28-11D8-A82A-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IEnumTfInputProcessorProfiles
{
    [PreserveSig]
    int Clone(
        out IEnumTfInputProcessorProfiles enumerator
    );

    [PreserveSig]
    int Next(
        uint count,
        out TF_INPUTPROCESSORPROFILE profile,
        out uint fetched
    );

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Skip(
        uint count
    );
}

// ============================================================
// ITfInputProcessorProfiles
// IMEの表示名などを取得するためのTSFインターフェース
// ============================================================

[ComImport]
[Guid("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ITfInputProcessorProfiles
{
    [PreserveSig]
    int Register(
        ref Guid clsid
    );

    [PreserveSig]
    int Unregister(
        ref Guid clsid
    );

    [PreserveSig]
    int AddLanguageProfile(
        ref Guid clsid,
        ushort langid,
        ref Guid guidProfile,
        IntPtr description,
        uint descriptionLength,
        IntPtr iconFile,
        uint iconFileLength,
        uint iconIndex
    );

    [PreserveSig]
    int RemoveLanguageProfile(
        ref Guid clsid,
        ushort langid,
        ref Guid guidProfile
    );

    [PreserveSig]
    int EnumInputProcessorInfo(
        out IntPtr enumerator
    );

    [PreserveSig]
    int GetDefaultLanguageProfile(
        ushort langid,
        ref Guid category,
        out Guid clsid,
        out Guid guidProfile
    );

    [PreserveSig]
    int SetDefaultLanguageProfile(
        ushort langid,
        ref Guid clsid,
        ref Guid guidProfile
    );

    [PreserveSig]
    int ActivateLanguageProfile(
        ref Guid clsid,
        ushort langid,
        ref Guid guidProfile
    );

    [PreserveSig]
    int GetActiveLanguageProfile(
        ref Guid clsid,
        out ushort langid,
        out Guid guidProfile
    );

    [PreserveSig]
    int GetLanguageProfileDescription(
        ref Guid clsid,
        ushort langid,
        ref Guid guidProfile,
        [MarshalAs(UnmanagedType.BStr)]
        out string description
    );
}