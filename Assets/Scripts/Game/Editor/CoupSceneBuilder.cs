using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Coup.Core;
using Coup.Game;
using Coup.Game.UI;

namespace Coup.EditorTools
{
    /// <summary>
    /// Generates the art library and the playable table scene from code, so the
    /// layout is reviewable in version control and can be rebuilt after art changes
    /// instead of being repaired by hand in the inspector.
    /// </summary>
    public static class CoupSceneBuilder
    {
        private const string Individual = "Assets/Sprites/01_Individual_Sprites";
        private const string Characters = "Assets/Sprites/04_Characters_And_Art_Reference";
        private const string Backgrounds = "Assets/Sprites/02_Backgrounds";
        private const string Generated = "Assets/Art/Generated";
        private const string ActionCallouts = "Assets/Sprites/Actions";
        private const string ArtAssetPath = "Assets/Art/CoupArt.asset";
        private const string ScenePath = "Assets/Scenes/Game.unity";

        /// <summary>
        /// Rebuilds the derived art, the library and the scene — but deliberately
        /// NOT the prefabs. The prefabs are hand-tuned after generation, and
        /// regenerating them here would silently discard that work every time the
        /// scene was rebuilt. Run "Coup > Generate Prefabs" explicitly to recreate
        /// them from scratch.
        /// </summary>
        [MenuItem("Coup/Build Game Scene", priority = 0)]
        public static void BuildAll()
        {
            CoupArtGenerator.GenerateAll();
            var art = BuildArtLibrary();
            BuildScene(art);
        }

        // ------------------------------------------------------------ art library

        [MenuItem("Coup/Rebuild Art Library", priority = 20)]
        public static CoupArt BuildArtLibrary()
        {
            Directory.CreateDirectory("Assets/Art");

            var art = AssetDatabase.LoadAssetAtPath<CoupArt>(ArtAssetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<CoupArt>();
                AssetDatabase.CreateAsset(art, ArtAssetPath);
            }

            art.roles = new[]
            {
                Role(Core.Role.Duke, "Duke", "Take 3 coins.\nBlocks foreign aid.", "#7B3FA0", "DukeEffect"),
                Role(Core.Role.Captain, "Captain", "Steal 2 coins.\nBlocks stealing.", "#1E3A6E", "Captain Effect"),
                Role(Core.Role.Assassin, "Assassin", "Pay 3 coins to assassinate.", "#1A1F38", "Assasin Effect"),
                Role(Core.Role.Contessa, "Contessa", "Blocks assassination.", "#B52430", "Contessa Effect"),
                Role(Core.Role.Ambassador, "Ambassador", "Exchange cards.\nBlocks stealing.", "#D4791E", "Ambassador Effect")
            };

            art.actions = new[]
            {
                Action(ActionKind.Income, "Income", "+1 Coin", "Action_Income", null),
                Action(ActionKind.ForeignAid, "Foreign Aid", "+2 Coins", "Action_Foreign_Aid", null),
                Action(ActionKind.Tax, "Tax", "Take 3 Coins", "Action_Tax", "Tax Effect"),
                Action(ActionKind.Steal, "Steal", "Take 2 Coins", "Action_Steal", "Steal Effect"),
                Action(ActionKind.Assassinate, "Assassinate", "Pay 3 Coins", "Action_Assassinate", "Assasinate Effect"),
                Action(ActionKind.Exchange, "Exchange", "Swap Cards", "Action_Exchange", "Exchange effect"),
                Action(ActionKind.Coup, "Coup", "Pay 7 Coins", "Action_Coup", "Coup Effect")
            };

            art.roundedPanel = Load($"{Generated}/Panel_Rounded.png");
            art.roundedBorder = Load($"{Generated}/Panel_Border.png");
            art.bottomScrim = Load($"{Generated}/Scrim_Bottom.png");
            art.vfxSpark = Load($"{Generated}/Vfx_Spark.png");
            art.vfxRing = Load($"{Generated}/Vfx_Ring.png");

            art.cardBack = Load($"{Individual}/Cards/Card_Back.png");
            art.opponentCardBack = Load($"{Individual}/Cards/Opponent_Card_Back_1.png");
            art.drawDeck = Load($"{Individual}/Cards/Draw_Deck_With_Blank_Labels.png");
            art.discardDeck = Load($"{Individual}/Cards/Discard_Deck_With_Blank_Labels.png");

            art.tableBackground = Load($"{Backgrounds}/celestial_gold_trimmed_marble_arena.png");
            art.coin = Load($"{Individual}/Coins/Single_Gold_Coin.png");
            art.coinPile = Load($"{Individual}/Coins/Coin_Pile_With_Blank_Labels.png");

            art.opponentFrame = Load($"{Individual}/HUD/Opponent_Info_Frame_With_Transparent_Avatar.png");
            art.localHudFrame = Load($"{Individual}/HUD/Local_Player_HUD_Frame.png");
            art.roundBanner = Load($"{Individual}/HUD/Round_Status_Blank.png");
            art.blankButton = Load($"{Individual}/Buttons/Reusable_Blank_Button_Background.png");

            art.blockButton = Load($"{Individual}/Buttons/Block_Button.png");
            art.challengeButton = Load($"{Individual}/Buttons/Challenge_Button.png");
            art.challengeCallout = Load($"{ActionCallouts}/Challenge.png");
            art.influenceLostCallout = Load($"{ActionCallouts}/Influence Lost effect.png");
            art.menuButton = Load($"{Individual}/Buttons/Menu_Button.png");
            art.settingsButton = Load($"{Individual}/Buttons/Settings_Button.png");

            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Coup] Art library rebuilt at {ArtAssetPath}.");
            return art;
        }

        private static CoupArt.RoleArt Role(Role role, string name, string ability, string hex, string calloutFile)
        {
            ColorUtility.TryParseHtmlString(hex, out var colour);
            return new CoupArt.RoleArt
            {
                role = role,
                displayName = name.ToUpperInvariant(),
                ability = ability,
                accent = colour,
                cardFace = Load($"{Characters}/Card_{role}.png"),
                portrait = Load($"{Generated}/Portrait_{role}.png"),
                backdrop = Load($"{Characters}/Backdrop_{role}.png"),
                callout = Load($"{ActionCallouts}/{calloutFile}.png")
            };
        }

        private static CoupArt.ActionArt Action(ActionKind kind, string label, string subtitle, string file, string calloutFile) =>
            new CoupArt.ActionArt
            {
                action = kind,
                label = label,
                subtitle = subtitle,
                icon = Load($"{Individual}/Buttons/{file}.png"),
                callout = string.IsNullOrEmpty(calloutFile) ? null : Load($"{ActionCallouts}/{calloutFile}.png")
            };

        private static Sprite Load(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[Coup] Missing sprite: {path}");
            return sprite;
        }

        // ------------------------------------------------------------------ scene

        [MenuItem("Coup/Build Game Scene (scene only)", priority = 21)]
        public static void BuildSceneOnly() =>
            BuildScene(AssetDatabase.LoadAssetAtPath<CoupArt>(ArtAssetPath));

        private static void BuildScene(CoupArt art)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera — the HUD renders in overlay, so this only paints the backdrop.
            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
            cam.orthographic = true;
            camGo.transform.position = new Vector3(0, 0, -10);

            BuildEventSystem();

            // Canvas — portrait 1080x1920, scaled by width so the phone-width layout holds.
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // match width: never clip the layout horizontally

            var match = new GameObject("Match", typeof(MatchController)).GetComponent<MatchController>();
            match.playerCount = 6;
            match.humanSeat = 0;

            var tableGo = new GameObject("Table", typeof(RectTransform), typeof(TableView));
            var rt = (RectTransform)tableGo.transform;
            rt.SetParent(canvasGo.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var table = tableGo.GetComponent<TableView>();
            table.art = art;
            table.match = match;
            table.cardPrefab = AssetDatabase.LoadAssetAtPath<Coup.Game.UI.CardView>(CoupPrefabBuilder.CardPath);
            table.playerPanelPrefab = AssetDatabase.LoadAssetAtPath<Coup.Game.UI.PlayerPanelView>(CoupPrefabBuilder.PlayerPanelPath);
            table.actionButtonPrefab = AssetDatabase.LoadAssetAtPath<Coup.Game.UI.ActionButtonView>(CoupPrefabBuilder.ActionButtonPath);

            if (table.cardPrefab == null || table.playerPanelPrefab == null || table.actionButtonPrefab == null)
                Debug.LogError("[Coup] Prefabs missing — run Coup > Generate Prefabs.");

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);

            Debug.Log($"[Coup] Scene built at {ScenePath}.");
            EditorSceneManager.OpenScene(ScenePath);
        }

        private static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
