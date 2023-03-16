using BubbleBuffs;
using Kingmaker.UI.MVVM._PCView.ServiceWindows.Spellbook.KnownSpells;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using Owlcat.Runtime.UI.Controls.Button;
using UniRx;

namespace EidolonUI {
    public class Prefabs {
        public static (Sprite, Texture) LoadSpriteAndTexture(string filePath, Vector2Int size) {
            var bytes = File.ReadAllBytes(filePath);
            var texture = new Texture2D(size.x, size.y, TextureFormat.ARGB32, false);
            _ = texture.LoadImage(bytes);
            return (Sprite.Create(texture, new Rect(0, 0, size.x, size.y), new Vector2(0, 0)), texture);
        }
        public static Sprite LoadSprite(string filePath, Vector2Int size) {
            var bytes = File.ReadAllBytes(filePath);
            var texture = new Texture2D(size.x, size.y, TextureFormat.ARGB32, false);
            _ = texture.LoadImage(bytes);
            return Sprite.Create(texture, new Rect(0, 0, size.x, size.y), new Vector2(0, 0));
        }

        public void MakeEvoTogglePrefab() {
            SpellbookKnownSpellPCView spellPrefab = null;
            var listPrefab = UIHelpers.SpellbookScreen.Find("MainContainer/KnownSpells");
            var spellsKnownView = listPrefab.GetComponent<SpellbookKnownSpellsPCView>();

            if (spellsKnownView != null)
                spellPrefab = listPrefab.GetComponent<SpellbookKnownSpellsPCView>().m_KnownSpellView;
            else {
                foreach (var component in UIHelpers.SpellbookScreen.gameObject.GetComponents<Component>()) {
                    if (component.GetType().FullName == "EnhancedInventory.Controllers.SpellbookController") {
                        var fieldHandle = component.GetType().GetField("m_known_spell_prefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        spellPrefab = (SpellbookKnownSpellPCView)fieldHandle.GetValue(component);

                        break;
                    }
                }
            }

            GameObject framePrefab = spellPrefab.transform.Find("School/Frame").gameObject;
            textPrefab = spellPrefab.transform.Find("School/SchoolLabel").gameObject;

            Sprite bgTextureObj = spellPrefab.transform.Find("Name/BackgroundName").GetComponent<Image>().sprite;

            damageIcon = LoadSprite("D:/wrathassets/Sprite/inc_damage.png", new(64, 64));
            reachIcon = LoadSprite("D:/wrathassets/Sprite/reach.png", new(64, 64));
            rendIcon = LoadSprite("D:/wrathassets/Sprite/rend.png", new(64, 64));
            tripIcon = LoadSprite("D:/wrathassets/Sprite/trip.png", new(64, 64));
            dnaIcon = LoadSprite("D:/wrathassets/Sprite/dna.png", new(64, 64));
            lineFrame = LoadSprite("D:/wrathassets/Sprite/UI_FrameIconSpellLine.png", new(111, 114));

            spellFrame = new("D:/wrathassets/Sprite/UI_FrameIconSpell", new(105, 109));

            {
                miniPrefab = new GameObject("evoTogglePrefab", typeof(RectTransform));
                var rect = miniPrefab.transform as RectTransform;
                rect.sizeDelta = new(25, 25);

                //miniPrefab.AddComponent<Image>().color = Color.red;

                var fill = new GameObject("fill", typeof(RectTransform));
                fill.name = "fill";
                fill.AddComponent<Image>();
                fill.AddTo(rect);
                fill.FillParent();

                var frame = GameObject.Instantiate(framePrefab);
                frame.name = "frame";
                frame.AddTo(rect);
                frame.FillParent();

                var icon = GameObject.Instantiate(framePrefab);
                icon.name = "icon";
                icon.GetComponent<Image>().color = Color.black;
                icon.AddTo(rect);
                icon.FillParent();

                miniPrefab.AddComponent<OwlcatButton>();
            }

            {
                titlePrefab = new GameObject("titlePrefab", typeof(RectTransform));
                var rect = titlePrefab.transform as RectTransform;

                var bg = GameObject.Instantiate(framePrefab);
                bg.AddTo(rect);
                bg.name = "fill";
                bg.GetComponent<Image>().sprite = bgTextureObj;

                //var frame = GameObject.Instantiate(framePrefab);
                //frame.name = "frame";
                //frame.AddTo(rect);

                var label = GameObject.Instantiate(textPrefab);
                label.name = "name";
                label.AddTo(rect);

                var icon = GameObject.Instantiate(framePrefab);
                icon.name = "icon";
                icon.AddTo(rect);

                bg.Rect().SetAnchor(0.3, 1.00, 0.1, 0.9);
                bg.Rect().sizeDelta = Vector2.zero;
                //frame.FillParent();

                icon.Rect().pivot = new(0, 0.5f);
                icon.Rect().SetAnchor(0, 0.5);
                icon.Rect().sizeDelta = new(40, 40);
                icon.Rect().anchoredPosition = new(7, 0);

                label.Rect().pivot = new(1, 0.5f);
                label.Rect().SetAnchor(1, 0.5);
                label.Rect().sizeDelta = new(150 - 50, 32);
                label.Rect().anchoredPosition = new(-2, 0);

                rect.sizeDelta = new(150, 40);
            }

            {
                evoTogglePrefab = new GameObject("evoTogglePrefab", typeof(RectTransform));
                var rect = evoTogglePrefab.transform as RectTransform;

                var bg = GameObject.Instantiate(framePrefab);
                bg.AddTo(rect);
                bg.name = "fill";
                bg.GetComponent<Image>().sprite = bgTextureObj;

                var frame = GameObject.Instantiate(framePrefab);
                frame.name = "frame";
                frame.AddTo(rect);

                var label = GameObject.Instantiate(textPrefab);
                label.name = "name";
                label.AddTo(rect);

                var icon = GameObject.Instantiate(framePrefab);
                icon.name = "icon";
                icon.AddTo(rect);

                bg.Rect().SetAnchor(0.3, 1.00, 0.1, 0.9);
                bg.Rect().sizeDelta = Vector2.zero;
                frame.FillParent();

                icon.Rect().pivot = new(0, 0.5f);
                icon.Rect().SetAnchor(0, 0.5);
                icon.Rect().sizeDelta = new(40, 40);
                icon.Rect().anchoredPosition = new(7, 0);

                label.Rect().pivot = new(1, 0.5f);
                label.Rect().SetAnchor(1, 0.5);
                label.Rect().sizeDelta = new(150 - 50, 32);
                label.Rect().anchoredPosition = new(-2, 0);

                evoTogglePrefab.AddComponent<OwlcatButton>();
                rect.sizeDelta = new(150, 40);
            }

            //evoTogglePrefab = GameObject.Instantiate(spellPrefab.gameObject);
            //evoTogglePrefab.name = "EidolonToggler";
            //evoTogglePrefab.DestroyComponents<SpellbookKnownSpellPCView>();
            //evoTogglePrefab.DestroyChildrenImmediate("Icon/Decoration", "Icon/Domain", "Icon/MythicArtFrame", "Icon/ArtArrowImage", "RemoveButton", "Level");
        }
        private GameObject evoTogglePrefab;
        public GameObject titlePrefab;
        private GameObject miniPrefab;

        public EvoToggle BigToggle(string name) {
            return new(evoTogglePrefab, spellFrame) {
                Name = name,
            };
        }

        public TitleView Title(string name) {
            return new(titlePrefab) {
                Name = name,
            };
        }


        public EvoToggle Toggle(Sprite sprite) {
            return new(miniPrefab, spellFrame) {
                Sprite = sprite,
            };
        }

        public Sprite damageIcon;
        public Sprite reachIcon;
        public Sprite rendIcon;
        public Sprite tripIcon;

        public Sprite dnaIcon;
        public Sprite lineFrame;

        private ButtonSprites spellFrame;
        public GameObject textPrefab;

        public class ButtonSprites {
            public Sprite Normal;
            public Sprite Hover;
            public Sprite Active;
            public Sprite ActiveHover;

            public ButtonSprites(string basePath, Vector2Int size) {
                Normal = LoadSprite(basePath + ".png", size);
                Hover = LoadSprite(basePath + "_Hover.png", size);
                Active = LoadSprite(basePath + "_Active.png", size);
                ActiveHover = LoadSprite(basePath + "_ActiveHover.png", size);
            }
        }

    }
}
