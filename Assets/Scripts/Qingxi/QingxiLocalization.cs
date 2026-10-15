using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Qingxi
{
    /// <summary>Reads the official String Tables. Chinese source text is the stable entry key.</summary>
    public static class QingxiLocalization
    {
        public const string TableName = "Qingxi";
        public const string PreferenceKey = "Qingxi.Language";
        public static readonly string[] LocaleCodes = { "zh-Hans", "en" };
        static readonly Dictionary<string, AsyncOperationHandle<StringTable>> Tables = new Dictionary<string, AsyncOperationHandle<StringTable>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Tables.Clear();

        internal static bool HasTable(string code) => Tables.TryGetValue(code, out var handle) && handle.IsValid() && handle.Result != null;

        internal static void Register(AsyncOperationHandle<StringTable> handle)
        {
            string code = handle.Result.LocaleIdentifier.Code;
            if (HasTable(code)) return;
            // StringDatabase releases its own references on locale changes. Retain ours until quit.
            Tables[code] = Addressables.ResourceManager.Acquire(handle);
        }

        internal static void ReleaseTables()
        {
            foreach (var handle in Tables.Values) if (handle.IsValid()) Addressables.Release(handle);
            Tables.Clear();
        }

        public static string Text(string source)
        {
            if (string.IsNullOrEmpty(source) || !Application.isPlaying || Tables.Count == 0) return source;
            var locale = LocalizationSettings.SelectedLocale;
            if (locale != null && Tables.TryGetValue(locale.Identifier.Code, out var handle) && handle.IsValid() && handle.Result != null)
            {
                var table = handle.Result;
                var entry = table.GetEntry(source);
                if (entry != null) return entry.LocalizedValue;
                Debug.LogError("Missing Qingxi localization entry: " + source);
            }
            return source;
        }

        public static string Format(string source, params object[] values) =>
            string.Format(CultureInfo.InvariantCulture, Text(source), values);
    }
}
