using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using CardDuel.Presentation;
using UnityEngine;

namespace CardDuel.Learning
{
    public sealed partial class TeachingGameScreen
    {
        private static UserSettings LoadSettings(string key)
        {
            bool alreadySaved = PlayerPrefs.HasKey(key);
            string json = alreadySaved ? PlayerPrefs.GetString(key) : ReadPreviousProductSettings(key);
            if (string.IsNullOrEmpty(json)) return new UserSettings();

            UserSettings stored;
            try { stored = JsonUtility.FromJson<UserSettings>(json); }
            catch (ArgumentException) { return new UserSettings(); }
            if (stored == null) return new UserSettings();

            stored.Sanitize();
            if (!alreadySaved)
            {
                PlayerPrefs.SetString(key, JsonUtility.ToJson(stored));
                PlayerPrefs.Save();
            }
            return stored;
        }

        private static string ReadPreviousProductSettings(string key)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Windows Unity stores PlayerPrefs under the product name with a hashed value suffix.
            const string previousProduct = @"DefaultCompany\big_old_guy";
            string root = Application.isEditor ? @"Software\Unity\UnityEditor\" : @"Software\";
            uint hash = 5381;
            foreach (char character in key)
                hash = unchecked((hash * 33) ^ character);
            string value = key + "_h" + hash.ToString(CultureInfo.InvariantCulture);
            IntPtr currentUser = new IntPtr(unchecked((int)0x80000001));
            const uint stringOrBinary = 0x0A;
            uint length = 0;
            if (RegGetValue(currentUser, root + previousProduct, value, stringOrBinary, out _, null, ref length) != 0 ||
                length == 0 || length > 65536)
                return null;

            var bytes = new byte[length];
            if (RegGetValue(currentUser, root + previousProduct, value, stringOrBinary, out uint type, bytes, ref length) != 0)
                return null;
            Encoding encoding = type == 1 ? Encoding.Unicode : Encoding.UTF8;
            return encoding.GetString(bytes, 0, (int)length).TrimEnd('\0');
#else
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("advapi32.dll", EntryPoint = "RegGetValueW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int RegGetValue(IntPtr key, string subkey, string value, uint flags,
            out uint type, [Out] byte[] data, ref uint length);
#endif
    }
}
