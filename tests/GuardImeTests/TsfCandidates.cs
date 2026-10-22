using System.Runtime.InteropServices;

// Read only the fixed test word's candidate UI registered on this fixture's
// own TSF thread. Modern Microsoft IME need not publish an IMM candidate list.
// This file is never compiled into the production application.
internal static class TsfCandidates
{
    internal sealed record Snapshot(uint Count, uint Selection, bool Shown, Guid Identity, string[] Sample);
    internal static Snapshot? Read()
    {
        Marshal.ThrowExceptionForHR(TF_GetThreadMgr(out nint pointer));
        if (pointer == 0) return null;
        object manager = Marshal.GetObjectForIUnknown(pointer);
        Marshal.Release(pointer);
        IElements? elements = null;
        try
        {
            Marshal.ThrowExceptionForHR(((IElementManager)manager).EnumUIElements(out elements));
            for (int i = 0; i < 32 && elements.Next(1, out object element, out uint fetched) == 0 && fetched == 1; i++)
            {
                try
                {
                    if (element is not ICandidateList list) continue;
                    Marshal.ThrowExceptionForHR(list.GetCount(out uint count));
                    Marshal.ThrowExceptionForHR(list.GetSelection(out uint selection));
                    Marshal.ThrowExceptionForHR(list.IsShown(out bool shown));
                    Marshal.ThrowExceptionForHR(list.GetGUID(out Guid identity));
                    var sample = new List<string>();
                    for (uint index = 0; index < Math.Min(count, 10); index++)
                    {
                        Marshal.ThrowExceptionForHR(list.GetString(index, out string text));
                        sample.Add(text);
                    }
                    Console.WriteLine($"Own TSF candidates: count={count}; selection={selection}; shown={shown}; sample={string.Join(",", sample)}");
                    if (count > 0 && shown) return new(count, selection, shown, identity, sample.ToArray());
                }
                finally { Marshal.ReleaseComObject(element); }
            }
            return null;
        }
        finally
        {
            if (elements != null) Marshal.ReleaseComObject(elements);
            Marshal.ReleaseComObject(manager);
        }
    }
    [DllImport("msctf.dll", ExactSpelling = true)]
    private static extern int TF_GetThreadMgr(out nint manager);
    [ComImport, Guid("ea1ea135-19df-11d7-a6d2-00065b84435c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IElementManager
    {
        [PreserveSig] int BeginUIElement(nint element, ref int show, out uint id);
        [PreserveSig] int UpdateUIElement(uint id);
        [PreserveSig] int EndUIElement(uint id);
        [PreserveSig] int GetUIElement(uint id, [MarshalAs(UnmanagedType.IUnknown)] out object element);
        [PreserveSig] int EnumUIElements(out IElements elements);
    }
    [ComImport, Guid("887aa91e-acba-4931-84da-3c5208cf543f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IElements
    {
        [PreserveSig] int Clone(out IElements elements);
        [PreserveSig] int Next(uint count, [MarshalAs(UnmanagedType.IUnknown)] out object element, out uint fetched);
        [PreserveSig] int Reset();
        [PreserveSig] int Skip(uint count);
    }
    [ComImport, Guid("ea1ea138-19df-11d7-a6d2-00065b84435c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICandidateList
    {
        [PreserveSig] int GetDescription([MarshalAs(UnmanagedType.BStr)] out string description);
        [PreserveSig] int GetGUID(out Guid identity);
        [PreserveSig] int Show([MarshalAs(UnmanagedType.Bool)] bool show);
        [PreserveSig] int IsShown([MarshalAs(UnmanagedType.Bool)] out bool shown);
        [PreserveSig] int GetUpdatedFlags(out uint flags);
        [PreserveSig] int GetDocumentMgr(out nint manager);
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetSelection(out uint index);
        [PreserveSig] int GetString(uint index, [MarshalAs(UnmanagedType.BStr)] out string text);
    }
}
