// Win32 version resource for the built exe -- csc turns these attributes into
// the FileDescription/CompanyName/ProductName/etc. fields Explorer's Details
// tab and static-analysis tools read. A raw csc build with no AssemblyInfo
// leaves that resource blank, which reputation-based AV heuristics treat as
// a signal on its own, independent of behavior.
//
// Version pulls from Version.cs (ticket 03's single source of truth) so it
// can't drift from the number in the exe's own name and header.

using System.Reflection;
using System.Runtime.InteropServices;
using ApePatcher;

[assembly: AssemblyTitle("Ape's Patch Extensions")]
[assembly: AssemblyDescription("GUI patcher for h.sie/Stiebler hardcode patch tuning in Silent Hunter 3")]
[assembly: AssemblyCompany("blinkingape")]
[assembly: AssemblyProduct("Ape's Patch Extensions")]
[assembly: AssemblyCopyright("Copyright (c) 2026 blinkingape")]
[assembly: AssemblyVersion(ApeVersion.Current)]
[assembly: AssemblyFileVersion(ApeVersion.Current)]
[assembly: ComVisible(false)]
