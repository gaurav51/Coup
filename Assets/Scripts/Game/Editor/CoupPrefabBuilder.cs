using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Coup.Core;
using Coup.Game;
using Coup.Game.UI;

namespace Coup.EditorTools
{
    /// <summary>
    /// Generates the reusable UI prefabs. Building them in code keeps every card and
    /// seat identical and lets the whole set be regenerated after an art change,
    /// rather than drifting apart through hand edits.
    /// </summary>
    public static class CoupPrefabBuilder
    {
        private const string PrefabDir = "Assets/Prefabs";
        private const string ArtAsset = "Assets/Art/CoupArt.asset";

        public static string CardPath => $"{PrefabDir}/Card.prefab";
        public static string PlayerPanelPath => $"{PrefabDir}/PlayerPanel.prefab";
        public static string ActionButtonPath => $"{PrefabDir}/ActionButton.prefab";

        private static readonly Color PanelFill = new Color(0.047f, 0.071f, 0.129f, 0.92f);
        private static readonly Color PanelFillSoft = new Color(0.078f, 0.110f, 0.196f, 0.95f);

        /// <summary>
        /// Recreates the prefabs from scratch. This DISCARDS any hand edits made to
        /// them, so it asks first and is deliberately left out of the scene build.
        /// </summary>
        [MenuItem("Coup/Generate Prefabs", priority = 11)]
        public static void GenerateAll()
        {
            if (File.Exists(CardPath) &&
                !EditorUtility.DisplayDialog(
                    "Regenerate prefabs?",
                    "This overwrites Card, PlayerPanel and ActionButton in Assets/Prefabs, " +
                    "discarding any changes made to them by hand.\n\nContinue?",
                    "Overwrite", "Cancel"))
                return;

            Directory.CreateDirectory(PrefabDir);
            var art = AssetDatabase.LoadAssetAtPath<CoupArt>(ArtAsset);
            if (art == null)
            {
                Debug.LogError("[Coup] Build the art library first (Coup > Rebuild Art Library).");
                return;
            }

            BuildCard(art);
            BuildPlayerPanel(art);
            BuildActionButton(art);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Coup] Prefabs generated in {PrefabDir}.");
        }

        // ------------------------------------------------------------------ card

        private static void BuildCard(CoupArt art)
        {
            var root = NewUI("Card", new Vector2(300, 440));
            var view = root.AddComponent<CardView>();
            var group = root.AddComponent<CanvasGroup>();
            AddRaycastTarget(root);

            // Rounded mask so the faction backdrop takes the card's shape.
            var clip = NewUI("Clip", Vector2.zero, root.transform);
            Stretch(clip, 0);
            var clipImg = clip.AddComponent<Image>();
            clipImg.sprite = art.roundedPanel;
            clipImg.type = Image.Type.Sliced;
            clipImg.color = Color.black;
            var mask = clip.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var backdrop = NewImage("Backdrop", clip.transform, null, preserveAspect: false);
            Stretch(backdrop.gameObject, 0);

            var portrait = NewImage("Portrait", clip.transform, null, preserveAspect: true);
            var prt = (RectTransform)portrait.transform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.offsetMin = new Vector2(-6, 34);   // sit the figure above the caption
            prt.offsetMax = new Vector2(6, 14);

            // Everything below is anchored in fractions of the card so one prefab
            // serves the table, the seat thumbnails and the reference screen.
            var scrim = NewImage("Scrim", clip.transform, art.bottomScrim, preserveAspect: false);
            Fraction(scrim.gameObject, 0f, 0f, 1f, 0.44f, 0f);

            var nameText = NewText("Name", clip.transform, "ROLE", 34, TextAlignmentOptions.Center);
            Fraction(nameText.gameObject, 0f, 0.20f, 1f, 0.33f, 10f);
            nameText.fontStyle = FontStyles.Bold;
            nameText.characterSpacing = 4f;
            AutoSize(nameText, 9f, 34f);

            var ability = NewText("Ability", clip.transform, "", 21, TextAlignmentOptions.Top);
            Fraction(ability.gameObject, 0f, 0.03f, 1f, 0.19f, 14f);
            ability.color = new Color(0.88f, 0.86f, 0.80f);
            AutoSize(ability, 7f, 21f);

            var frame = NewImage("Frame", root.transform, art.roundedBorder, preserveAspect: false);
            frame.type = Image.Type.Sliced;
            Stretch(frame.gameObject, 0);

            var glow = NewImage("Glow", root.transform, art.roundedBorder, preserveAspect: false);
            glow.type = Image.Type.Sliced;
            Stretch(glow.gameObject, -10);
            glow.color = new Color(1f, 0.85f, 0.4f, 0f);
            glow.gameObject.SetActive(false);

            var back = NewImage("Back", root.transform, art.cardBack, preserveAspect: false);
            Stretch(back.gameObject, 0);
            back.gameObject.SetActive(false);

            view.root = (RectTransform)root.transform;
            view.group = group;
            view.backdrop = backdrop;
            view.portrait = portrait;
            view.scrim = scrim;
            view.frame = frame;
            view.glow = glow;
            view.back = back.gameObject;
            view.nameText = nameText;
            view.abilityText = ability;

            Save(root, CardPath);
        }

        // ---------------------------------------------------------- player panel

        private static void BuildPlayerPanel(CoupArt art)
        {
            var root = NewUI("PlayerPanel", new Vector2(300, 300));
            var view = root.AddComponent<PlayerPanelView>();
            var group = root.AddComponent<CanvasGroup>();
            AddRaycastTarget(root);

            var background = NewImage("Background", root.transform, art.roundedPanel, preserveAspect: false);
            background.type = Image.Type.Sliced;
            background.color = PanelFill;
            Stretch(background.gameObject, 0);

            var border = NewImage("Border", root.transform, art.roundedBorder, preserveAspect: false);
            border.type = Image.Type.Sliced;
            border.color = new Color(0.84f, 0.69f, 0.33f, 0.75f);
            Stretch(border.gameObject, 0);

            var turnGlow = NewImage("TurnGlow", root.transform, art.roundedBorder, preserveAspect: false);
            turnGlow.type = Image.Type.Sliced;
            turnGlow.color = new Color(1f, 0.86f, 0.42f, 1f);
            Stretch(turnGlow.gameObject, -8);
            turnGlow.gameObject.SetActive(false);

            var targetGlow = NewImage("TargetGlow", root.transform, art.roundedBorder, preserveAspect: false);
            targetGlow.type = Image.Type.Sliced;
            targetGlow.color = new Color(0.85f, 0.22f, 0.25f, 1f);
            Stretch(targetGlow.gameObject, -14);
            targetGlow.gameObject.SetActive(false);

            var avatar = NewImage("Avatar", root.transform, art.roundedPanel, preserveAspect: false);
            avatar.type = Image.Type.Sliced;
            avatar.color = PanelFillSoft;
            Anchor(avatar.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -58), new Vector2(92, 92));

            var initial = NewText("AvatarInitial", root.transform, "?", 44, TextAlignmentOptions.Center);
            Anchor(initial.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -58), new Vector2(92, 92));
            initial.color = UIFactory.Gold;
            initial.fontStyle = FontStyles.Bold;

            var nameText = NewText("Name", root.transform, "Player", 28, TextAlignmentOptions.Center);
            Anchor(nameText.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -128), new Vector2(270, 38));

            var coinIcon = NewImage("CoinIcon", root.transform, art.coin, preserveAspect: true);
            Anchor(coinIcon.gameObject, new Vector2(0.5f, 1f), new Vector2(-38, -168), new Vector2(34, 34));

            var coinText = NewText("Coins", root.transform, "0", 30, TextAlignmentOptions.Left);
            Anchor(coinText.gameObject, new Vector2(0.5f, 1f), new Vector2(28, -168), new Vector2(72, 40));
            coinText.color = UIFactory.Gold;
            coinText.fontStyle = FontStyles.Bold;

            // Last claim: the single most useful thing to see on an opponent.
            var claimBadge = NewUI("ClaimBadge", new Vector2(250, 40), root.transform);
            Anchor(claimBadge, new Vector2(0.5f, 1f), new Vector2(0, -212), new Vector2(252, 38));
            claimBadge.AddComponent<CanvasGroup>();
            var claimBg = NewImage("Bg", claimBadge.transform, art.roundedPanel, preserveAspect: false);
            claimBg.type = Image.Type.Sliced;
            claimBg.color = new Color(0f, 0f, 0f, 0.65f);
            Stretch(claimBg.gameObject, 0);
            var claimText = NewText("Text", claimBadge.transform, "", 20, TextAlignmentOptions.Center);
            Stretch(claimText.gameObject, 0);
            claimBadge.SetActive(false);

            var slots = NewUI("CardSlots", new Vector2(280, 110), root.transform);
            Anchor(slots, new Vector2(0.5f, 0f), new Vector2(0, -30), new Vector2(280, 104));

            view.root = (RectTransform)root.transform;
            view.group = group;
            view.background = background;
            view.border = border;
            view.turnGlow = turnGlow;
            view.targetGlow = targetGlow;
            view.avatarInitial = initial;
            view.nameText = nameText;
            view.coinText = coinText;
            view.coinIcon = coinIcon;
            view.cardSlots = (RectTransform)slots.transform;
            view.claimBadge = claimBadge;
            view.claimText = claimText;

            Save(root, PlayerPanelPath);
        }

        // --------------------------------------------------------- action button

        private static void BuildActionButton(CoupArt art)
        {
            var root = NewUI("ActionButton", new Vector2(140, 168));
            var view = root.AddComponent<ActionButtonView>();
            var group = root.AddComponent<CanvasGroup>();
            AddRaycastTarget(root);

            var background = NewImage("Background", root.transform, art.roundedPanel, preserveAspect: false);
            background.type = Image.Type.Sliced;
            background.color = PanelFill;
            Stretch(background.gameObject, 0);

            var border = NewImage("Border", root.transform, art.roundedBorder, preserveAspect: false);
            border.type = Image.Type.Sliced;
            border.color = UIFactory.Gold;
            Stretch(border.gameObject, 0);

            var icon = NewImage("Icon", root.transform, null, preserveAspect: true);
            Anchor(icon.gameObject, new Vector2(0.5f, 1f), new Vector2(0, -58), new Vector2(74, 74));

            var label = NewText("Label", root.transform, "Action", 20, TextAlignmentOptions.Center);
            Anchor(label.gameObject, new Vector2(0.5f, 0f), new Vector2(0, 54), new Vector2(136, 30));
            label.fontStyle = FontStyles.Bold;

            var cost = NewText("Cost", root.transform, "", 16, TextAlignmentOptions.Center);
            Anchor(cost.gameObject, new Vector2(0.5f, 0f), new Vector2(0, 26), new Vector2(136, 24));
            cost.color = new Color(0.78f, 0.74f, 0.64f);

            var locked = NewImage("Locked", root.transform, art.roundedPanel, preserveAspect: false);
            locked.type = Image.Type.Sliced;
            locked.color = new Color(0f, 0f, 0f, 0.45f);
            Stretch(locked.gameObject, 0);
            locked.gameObject.SetActive(false);

            view.root = (RectTransform)root.transform;
            view.group = group;
            view.background = background;
            view.border = border;
            view.icon = icon;
            view.label = label;
            view.cost = cost;
            view.lockIcon = locked;

            Save(root, ActionButtonPath);
        }

        // --------------------------------------------------------------- helpers

        private static GameObject NewUI(string name, Vector2 size, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            return go;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, bool preserveAspect)
        {
            var go = NewUI(name, new Vector2(100, 100), parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string content,
                                               float size, TextAlignmentOptions align)
        {
            var go = NewUI(name, new Vector2(200, 40), parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = size;
            t.alignment = align;
            t.color = UIFactory.Parchment;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>An invisible graphic so the root itself receives pointer events.</summary>
        private static void AddRaycastTarget(GameObject go)
        {
            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0);
            img.raycastTarget = true;
        }

        /// <summary>Anchors to a fraction of the parent so the element scales with it.</summary>
        private static void Fraction(GameObject go, float xMin, float yMin, float xMax, float yMax, float padX)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padX, 0);
            rt.offsetMax = new Vector2(-padX, 0);
        }

        /// <summary>Lets text shrink to fit rather than clipping its own name.</summary>
        private static void AutoSize(TextMeshProUGUI text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
            text.overflowMode = TextOverflowModes.Truncate;
        }

        private static void Stretch(GameObject go, float padding)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>
        /// Anchors to an edge but keeps the pivot centred, so `pos` is the offset of
        /// the element's centre. Pivoting on the edge instead makes every offset
        /// measure from a different place per element, and they silently overlap.
        /// </summary>
        private static void Anchor(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
