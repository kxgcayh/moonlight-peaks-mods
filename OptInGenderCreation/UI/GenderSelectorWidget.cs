using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OptInGenderCreation.UI
{
    public sealed class StyleBannerGraphic : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            float tip = rect.height * 0.5f;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax - tip, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.center.y), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax - tip, rect.yMin), color, Vector2.zero);
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(0, 2, 4);
            mesh.AddTriangle(2, 3, 4);
        }
    }

    public class GenderSelectorWidget : MonoBehaviour
    {
        public static GenderSelectorWidget? Instance { get; private set; }
        private GenderMode currentMode;
        private readonly List<Choice> choices = new List<Choice>();
        public GenderMode CurrentMode => currentMode;
        public event Action<GenderMode>? OnGenderChanged;

        private sealed class Choice
        {
            public GenderMode Mode;
            public GameObject Indicator = null!;
            public TMP_Text Label = null!;
            public Button Button = null!;
        }

        private void Awake() => Instance = this;
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Initialize(GenderMode initialMode, Transform screenRoot, TMP_Text? fontTemplate)
        {
            currentMode = initialMode;
            var root = gameObject.AddComponent<RectTransform>();
            transform.SetParent(screenRoot, false);
            Place(root, 32f, 48f, 218f, 190f);

            var heading = MakeText("Heading", transform, "Style", fontTemplate, 18f);
            Place(heading.rectTransform, 0f, 0f, 210f, 25f);
            CreateChoice(GenderMode.Default, "Default", fontTemplate, 35f);
            CreateChoice(GenderMode.Male, "Male", fontTemplate, 91f);
            CreateChoice(GenderMode.Female, "Female", fontTemplate, 147f);
            UpdateSelectionVisuals();
        }

        private static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static TMP_Text MakeText(string name, Transform parent, string value, TMP_Text? template, float size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<TextMeshProUGUI>();
            if (template != null) text.font = template.font;
            text.fontSize = size;
            text.color = new Color(0.98f, 0.88f, 0.55f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private void CreateChoice(GenderMode mode, string label, TMP_Text? template, float top)
        {
            var row = new GameObject($"Style_{mode}", typeof(RectTransform), typeof(StyleBannerGraphic), typeof(Button));
            row.transform.SetParent(transform, false);
            Place(row.GetComponent<RectTransform>(), 0f, top, 218f, 44f);
            var banner = row.GetComponent<StyleBannerGraphic>();
            banner.color = Color.white;
            var button = row.GetComponent<Button>();
            button.targetGraphic = banner;
            button.onClick.AddListener(() => SetMode(mode));

            var box = new GameObject("SelectionBox", typeof(RectTransform), typeof(Image), typeof(Outline));
            box.transform.SetParent(row.transform, false);
            Place(box.GetComponent<RectTransform>(), 2f, 3f, 38f, 38f);
            var boxImage = box.GetComponent<Image>();
            boxImage.color = new Color(0.13f, 0.07f, 0.24f);
            boxImage.raycastTarget = false;
            var outline = box.GetComponent<Outline>();
            outline.effectColor = new Color(0.46f, 0.29f, 0.68f);
            outline.effectDistance = new Vector2(2f, -2f);

            var indicator = new GameObject("Selected", typeof(RectTransform), typeof(Image));
            indicator.transform.SetParent(box.transform, false);
            Place(indicator.GetComponent<RectTransform>(), 10f, 10f, 18f, 18f);
            var indicatorImage = indicator.GetComponent<Image>();
            indicatorImage.color = new Color(0.98f, 0.88f, 0.55f);
            indicatorImage.raycastTarget = false;

            var text = MakeText("Label", row.transform, label, template, 25f);
            Place(text.rectTransform, 60f, 0f, 133f, 44f);
            choices.Add(new Choice { Mode = mode, Indicator = indicator, Label = text, Button = button });
        }

        public void SetMode(GenderMode mode)
        {
            if (currentMode == mode) return;
            currentMode = mode;
            UpdateSelectionVisuals();
            OnGenderChanged?.Invoke(mode);
        }

        private void UpdateSelectionVisuals()
        {
            foreach (var choice in choices)
            {
                bool selected = choice.Mode == currentMode;
                choice.Indicator.SetActive(selected);
                choice.Label.color = selected ? new Color(1f, 0.93f, 0.65f) : new Color(0.93f, 0.82f, 0.50f);
                var colors = choice.Button.colors;
                colors.normalColor = selected ? new Color(0.36f, 0.17f, 0.57f) : new Color(0.23f, 0.10f, 0.40f);
                colors.highlightedColor = colors.selectedColor = new Color(0.45f, 0.24f, 0.65f);
                colors.pressedColor = new Color(0.52f, 0.30f, 0.70f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.1f;
                choice.Button.colors = colors;
            }
        }
    }
}
