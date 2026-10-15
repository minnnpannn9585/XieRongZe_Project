#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Qingxi
{
    public sealed partial class QingxiGame
    {
        /// <summary>Updates existing authored UI without rebuilding the world or its layout.</summary>
        public void EditorConfigureLocalization()
        {
            uiFont = LoadUiFont();
            var dynamicLabels = new HashSet<Text> { introChapter, introBody, headerTitle, mapCaption,
                pointsText, promptText, detailTitle, surfaceLabel, deepLabel, surfaceBody, deepBody,
                notesHeader, notebookText, resultTitle, resultComment, resultChoice, resultTruth,
                resultHint, restartLabel, journalBody, journalSummary };
            if (levelButtons != null) foreach (var level in levelButtons) dynamicLabels.Add(level.Label);
            if (choices != null) foreach (var choice in choices) dynamicLabels.Add(choice.Label);

            foreach (Text label in GetComponentsInChildren<Text>(true))
            {
                if (dynamicLabels.Contains(label) || string.IsNullOrEmpty(label.text)) continue;
                if (cleanupGame != null && cleanupGame.IsDynamicLabel(label)) continue;
                if (label.GetComponentInParent<Dropdown>(true) != null) continue;
                var binding = label.GetComponent<QingxiLocalizedText>();
                if (binding == null)
                {
                    binding = label.gameObject.AddComponent<QingxiLocalizedText>();
                    binding.Configure(label.text);
                }
            }

            var controller = GetComponent<QingxiLocaleController>();
            if (controller == null) controller = gameObject.AddComponent<QingxiLocaleController>();
            Dropdown dropdown = GetComponentInChildren<Dropdown>(true);
            if (sceneCaseIndex < 0 && dropdown == null) dropdown = BuildLanguageDropdown(menuPanel.transform);
            controller.Configure(dropdown);

            // Keep Chinese's intended sizes while allowing longer English labels to fit their cards.
            foreach (Text label in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.font == null) label.font = uiFont;
                if (label.verticalOverflow == VerticalWrapMode.Overflow) continue;
                label.resizeTextForBestFit = true;
                label.resizeTextMaxSize = label.fontSize;
                label.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(label.fontSize * 0.60f));
                UnityEditor.EditorUtility.SetDirty(label);
            }
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(controller);
        }

        Dropdown BuildLanguageDropdown(Transform parent)
        {
            CreateText(parent, "LanguageLabel", "语言", 20, Paper, TextAnchor.MiddleRight,
                new Vector2(0.60f, 0.934f), new Vector2(0.775f, 0.98f))
                .gameObject.AddComponent<QingxiLocalizedText>().Configure("语言");
            Image background = CreateImage(parent, "LanguageDropdown", Card,
                new Vector2(0.785f, 0.934f), new Vector2(0.94f, 0.98f), true);
            var dropdown = background.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = background;
            Text caption = CreateText(background.transform, "Caption", "简体中文", 22, Paper, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one);
            caption.rectTransform.offsetMin = new Vector2(18, 4);
            caption.rectTransform.offsetMax = new Vector2(-42, -4);
            CreateText(background.transform, "Arrow", "▾", 24, Gold, TextAnchor.MiddleCenter,
                new Vector2(0.85f, 0f), Vector2.one);

            Image template = CreateImage(background.transform, "Template", Panel, Vector2.zero, new Vector2(1, 0), true);
            template.rectTransform.pivot = new Vector2(0.5f, 1f);
            template.rectTransform.sizeDelta = new Vector2(0, 112);
            template.rectTransform.anchoredPosition = new Vector2(0, -6);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform viewport = CreateRect(template.transform, "Viewport", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(4, 4);
            viewport.offsetMax = new Vector2(-4, -4);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect(viewport, "Content", new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 52);
            Image item = CreateImage(content, "Item", Card, new Vector2(0, 0.5f), new Vector2(1, 0.5f), true);
            item.rectTransform.sizeDelta = new Vector2(0, 52);
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = item;
            Image check = CreateImage(item.transform, "Checkmark", Gold, new Vector2(0, 0.5f), new Vector2(0, 0.5f), false);
            check.rectTransform.sizeDelta = new Vector2(10, 10);
            check.rectTransform.anchoredPosition = new Vector2(16, 0);
            toggle.graphic = check;
            Text itemLabel = CreateText(item.transform, "ItemLabel", "English", 22, Paper, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one);
            itemLabel.rectTransform.offsetMin = new Vector2(34, 4);
            itemLabel.rectTransform.offsetMax = new Vector2(-12, -4);
            scroll.viewport = viewport;
            scroll.content = content;
            dropdown.template = template.rectTransform;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.options = new List<Dropdown.OptionData> { new Dropdown.OptionData("简体中文"), new Dropdown.OptionData("English") };
            dropdown.value = 0;
            dropdown.RefreshShownValue();
            template.gameObject.SetActive(false);
            return dropdown;
        }
    }
}
#endif
