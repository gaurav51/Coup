using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>
    /// Result screen after the victory crest. Win fills the plate with the
    /// sunburst wash, a large portrait and particles. Loss stays quieter.
    /// </summary>
    public sealed class GameOverPanel : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _sheet;
        private RectTransform _portraitFrame;
        private CanvasGroup _group;
        private Image _dim;
        private Image _background;
        private Image _wash;
        private Image _border;
        private Image _portraitHalo;
        private Image _laurel;
        private Image _portraitRing;
        private RawImage _portrait;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _winner;
        private Button _retry;
        private Button _lobby;
        private Image _retryFill;
        private Image _lobbyFill;
        private TextMeshProUGUI _retryLabel;
        private TextMeshProUGUI _lobbyLabel;
        private ParticleSystem _confetti;
        private CoupArt _art;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public bool TryBind(CoupArt art)
        {
            var root = transform.Find("GameOver") as RectTransform;
            if (root == null) return false;
            if (root.Find("Sheet/Assembled") == null) return false;

            _art = art;
            _root = root;
            _group = root.GetComponent<CanvasGroup>();
            _dim = root.Find("Dim")?.GetComponent<Image>();
            _sheet = root.Find("Sheet") as RectTransform;
            if (_sheet == null) return false;

            _background = _sheet.Find("Background")?.GetComponent<Image>();
            _wash = _sheet.Find("Wash")?.GetComponent<Image>();
            _border = _sheet.Find("Border")?.GetComponent<Image>();
            _portraitFrame = _sheet.Find("Portrait") as RectTransform;
            _portraitHalo = _portraitFrame?.Find("Halo")?.GetComponent<Image>();
            _laurel = _portraitFrame?.Find("Laurel")?.GetComponent<Image>();
            _portraitRing = _portraitFrame?.Find("Ring")?.GetComponent<Image>();
            _portrait = _portraitFrame?.Find("Mask/Face")?.GetComponent<RawImage>()
                        ?? _portraitFrame?.Find("Face")?.GetComponent<RawImage>();
            _title = _sheet.Find("Title")?.GetComponent<TextMeshProUGUI>();
            _winner = _sheet.Find("Winner")?.GetComponent<TextMeshProUGUI>();
            _retry = _sheet.Find("Retry")?.GetComponent<Button>();
            _lobby = _sheet.Find("Lobby")?.GetComponent<Button>();
            _retryFill = _retry != null ? _retry.GetComponent<Image>() : null;
            _lobbyFill = _lobby != null ? _lobby.GetComponent<Image>() : null;
            _retryLabel = _retry?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            _lobbyLabel = _lobby?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            return _group != null && _title != null && _retry != null && _lobby != null && _wash != null;
        }

        public void Build(RectTransform parent, CoupArt art)
        {
            _art = art;
            var stale = parent.Find("GameOver");
            if (stale != null)
            {
                if (Application.isPlaying) Destroy(stale.gameObject);
                else DestroyImmediate(stale.gameObject);
            }

            _root = UIFactory.Rect("GameOver", parent, new Vector2(1080f, 1920f), Vector2.zero);
            _group = UIFactory.Group(_root.gameObject);

            _dim = UIFactory.Image("Dim", _root, null, new Vector2(1200f, 2100f), Vector2.zero, false);
            _dim.color = new Color(0.01f, 0.02f, 0.04f, 0.88f);
            _dim.raycastTarget = true;

            _sheet = UIFactory.Rect("Sheet", _root, new Vector2(960f, 1280f), new Vector2(0f, 20f));
            UIFactory.Rect("Assembled", _sheet, new Vector2(1f, 1f), Vector2.zero);

            _background = UIFactory.Image("Background", _sheet, art != null ? art.roundedPanel : null,
                _sheet.sizeDelta, Vector2.zero, false);
            _background.type = Image.Type.Sliced;
            _background.color = new Color(0.03f, 0.04f, 0.07f, 0.98f);

            _wash = UIFactory.Image("Wash", _sheet, null, _sheet.sizeDelta, Vector2.zero, false);
            _wash.preserveAspect = false;
            _wash.color = new Color(1f, 1f, 1f, 0f);
            _wash.raycastTarget = false;

            _border = UIFactory.Image("Border", _sheet, art != null ? art.roundedBorder : null,
                _sheet.sizeDelta, Vector2.zero, false);
            _border.type = Image.Type.Sliced;
            _border.color = new Color(1f, 0.74f, 0.22f, 1f);

            _portraitFrame = UIFactory.Rect("Portrait", _sheet, new Vector2(520f, 520f), new Vector2(0f, 250f));
            _portraitHalo = UIFactory.Image("Halo", _portraitFrame, art != null ? art.vfxSpark : null,
                new Vector2(640f, 640f), Vector2.zero);
            _portraitHalo.color = new Color(1f, 0.78f, 0.28f, 0.35f);

            _laurel = UIFactory.Image("Laurel", _portraitFrame, art != null ? art.vfxRing : null,
                new Vector2(620f, 620f), Vector2.zero);
            _laurel.color = Color.white;
            _laurel.preserveAspect = true;

            var maskRt = UIFactory.Rect("Mask", _portraitFrame, new Vector2(430f, 430f), Vector2.zero);
            var maskFill = maskRt.gameObject.AddComponent<Image>();
            maskFill.sprite = art != null ? art.vfxSpark : null;
            maskFill.color = Color.white;
            maskFill.gameObject.name = "Mask";
            maskFill.raycastTarget = false;
            var mask = maskRt.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var faceObject = UIFactory.Rect("Face", maskRt, new Vector2(430f, 430f), Vector2.zero);
            _portrait = faceObject.gameObject.AddComponent<RawImage>();
            _portrait.color = Color.white;
            _portrait.raycastTarget = false;

            _portraitRing = UIFactory.Image("Ring", _portraitFrame, art != null ? art.vfxRing : null,
                new Vector2(448f, 448f), Vector2.zero);
            _portraitRing.color = new Color(1f, 0.76f, 0.24f, 1f);

            _title = UIFactory.Text("Title", _sheet, "YOU WON", 108f,
                new Vector2(880f, 150f), new Vector2(0f, -140f));
            _title.fontStyle = FontStyles.Bold;
            _title.color = new Color(1f, 0.82f, 0.32f, 1f);
            FitDisplay(_title, 108f);

            _winner = UIFactory.Text("Winner", _sheet, "", 34f,
                new Vector2(840f, 56f), new Vector2(0f, -250f));
            _winner.fontStyle = FontStyles.Bold;
            _winner.color = Color.white;
            FitHud(_winner, 34f);

            _retry = CreateButton("Retry", new Vector2(760f, 128f), new Vector2(0f, -400f),
                new Color(0.04f, 0.38f, 0.62f, 1f), out _retryFill, out _retryLabel);
            _retryLabel.text = "PLAY AGAIN";

            _lobby = CreateButton("Lobby", new Vector2(760f, 120f), new Vector2(0f, -550f),
                new Color(0.38f, 0.24f, 0.07f, 1f), out _lobbyFill, out _lobbyLabel);
            _lobbyLabel.text = "RETURN TO LOBBY";

            _root.gameObject.SetActive(false);
        }

        public void Show(bool won, string winnerName, Texture portrait, Action retry, Action lobby, UiVfx vfx)
        {
            if (_root == null) return;
            StopConfetti();
            ApplyArt(won);

            _title.text = won ? "YOU WON" : "YOU LOSE";
            var name = string.IsNullOrWhiteSpace(winnerName) ? "A RIVAL" : winnerName.Trim();
            var local = name.Equals("You", StringComparison.OrdinalIgnoreCase);
            _winner.text = won
                ? (local ? "THE COURT IS YOURS" : $"{name.ToUpperInvariant()} RULES THE COURT")
                : (local ? "THE COURT FALLS" : $"{name.ToUpperInvariant()} WINS");
            _retryLabel.text = won ? "PLAY AGAIN" : "TRY AGAIN";
            _lobbyLabel.text = "RETURN TO LOBBY";
            FitDisplay(_title, won ? 108f : 96f);
            if (_winner != null)
            {
                _winner.gameObject.SetActive(true);
                _winner.text = won ? "Last influence standing" : _winner.text;
            }
            FitHud(_retryLabel, 28f);
            FitHud(_lobbyLabel, 26f);

            if (_portrait != null)
            {
                _portrait.texture = portrait;
                _portrait.color = portrait != null ? Color.white : new Color(0.03f, 0.045f, 0.08f, 1f);
                _portrait.uvRect = SquareCrop(portrait);
                _portrait.gameObject.SetActive(true);
            }

            if (won)
            {
                _dim.color = new Color(0.01f, 0.02f, 0.05f, 0.86f);
                _title.color = new Color(1f, 0.84f, 0.34f, 1f);
                if (_laurel != null) _laurel.color = Color.white;
                if (_portraitRing != null) _portraitRing.enabled = false;
                if (_portraitHalo != null) _portraitHalo.enabled = false;
                if (_winner != null) _winner.color = new Color(0.9f, 0.9f, 0.92f, 1f);
                StyleButton(_retryFill, Piece("BtnBlue"), new Color(0.04f, 0.4f, 0.66f, 1f), true);
                StyleButton(_lobbyFill, Piece("BtnGold"), new Color(0.72f, 0.55f, 0.16f, 1f), true);
                LayoutResult(true);
            }
            else
            {
                _dim.color = new Color(0.02f, 0.01f, 0.02f, 0.9f);
                _title.color = new Color(0.93f, 0.93f, 0.95f, 1f);
                if (_laurel != null) _laurel.color = new Color(0.62f, 0.5f, 0.34f, 0.9f);
                if (_portraitRing != null) _portraitRing.enabled = false;
                if (_portraitHalo != null) _portraitHalo.enabled = false;
                if (_winner != null) _winner.color = new Color(0.82f, 0.32f, 0.34f, 1f);
                StyleButton(_retryFill, Piece("BtnGold"), new Color(0.4f, 0.28f, 0.1f, 1f), true);
                StyleButton(_lobbyFill, Piece("BtnBlue"), new Color(0.07f, 0.08f, 0.12f, 1f), true);
                LayoutResult(false);
            }

            Wire(_retry, () => { HideImmediate(); retry?.Invoke(); });
            Wire(_lobby, () => { HideImmediate(); lobby?.Invoke(); });

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _group.alpha = 0f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            CoupTween.Fade(_group, 1f, 0.28f);
            CoupTween.PopIn(_sheet, null, 0.36f);
            CoupTween.Punch(_title.transform, won ? 0.12f : 0.04f, 0.45f);

            if (won) PlayConfetti();
            else StopConfetti();
        }

        public void HideImmediate()
        {
            StopConfetti();
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void ApplyArt(bool won)
        {
            var bg = Piece(won ? "WinBg" : "LoseBg");
            if (_background != null)
            {
                _background.enabled = true;
                if (bg != null)
                {
                    _background.sprite = bg;
                    _background.type = Image.Type.Simple;
                    _background.preserveAspect = false;
                    _background.color = Color.white;
                }
            }
            if (_wash != null)
            {
                _wash.sprite = null;
                _wash.enabled = false;
                _wash.color = new Color(1f, 1f, 1f, 0f);
            }
            if (_border != null) _border.enabled = bg == null;

            Assign(_laurel, Piece("Laurel"));
            if (_portraitHalo != null) _portraitHalo.enabled = false;
            if (_portraitRing != null) _portraitRing.enabled = false;

            var mask = _portraitFrame != null ? _portraitFrame.Find("Mask")?.GetComponent<Image>() : null;
            var hole = Piece("Circle");
            if (mask != null && hole != null)
            {
                mask.sprite = hole;
                mask.color = Color.white;
            }
            if (mask != null)
            {
                var clip = mask.GetComponent<Mask>();
                if (clip != null) clip.showMaskGraphic = false;
            }
        }

        private static Sprite Piece(string name)
        {
            var sprite = Resources.Load<Sprite>("GameOver/" + name);
            if (sprite != null) return sprite;
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (var i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].name == name)
                    return sprites[i];
            }
            return null;
        }

        private static void Assign(Image image, Sprite sprite)
        {
            if (image == null) return;
            if (sprite != null) image.sprite = sprite;
            image.enabled = true;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        private static Sprite SpriteOf(UiVfx vfx, string name, Sprite preferred)
        {
            if (preferred != null) return preferred;
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (var i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].name == name)
                    return sprites[i];
            }
            return null;
        }

        private static void StyleButton(Image image, Sprite sprite, Color tint, bool drawFill)
        {
            if (image == null) return;
            if (!drawFill)
            {
                image.sprite = null;
                image.color = new Color(0f, 0f, 0f, 0.01f);
            }
            else if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
            }
            else
            {
                image.color = tint;
            }
            image.raycastTarget = true;
            var outline = image.transform.Find("Outline")?.GetComponent<Image>();
            if (outline != null) outline.enabled = sprite == null && drawFill;
        }

        private void LayoutResult(bool won)
        {
            _sheet.sizeDelta = new Vector2(920f, 1240f);
            _sheet.anchoredPosition = new Vector2(0f, 50f);
            if (_background != null) _background.rectTransform.sizeDelta = _sheet.sizeDelta;
            var portrait = won ? 520f : 440f;
            Place(_portraitFrame, new Vector2(portrait, portrait), new Vector2(0f, 300f));
            Place(_laurel != null ? _laurel.rectTransform : null, new Vector2(portrait + 160f, portrait + 160f), Vector2.zero);
            SizeFace(portrait * 0.72f);
            Place(_title != null ? _title.rectTransform : null, new Vector2(820f, 140f), new Vector2(0f, -50f));
            Place(_winner != null ? _winner.rectTransform : null, new Vector2(760f, 48f), new Vector2(0f, -150f));
            Place(_retry != null ? _retry.GetComponent<RectTransform>() : null, new Vector2(560f, 108f), new Vector2(0f, -270f));
            Place(_lobby != null ? _lobby.GetComponent<RectTransform>() : null, new Vector2(560f, 108f), new Vector2(0f, -390f));
        }

        private Button CreateButton(string name, Vector2 size, Vector2 pos, Color fill,
                                    out Image image, out TextMeshProUGUI label)
        {
            var button = UIFactory.Button(name, _sheet, _art != null ? _art.roundedPanel : null, size, pos);
            image = button.GetComponent<Image>();
            image.type = Image.Type.Sliced;
            image.color = fill;

            var outline = UIFactory.Image("Outline", button.transform,
                _art != null ? _art.roundedBorder : null, size - new Vector2(8f, 8f), Vector2.zero, false);
            outline.type = Image.Type.Sliced;
            outline.color = new Color(1f, 0.76f, 0.28f, 0.95f);

            label = UIFactory.Text("Label", button.transform, "", 36f,
                size - new Vector2(50f, 30f), Vector2.zero);
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            FitHud(label, 36f);
            return button;
        }

        private void PlayConfetti()
        {
            if (_portraitFrame == null) return;
            if (_confetti == null)
            {
                var prefab = Resources.Load<GameObject>("GameOver/WinConfetti");
                if (prefab == null) return;
                var go = Instantiate(prefab, _portraitFrame);
                go.name = "WinConfetti";
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                _confetti = go.GetComponent<ParticleSystem>();
                if (_confetti != null) ShapeVictoryConfetti(_confetti);
            }

            _confetti.gameObject.SetActive(true);
            _confetti.Clear(true);
            _confetti.Play(true);
        }

        /// <summary>
        /// Turns the prefab's drizzle into a celebration. Two things were wrong with
        /// winning: the confetti spawned in a narrow box across the middle of the
        /// card, so it covered the title and buttons rather than falling past them,
        /// and it emitted only a slow trickle that needed a full particle lifetime —
        /// over five seconds — to reach any density. Both are fixed here: emit as a
        /// wide line along the top edge, and open at the density the trickle was
        /// always climbing towards.
        ///
        /// Each system keeps its own rate, colours and materials, so the mix between
        /// ribbons, flakes, sparks and diamonds is the artist's. Applied to the
        /// instantiated copy; the prefab is hand-tuned and never written back to.
        /// </summary>
        private void ShapeVictoryConfetti(ParticleSystem root)
        {
            // Where the top of the card sits, in the confetti's own space. The
            // emitter is parented to the portrait frame, so this is not simply the
            // sheet's height.
            var top = 560f;
            var width = 980f;
            if (_sheet != null && _portraitFrame != null)
            {
                var sheetTop = _sheet.TransformPoint(new Vector3(0f, _sheet.rect.yMax, 0f));
                top = _portraitFrame.InverseTransformPoint(sheetTop).y;
                width = _sheet.rect.width;
            }

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var emission = ps.emission;
                if (!emission.enabled) continue;

                // The prefab emits from a 620x280 box at y=-40 -- the middle of the
                // card -- so confetti appeared on top of the title and the buttons
                // instead of falling past them. A constant -90 force carries a
                // particle about 1200 units over its life, which is the card's full
                // height, so emitting as a wide line along the top edge gives a fall
                // that lasts exactly as long as the particle does.
                var shape = ps.shape;
                if (shape.enabled && shape.shapeType == ParticleSystemShapeType.Box)
                {
                    shape.scale = new Vector3(width, 90f, 0.01f);
                    shape.position = new Vector3(0f, top, 0f);
                }

                // Starting velocity ran -30..+50 against a -90 force, so for the first
                // two seconds a particle has barely moved and the whole shower hangs
                // around the crest. Biasing the start downward makes it read as
                // falling from the moment it appears; the spread is kept so pieces
                // still drift at different speeds rather than dropping as a sheet.
                var velocity = ps.velocityOverLifetime;
                if (velocity.enabled)
                {
                    velocity.y = new ParticleSystem.MinMaxCurve(-150f, -40f);
                }

                var main = ps.main;
                var life = Mathf.Max(0.5f, main.startLifetime.constant);

                // These flakes are 60-120 units wide on a 920 wide card, so the rate
                // the prefab ships settles at a density that hides the laurel and the
                // title. Halving it leaves the fall readable; the win screen wants
                // confetti you can pick individual pieces out of, not a gold curtain.
                var trickle = Mathf.Max(0.5f, emission.rateOverTime.constant) * 0.5f;
                emission.rateOverTime = trickle;

                // A trickle needs one full particle lifetime to reach its steady
                // density, and these live 5.2s, so the first seconds of a win showed
                // a near-empty screen climbing slowly. rate x lifetime is exactly the
                // population the prefab was authored for: open at it instead of
                // crawling up to it. Scaling off each system's own rate keeps the
                // ribbon/flake/spark/diamond mix the artist chose.
                var opening = Mathf.RoundToInt(Mathf.Clamp(trickle * life, 8f, 90f));

                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, (short)opening),
                    new ParticleSystem.Burst(0.35f, (short)Mathf.Max(4, opening / 3))
                });

                // The prefab's cap was sized for the slow climb and would otherwise
                // swallow the opening burst.
                main.maxParticles = Mathf.Max(main.maxParticles, opening * 3);
            }
        }

        private void StopConfetti()
        {
            if (_confetti == null) return;
            _confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void FitDisplay(TextMeshProUGUI text, float size)
        {
            if (text == null) return;
            if (UIFactory.DisplayFont != null) text.font = UIFactory.DisplayFont;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = 48f;
            text.fontSizeMax = size;
            text.characterSpacing = 8f;
            text.wordSpacing = 20f;
        }

        private static void FitHud(TextMeshProUGUI text, float size)
        {
            if (text == null) return;
            var hud = HudFont();
            if (hud != null) text.font = hud;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = size;
            text.characterSpacing = 2.4f;
            text.wordSpacing = 10f;
        }

        private static TMP_FontAsset HudFont()
        {
            var labels = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < labels.Length; i++)
            {
                var font = labels[i].font;
                if (font != null && font.name.IndexOf("Rajdhani", StringComparison.OrdinalIgnoreCase) >= 0)
                    return font;
            }
            return UIFactory.BodyFont != null ? UIFactory.BodyFont : UIFactory.DisplayFont;
        }

        private static Rect SquareCrop(Texture texture)
        {
            if (texture == null || texture.width < 1 || texture.height < 1)
                return new Rect(0f, 0f, 1f, 1f);
            var aspect = texture.width / (float)texture.height;
            if (aspect < 0.98f)
            {
                var height = aspect;
                return new Rect(0f, 1f - height, 1f, height);
            }
            if (aspect > 1.02f)
            {
                var width = 1f / aspect;
                return new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            return new Rect(0f, 0f, 1f, 1f);
        }

        private void SizeFace(float size)
        {
            if (_portraitFrame == null) return;
            Place(_portraitFrame.Find("Mask") as RectTransform, new Vector2(size, size), Vector2.zero);
            Place(_portraitFrame.Find("Mask/Face") as RectTransform, new Vector2(size, size), Vector2.zero);
        }

        private static void Place(RectTransform rect, Vector2 size, Vector2 pos)
        {
            if (rect == null) return;
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
