using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PlanetbaseTrainer.Core
{
    /// <summary>
    /// Workaround for a known crash when the game exits with BepInEx 5 on 32-bit Unity 2022.3
    /// (BepInEx issue #398, root cause found in MonoMod issue #329, fixed upstream in MonoMod PR #330 but not in the
    /// MonoMod 22.x bundled with BepInEx 5).
    ///
    /// MonoMod's <c>SetMonoCorlibInternal</c> computes the offset of <c>MonoAssembly.corlib_internal</c> for an older
    /// layout: on this runtime it writes its "1" at +0x4F instead of +0x53, i.e. into the high byte of
    /// <c>friend_assembly_names</c> (+0x4C). The null list pointer of every assembly MonoMod generates becomes
    /// 0x01000000, which Mono dereferences in <c>mono_domain_free</c> at shutdown: the game crashes after it has quit.
    ///
    /// Just before shutdown, this restores those pointers to null. Guarded three ways: 32-bit Mono only, Unity 2022.3
    /// only, and a pointer is only written if it holds exactly the corrupted value.
    /// </summary>
    internal static class MonoExitCrashFix
    {
        private const int FriendAssemblyNamesOffset = 0x4C;
        private const int CorruptedPointer = 0x01000000;

        /// <summary>Returns the number of repaired assemblies.</summary>
        public static int Apply()
        {
            if (IntPtr.Size != 4 || Type.GetType("Mono.Runtime") == null ||
                !Application.unityVersion.StartsWith("2022.3", StringComparison.Ordinal))
                return 0;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            int repaired = 0;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var runtimeType = assembly.GetType();
                    var field = runtimeType.GetField("_mono_assembly", flags) ?? runtimeType.GetField("dynamic_assembly", flags);
                    if (field == null)
                        continue;
                    var native = ToPointer(field.GetValue(assembly));
                    if (native == IntPtr.Zero || Marshal.ReadInt32(native, FriendAssemblyNamesOffset) != CorruptedPointer)
                        continue;
                    Marshal.WriteInt32(native, FriendAssemblyNamesOffset, 0);
                    repaired++;
                }
                catch (Exception)
                {
                    // Best effort: never let a diagnostic workaround interfere with the shutdown.
                }
            }
            return repaired;
        }

        private static IntPtr ToPointer(object value)
        {
            switch (value)
            {
                case IntPtr pointer:
                    return pointer;
                case UIntPtr unsignedPointer:
                    return new IntPtr((long)unsignedPointer.ToUInt64());
                default:
                    return IntPtr.Zero;
            }
        }
    }
}
