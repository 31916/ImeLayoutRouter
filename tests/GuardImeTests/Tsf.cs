using System.Runtime.InteropServices;

internal static class Tsf
{
    internal static void ActivateJapanese()
    {
        object managerObject = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("33C53A50-F456-4884-B049-85FD643ECFED"))!)!;
        IProfiles? profiles = null;
        try
        {
            var manager = (IManager)managerObject;
            Marshal.ThrowExceptionForHR(manager.EnumProfiles(0x411, out profiles));
            while (profiles.Next(1, out var profile, out uint fetched) == 0 && fetched == 1)
            {
                Console.WriteLine($"Japanese profile: type={profile.Type}; clsid={profile.Clsid}; profile={profile.ProfileId}; flags={profile.Flags}");
                if (profile.Type != 1 || (profile.Flags & 2) == 0) continue;
                Marshal.ThrowExceptionForHR(manager.ActivateProfile(1, 0x411, ref profile.Clsid, ref profile.ProfileId, profile.Hkl, 0));
                Console.WriteLine("Japanese TSF profile activated for fixture thread.");
                return;
            }
            throw new InvalidOperationException("No enabled Japanese TSF input processor is available in the runner's session.");
        }
        finally
        {
            if (profiles != null) Marshal.FinalReleaseComObject(profiles);
            Marshal.FinalReleaseComObject(managerObject);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Profile
    {
        public uint Type; public ushort Language; public Guid Clsid, ProfileId, Category;
        public nint Substitute; public uint Caps; public nint Hkl; public uint Flags;
    }
    [ComImport, Guid("71C6E74C-0F28-11D8-A82A-00065B84435C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IManager
    {
        [PreserveSig] int ActivateProfile(uint type, ushort lang, ref Guid clsid, ref Guid profile, nint hkl, uint flags);
        [PreserveSig] int DeactivateProfile(uint type, ushort lang, ref Guid clsid, ref Guid profile, nint hkl, uint flags);
        [PreserveSig] int GetProfile(uint type, ushort lang, ref Guid clsid, ref Guid profile, nint hkl, out Profile value);
        [PreserveSig] int EnumProfiles(ushort lang, out IProfiles profiles);
    }
    [ComImport, Guid("71C6E74D-0F28-11D8-A82A-00065B84435C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IProfiles
    {
        [PreserveSig] int Clone(out IProfiles profiles);
        [PreserveSig] int Next(uint count, out Profile profile, out uint fetched);
        [PreserveSig] int Reset();
        [PreserveSig] int Skip(uint count);
    }
}
