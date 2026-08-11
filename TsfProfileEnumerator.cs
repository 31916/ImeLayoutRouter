using System;
using System.Runtime.InteropServices;

static class TsfProfileEnumerator
{
    // Windows TSFの
    // CLSID_TF_InputProcessorProfiles
    private static readonly Guid CLSID_TF_InputProcessorProfiles =
        new Guid("33C53A50-F456-4884-B049-85FD643ECFED");

    private const uint TF_PROFILETYPE_INPUTPROCESSOR = 0x0001;
    private const uint TF_PROFILETYPE_KEYBOARDLAYOUT = 0x0002;

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
                    profile
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
        TF_INPUTPROCESSORPROFILE profile
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

        Console.WriteLine();
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