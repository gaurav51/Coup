using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Coup.Game.UI;

namespace Coup.EditorTools
{
    /// <summary>
    /// Adds the foil-sweep overlay to the card prefab. This edits the existing prefab
    /// in place rather than regenerating it, so hand-made changes elsewhere in the
    /// card survive. Running it twice is harmless — it reuses the overlay it made.
    /// </summary>
    public static class CoupShineSetup
    {
        private const string ShaderName = "Coup/UI Card Shine";
        private const string MaterialPath = "Assets/Art/Generated/M_CardShine.mat";
        private const string ShineName = "Shine";

        [MenuItem("Coup/Add Card Shine", priority = 12)]
        public static void AddShine()
        {
            var material = EnsureMaterial();
            if (material == null) return;

            var path = CoupPrefabBuilder.CardPath;
            var root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                var card = root.GetComponent<CardView>();
                if (card == null)
                {
                    Debug.LogError("[Coup] Card.prefab has no CardView.");
                    return;
                }

                // Parented under Clip so the card's rounded mask trims the sweep,
                // and added last so it sits above the portrait.
                var clip = root.transform.Find("Clip");
                var parent = clip != null ? clip : root.transform;

                var existing = parent.Find(ShineName);
                var shineGo = existing != null ? existing.gameObject : NewShineObject(parent);

                var image = shineGo.GetComponent<Image>();
                image.sprite = null;                 // plain white; the mask gives it shape
                image.material = material;
                image.raycastTarget = false;
                image.color = Color.white;

                var rt = (RectTransform)shineGo.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.SetAsLastSibling();

                var shine = shineGo.GetComponent<CardShine>();
                if (shine == null) shine = shineGo.AddComponent<CardShine>();

                card.shine = shine;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Coup] Shine overlay added to {path}. Delete the '{ShineName}' child to remove it.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        private static GameObject NewShineObject(Transform parent)
        {
            var go = new GameObject(ShineName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>();
            return go;
        }

        private static Material EnsureMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Coup] Shader '{ShaderName}' not found. Is UICardShine.shader compiling?");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            var material = new Material(shader) { name = "M_CardShine" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            Debug.Log($"[Coup] Created {MaterialPath}.");
            return material;
        }
    }
}
