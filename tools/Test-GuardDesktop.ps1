param([Parameter(Mandatory)][string]$Assembly, [Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class IsolatedFixture {
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct Startup {
  public uint Size; public string Reserved, Desktop, Title; public uint X,Y,Width,Height,XChars,YChars,Fill,Flags;
  public ushort Show, Reserved2; public IntPtr ReservedBytes, Input, Output, Error;
 }
 [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr Process, Thread; public uint Pid, Tid; }
 [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr CreateDesktopW(string name, IntPtr device, IntPtr mode, uint flags, uint access, IntPtr attributes);
 [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool CreateProcessW(string application, StringBuilder command, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref Startup startup, out ProcessInfo info);
 [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle, uint ms);
 [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr handle, out uint code);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr handle, uint code);
 public static uint Run(string executable, string arguments) {
  string name="ImeGuardTest-"+Guid.NewGuid().ToString("N");
  IntPtr desktop=CreateDesktopW(name,IntPtr.Zero,IntPtr.Zero,0,0x1FF,IntPtr.Zero);
  if(desktop==IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
  ProcessInfo child=new ProcessInfo();
  try {
   var startup=new Startup { Size=(uint)Marshal.SizeOf(typeof(Startup)), Desktop=name, Flags=1, Show=0 };
   if(!CreateProcessW(executable,new StringBuilder("\""+executable+"\" "+arguments),IntPtr.Zero,IntPtr.Zero,false,0x08000000,IntPtr.Zero,null,ref startup,out child)) throw new System.ComponentModel.Win32Exception();
   if(WaitForSingleObject(child.Process,120000)!=0) { TerminateProcess(child.Process,124); throw new Exception("Only the isolated fixture timed out"); }
   uint code; GetExitCodeProcess(child.Process,out code); return code;
  } finally { if(child.Thread!=IntPtr.Zero)CloseHandle(child.Thread); if(child.Process!=IntPtr.Zero)CloseHandle(child.Process); CloseDesktop(desktop); }
 }
}
'@
$taskDotnet = (Get-Command dotnet -ErrorAction Stop).Source
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $OutputPath) { throw 'Use a new output path so previous results cannot be mistaken for this run.' }
$code = [IsolatedFixture]::Run($taskDotnet, ('"' + $Assembly + '" --fixture "' + $OutputPath + '"'))
if (Test-Path -LiteralPath $OutputPath) { Get-Content -LiteralPath $OutputPath }
if ($code -ne 0) { throw "Isolated fixture exit code: $code" }
