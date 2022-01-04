using System.Reflection;
using System.Runtime.CompilerServices;
using static iOSSteamGuardExtractor.BuildConfig;

#pragma warning disable SYSLIB0025 // 类型或成员已过时
[assembly: SuppressIldasm]
#pragma warning restore SYSLIB0025 // 类型或成员已过时
[assembly: AssemblyTitle("iOS Backup SteamGuard Authenticator Data Extractor")]
[assembly: AssemblyDescription("Now greatly simplified the process of extracting your Steamguard Authenticator from your iPhone / iPad, for use in WinAuth / Steam++.")]
[assembly: AssemblyFileVersion(Version)]
[assembly: AssemblyInformationalVersion(Version)]
[assembly: AssemblyVersion(Version)]

namespace iOSSteamGuardExtractor
{
    public static class BuildConfig
    {
        public const string Version = "1.3.0";
    }
}