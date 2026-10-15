using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Qingxi
{
    /// <summary>Inspector-editable Unity LocalizedString binding for a static UGUI label.</summary>
    [RequireComponent(typeof(Text)), DisallowMultipleComponent]
    public sealed class QingxiLocalizedText : MonoBehaviour
    {
        [SerializeField] LocalizedString localizedString = new LocalizedString();
        public LocalizedString StringReference => localizedString;

        public void Configure(string source) => localizedString = new LocalizedString(QingxiLocalization.TableName, source);

        void OnEnable()
        {
            if (!localizedString.IsEmpty) localizedString.StringChanged += Apply;
        }

        void OnDisable()
        {
            if (!localizedString.IsEmpty) localizedString.StringChanged -= Apply;
        }

        void Apply(string value) => GetComponent<Text>().text = value;
    }
}
