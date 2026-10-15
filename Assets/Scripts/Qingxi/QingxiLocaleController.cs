using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Qingxi
{
    /// <summary>Initializes Localization and connects the authored language dropdown to SelectedLocale.</summary>
    [RequireComponent(typeof(QingxiGame)), DisallowMultipleComponent]
    public sealed class QingxiLocaleController : MonoBehaviour
    {
        [SerializeField] Dropdown languageDropdown;
        QingxiGame game;
        public bool IsReady { get; private set; }
        public Dropdown LanguageDropdown => languageDropdown;

        public void Configure(Dropdown dropdown) => languageDropdown = dropdown;

        void Awake()
        {
            game = GetComponent<QingxiGame>();
            if (languageDropdown != null)
            {
                languageDropdown.interactable = false;
                languageDropdown.onValueChanged.AddListener(SelectLanguage);
            }
        }

        void OnEnable() => LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        void OnApplicationQuit() => QingxiLocalization.ReleaseTables();

        void OnDestroy()
        {
            if (languageDropdown != null) languageDropdown.onValueChanged.RemoveListener(SelectLanguage);
        }

        IEnumerator Start()
        {
            var initialization = LocalizationSettings.InitializationOperation;
            yield return initialization;
            if (initialization.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Qingxi Localization initialization failed.", this);
                yield break;
            }
            // Both tiny tables stay in StringDatabase's cache, so switching never blocks a frame.
            foreach (string code in QingxiLocalization.LocaleCodes)
            {
                if (QingxiLocalization.HasTable(code)) continue;
                var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                if (locale == null) { Debug.LogError("Missing Qingxi locale: " + code, this); yield break; }
                var loading = LocalizationSettings.StringDatabase.GetTableAsync(QingxiLocalization.TableName, locale);
                yield return loading;
                if (loading.Status != AsyncOperationStatus.Succeeded || loading.Result == null)
                {
                    Debug.LogError("Missing Qingxi String Table: " + code, this);
                    yield break;
                }
                QingxiLocalization.Register(loading);
            }
            IsReady = true;
            OnLocaleChanged(LocalizationSettings.SelectedLocale);
            if (languageDropdown != null) languageDropdown.interactable = true;
        }

        public void SelectLanguage(int index)
        {
            if (!IsReady || index < 0 || index >= QingxiLocalization.LocaleCodes.Length) return;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(QingxiLocalization.LocaleCodes[index]);
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
        }

        void OnLocaleChanged(Locale locale)
        {
            if (!IsReady || locale == null) return;
            PlayerPrefs.SetString(QingxiLocalization.PreferenceKey, locale.Identifier.Code);
            PlayerPrefs.Save();
            if (languageDropdown != null)
                languageDropdown.SetValueWithoutNotify(locale.Identifier.Code == "en" ? 1 : 0);
            game.RefreshLanguage();
        }
    }
}
