using System.Reflection;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0079 // 请删除不必要的忽略
#pragma warning disable SYSLIB0025 // 类型或成员已过时
[assembly: SuppressIldasm]
#pragma warning restore SYSLIB0025 // 类型或成员已过时
#pragma warning restore IDE0079 // 请删除不必要的忽略
[assembly: AssemblyTitle("iOS Backup SteamGuard Authenticator Data Extractor")]
[assembly: AssemblyDescription("Now greatly simplified the process of extracting your Steamguard Authenticator from your iPhone / iPad, for use in WinAuth / Steam++.")]
[assembly: AssemblyFileVersion(BuildConfig.Version)]
[assembly: AssemblyInformationalVersion(BuildConfig.Version)]
[assembly: AssemblyVersion(BuildConfig.Version)]

static class BuildConfig
{
    public const string Version = "1.4.0";
}