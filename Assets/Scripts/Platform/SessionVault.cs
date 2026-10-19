using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace Coup.Net
{
    // Refresh credentials never go into PlayerPrefs. Editor sessions stay in memory.
    public static class SessionVault
    {
        private static string _editorValue;
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern IntPtr CoupCredentialRead();
        [DllImport("__Internal")] private static extern bool CoupCredentialWrite(string value);
        [DllImport("__Internal")] private static extern void CoupCredentialFree(IntPtr value);
#endif
        public static string Read()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var store = new AndroidJavaClass("com.coup.security.SessionVault"))
                return store.CallStatic<string>("read", unity.GetStatic<AndroidJavaObject>("currentActivity"));
#elif UNITY_IOS && !UNITY_EDITOR
            var pointer = CoupCredentialRead();
            if (pointer == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringAnsi(pointer); } finally { CoupCredentialFree(pointer); }
#else
            return _editorValue;
#endif
        }
        public static void Write(string value)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var store = new AndroidJavaClass("com.coup.security.SessionVault"))
                store.CallStatic("write", unity.GetStatic<AndroidJavaObject>("currentActivity"), value ?? "");
#elif UNITY_IOS && !UNITY_EDITOR
            if (!CoupCredentialWrite(value ?? "")) throw new InvalidOperationException("Secure account storage is unavailable.");
#else
            _editorValue = value;
#endif
        }
    }
}
