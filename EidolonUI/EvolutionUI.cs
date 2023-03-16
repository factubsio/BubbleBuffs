using BubbleBuffs.EidolonEditor;
using BubbleBuffs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI;
using Kingmaker.ResourceLinks;
using BubbleBuffs.Utilities;
using Kingmaker.UI.MVVM._VM.Tooltip.Utils;
using Owlcat.Runtime.UI.Controls.Button;
using UniRx;
using QuickGraph;
using Kingmaker.UnitLogic.FactLogic;
using System.Globalization;
using UniRx.Triggers;
using Kingmaker.Designers.EventConditionActionSystem.NamedParameters;
using Kingmaker.Visual;
using UnityEngine.EventSystems;
using JetBrains.Annotations;
using Kingmaker.Designers.EventConditionActionSystem.Evaluators;
using Steamworks;
using System.Data.Objects;
using UnityEngine.TextCore;
using System.Runtime.CompilerServices;
using Owlcat.Runtime.Core.Utils;

namespace EidolonUI {
    public static class Cols {
        public static Color Available = new(0.85f, 0.85f, 0.85f, 0.5f);
        public static Color NotAvailable = new(1.00f, 0.15f, 0.15f, 0.5f);
        public static Color SelectedNormal = new(0.15f, 1.05f, 0.15f, 0.5f);

        public static Color PreviewSpend = new(0.35f, 0.45f, 0.75f, 0.5f);
        public static Color PreviewRefund = new(0.85f, 0.85f, 0.05f, 0.6f);
    }

    public class EvolutionUI : IUIProvider {

        public static EvolutionUI Instance;

        public Action<string> Log { get; set; }


        public string ID => "EvolutionUI";



        public class EvolutionRankView {
            public EvolutionRankView(Action<bool, bool> onChanged, Prefabs prefabs) {
                Toggle = prefabs.Toggle(null);

                Toggle.Button.OnLeftClick.AddListener(() => onChanged(false, false));

                Toggle.Button.OnHover.AddListener(h => onChanged(true, h));
            }

            public EvoToggle Toggle;
        }

        public class SimpleEvolutionView {
            public Evolution Evolution;

            public SimpleEvolutionView(EvolutionState state, Prefabs prefabs, Evolution evolution) {
                Evolution = evolution;
                Toggle = prefabs.Toggle(evolution.Blueprint.Sprite);

                Toggle.Button.OnLeftClick.AddListener(() => {
                    if (Evolution.NotAvailable) {
                        return;
                    }
                    evolution.ToggleOnOff(state.Surge);

                    Update(state.Surge);
                    state.Update();
                });

                Toggle.Button.OnHover.AddListener(h => {
                    if (Evolution.NotAvailable) {
                        return;
                    }

                    if (h) {
                        evolution.PreviewAdjustment = evolution.CurrentRank == 0 ? 1 : -1;
                    } else {
                        evolution.PreviewAdjustment = 0;
                    }
                    Update(state.Surge);
                    state.Update();
                });
            }

            public EvoToggle Toggle;

            public void Update(bool surge) {
                Toggle.Background = Evolution.EffectiveAvailability.Color();
            }

        }

        public class EvolutionViewWithSelections {
            public Evolution Evolution;

            public BubbleObject MainToggle;
            public GameObject Root;

            public List<SimpleEvolutionView> Selections = new();
            public List<EvolutionRankView> Ranks = new();

            public EvolutionViewWithSelections(EvolutionState state, Prefabs prefabs, Evolution evolution) {
                bool currentHover = false;
                if (evolution.Blueprint.IsToggle) {
                    var toggle = prefabs.BigToggle(evolution.Blueprint.Name);
                    toggle.Button.OnLeftClick.AddListener(() => {
                        if (Evolution.NotAvailable) {
                            return;
                        }

                        evolution.ToggleOnOff(state.Surge);

                        if (currentHover) {
                            evolution.PreviewAdjustment = evolution.CurrentRank == 0 ? 1 : -1;
                        }

                        Update(state.Surge);
                        state.Update();

                    });
                    toggle.Button.OnHover.AddListener(h => {
                        if (Evolution.NotAvailable) {
                            return;
                        }

                        if (h) {
                            evolution.PreviewAdjustment = evolution.CurrentRank == 0 ? 1 : -1;
                        } else {
                            evolution.PreviewAdjustment = 0;
                        }
                        currentHover = h;
                        Update(state.Surge);
                        state.Update();
                    });

                    MainToggle = toggle;
                } else {
                    MainToggle = prefabs.Title(evolution.Blueprint.Name);

                    for (int i = 0; i < evolution.Blueprint.MaxRank; i++) {
                        int rank = (i + 1);
                        Ranks.Add(new((bool preview, bool hover) => {
                            if (Evolution.NotAvailable) {
                                return;
                            }

                            if (preview) {
                                if (hover) {
                                    if (rank <= Evolution.CurrentRank) {
                                        Evolution.PreviewAdjustment = rank - 1 - evolution.CurrentRank;
                                    } else {
                                        Evolution.PreviewAdjustment = rank - evolution.CurrentRank;
                                    }
                                } else {
                                    Evolution.PreviewAdjustment = 0;
                                }
                                currentHover = hover;
                            } else {
                                if (rank <= Evolution.CurrentRank) {
                                    Evolution.SetRank(state.Surge, rank - 1);
                                } else {
                                    Evolution.SetRank(state.Surge, rank);
                                }
                                if (currentHover) {
                                    if (rank <= Evolution.CurrentRank) {
                                        Evolution.PreviewAdjustment = rank - 1 - evolution.CurrentRank;
                                    } else {
                                        Evolution.PreviewAdjustment = rank - evolution.CurrentRank;
                                    }
                                }
                            }

                            Update(state.Surge);
                            state.Update();

                        }, prefabs));
                    }
                }
                MainToggle.Background = Cols.Available;

                foreach (var selection in evolution.SelectionsOrdered) {
                    Selections.Add(new(state, prefabs, selection));
                }

                Evolution = evolution;

                LayoutSelections(state.Surge);

                if (Evolution.Parent != null) {
                    Evolution.Parent.OnChange += () => Update(state.Surge);
                }
            }

            public void Update(bool surge) {
                foreach (var sel in Selections) {
                    sel.Update(surge);
                }

                for (int i = 0; i < Ranks.Count; i++) {
                    if (i < Evolution.EffectiveCurrentRank) {
                        Ranks[i].Toggle.Background = surge ? Cols.SelectedNormal : Cols.SelectedNormal;
                    } else if (i < Evolution.EffectiveMaxRank){
                        Ranks[i].Toggle.Background = Cols.Available;
                    } else {
                        Ranks[i].Toggle.Background = Cols.NotAvailable;
                    }
                }

                if (Evolution.Blueprint.IsToggle) {
                    MainToggle.Background = Evolution.EffectiveAvailability.Color();
                }
            }

            private void LayoutSelections(bool surge) {
                Root = new GameObject("EvolutionViewWithSelections", typeof(RectTransform));
                MainToggle.Rect.AddTo(Root);
                Root.Rect().sizeDelta = MainToggle.Rect.sizeDelta;
                MainToggle.Rect.FillParent();

                double sDiv = Selections.Count + 1;
                for (int i = 0; i < Selections.Count; i++) {
                    SimpleEvolutionView selection = Selections[i];
                    selection.Toggle.Obj.AddTo(Root);
                    selection.Toggle.Rect.pivot = new(0.5f, 1);
                    double x = (i + 1) / sDiv;
                    selection.Toggle.Rect.SetAnchor(x, x, 0, 0);
                    selection.Toggle.Rect.anchoredPosition = new(0, 1);
                }

                double rDiv = Ranks.Count + 1;
                for (int i = 0; i < Ranks.Count; i++) {
                    EvolutionRankView rank = Ranks[i];
                    rank.Toggle.Obj.AddTo(Root);
                    rank.Toggle.Rect.pivot = new(0.5f, 0);
                    double x = (i + 1) / rDiv;
                    rank.Toggle.Rect.SetAnchor(x, x, 1, 1);
                    rank.Toggle.Rect.anchoredPosition = new(0, -1);
                }

                Update(surge);
            }
        }

        Prefabs prefabs;
        public bool made = false;

        private TMP_SpriteAsset textSprite = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        //private TMP_SpriteGlyph glyph;
        //private TMP_SpriteCharacter chara;
        private TMP_Sprite glyph;
        private Sprite tmpSprite;
        private Texture tmpTexture;

        public void BuildUI(UnityEngine.Transform root) {
            Log("Helloo");

            this.State = new();
            this.evoViews = new();

            prefabs = new();
            prefabs.MakeEvoTogglePrefab();

            root.gameObject.DestroyComponents<ContentSizeFitterExtended>();
            root.gameObject.DestroyComponents<ContentSizeFitterExtended>();
            root.gameObject.DestroyComponents<VerticalLayoutGroupWorkaround>();

            EvolutionBlueprint incDamageBp = new() {
                BaseCost = 1,
                Sprite = prefabs.damageIcon,
                Name = "Increased Damage",
            };
            EvolutionBlueprint reachBp = new() {
                BaseCost = 1,
                Sprite = prefabs.reachIcon,
                Name = "Reach",
            };
            EvolutionBlueprint tripBp = new() {
                BaseCost = 2,
                Sprite = prefabs.tripIcon,
                Name = "Trip",
            };
            EvolutionBlueprint rendBp = new() {
                BaseCost = 2,
                Sprite = prefabs.rendIcon,
                Name = "Rend",
            };


            try {
                GlyphMetrics metrics = new(16, 16, 0, 0, 16);
                GlyphRect rect = new(0, 0, 16, 16);

                (tmpSprite, tmpTexture) = Prefabs.LoadSpriteAndTexture("D:/wrathassets/Sprite/dna.png", new(64, 64));

                glyph = new() {
                    name = "bubble",
                    unicode = 0,
                    pivot = Vector2.zero,
                    sprite = tmpSprite,
                    id = 1,
                    x = 0,
                    y = 0,
                    width = 16,
                    height = 16,
                    xOffset = 0,
                    yOffset = 0,
                    xAdvance = 16,
                    scale = 1,
                };
                var field = typeof(TextMeshProUGUI).GetField("m_DefaultSpriteAsset", System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var defaultAsset = field.GetValue(prefabs.textPrefab.GetComponent<TextMeshProUGUI>()) as TMP_SpriteAsset;
                var setFace = typeof(TMP_SpriteAsset).GetField("m_FaceInfo", System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                setFace.SetValue(textSprite, defaultAsset.faceInfo);
                textSprite.spriteInfoList.Add(glyph);
                textSprite.spriteSheet = tmpTexture;
                Material material = new Material(Shader.Find("TextMeshPro/Sprite"));
                material.SetTexture(ShaderUtilities.ID_MainTex, textSprite.spriteSheet);
                material.hideFlags = HideFlags.HideInHierarchy;
                textSprite.material = material;
                textSprite.UpdateLookupTables();
            } catch (Exception ex) {
                Log(ex.Message);

            }

            TMP_Text.OnSpriteAssetRequest += HandleSpriteAssetRequest;
            made = true;


            EvolutionBlueprint goreBp = new() {
                BaseCost = 2,
                Sprite = null,
                Name= "Gore <sprite=\"bubbs\" name=\"bubble\">",
                Selections = new() {
                    incDamageBp,
                    reachBp,
                }
            };

            EvolutionBlueprint biteBp = new() {
                BaseCost = 1,
                Sprite = null,
                Name= "Bite",
                Selections = new() {
                    incDamageBp,
                    reachBp,
                    tripBp,
                }
            };

            EvolutionBlueprint clawsBp = new() {
                BaseCost = 1,
                ExtraRankCost = 1,
                MaxRank = 5,
                Sprite = null,
                Name = "Claws",
                Selections = new() {
                    incDamageBp,
                    reachBp,
                    rendBp,
                }
            };

            EvolutionBlueprint armsBp = new() {
                BaseCost = 2,
                ExtraRankCost = 2,
                MaxRank = 5,
                Sprite = null,
                Name = "Arms",
                LinkedEvolution = clawsBp,
                LimitLinkedEvolutionRanksToThis = true,
            };

            Evolution gore = new(goreBp);
            Evolution bite = new(biteBp);

            State.Add(gore);
            State.Add(bite);

            EvolutionViewWithSelections goreView = new(State, prefabs, gore);
            EvolutionViewWithSelections biteView = new(State, prefabs, bite);

            goreView.Root.AddTo(root);
            goreView.Root.Rect().localPosition = new(300, -300);

            biteView.Root.AddTo(root);
            biteView.Root.Rect().localPosition = new(300, -380);

            Evolution arms = new(armsBp);
            Evolution claws = new(clawsBp);
            claws.Parent = arms;

            State.Add(arms);
            State.Add(claws);

            EvolutionViewWithSelections armsView = new(State, prefabs, arms);
            EvolutionViewWithSelections clawsView = new(State, prefabs, claws);

            armsView.Root.AddTo(root);
            armsView.Root.Rect().localPosition = new(500, -300);

            clawsView.Root.AddTo(root);
            clawsView.Root.Rect().localPosition = new(500, -380);

            const int evoPointSize = 32;

            var budget = new GameObject("evo_points", typeof(RectTransform));
            budget.Rect().sizeDelta = new(evoPointSize * 20, evoPointSize);
            budget.AddTo(root);
            budget.Rect().localPosition = new(200, -500);

            for (int i = 0; i < 15; i++) {
                var cell = new GameObject("evo_point_cell", typeof(RectTransform));
                cell.Rect().sizeDelta = new(evoPointSize, evoPointSize);

                var bg = new GameObject("bg", typeof(RectTransform));
                var bgImg = bg.AddComponent<Image>();
                bgImg.color = Cols.Available;
                bg.AddTo(cell);
                bg.FillParent();

                var fg = new GameObject("fg", typeof(RectTransform));
                var fgImg = fg.AddComponent<Image>();
                fgImg.color = Color.gray;
                fgImg.sprite = prefabs.dnaIcon;
                fg.AddTo(cell);
                fg.FillParent();

                var frame = new GameObject("frame", typeof(RectTransform));
                var frameImg = frame.AddComponent<Image>();
                frameImg.color = Color.black;
                frameImg.sprite = prefabs.lineFrame;
                frame.AddTo(cell);
                frame.FillParent();

                cell.AddTo(budget);
                cell.Rect().localPosition = new(i * evoPointSize, 0);

                State.EvoPoints.Add(new());
                evoViews.Add(bgImg);
            }

            State.OnUpdate = () => {
                for (int i = 0; i < State.EvoPoints.Count; i++) {
                    evoViews[i].color = State.EvoPoints[i].Color;
                }
            };
        }

        private TMP_SpriteAsset HandleSpriteAssetRequest(int arg1, string arg2) {
            Log("LOADING SPRITE (2): " + arg2 + " >" + arg1);
            return textSprite;
        }
        public void Load() {
        }

        public void Unload() {
            Log("unloading");
            if (made) {
                Log("unregistering old request");
                TMP_Text.OnSpriteAssetRequest -= HandleSpriteAssetRequest;
            }
            
        }

        private List<Image> evoViews;


        public EvolutionState State;

        public EvolutionUI() {
            Instance = this;
        }
    }


    public class EvolutionState {
        public int CurrentSpend = 0;
        public int PreviewSpend = 0;

        public Action OnUpdate;
        public bool Surge = false;
        private readonly List<Evolution> Evolutions = new();

        public void Update() {
            CurrentSpend = 0;
            PreviewSpend = 0;

            StringBuilder str = new();

            foreach (var evo in Evolutions) {
                CountEvolutionSpend(evo);

                foreach (var ch in evo.SelectionsOrdered.Where(x => x.PreviewRank != 0 || x.CurrentRank != 0)) {
                    CountEvolutionSpend(ch);
                }
            }

            //throw new Exception(str.ToString());

            EvoPointState previewState = EvoPointState.PreviewSpend;

            int previewFrom = CurrentSpend;
            int previewTo = CurrentSpend + PreviewSpend;

            if (PreviewSpend < 0) {
                previewFrom = CurrentSpend + PreviewSpend;
                previewTo = CurrentSpend;
                previewState = EvoPointState.PreviewRefund;
            }

            for (int i = 0; i < EvoPoints.Count; i++) {
                if (i < previewFrom) {
                    EvoPoints[i].State = EvoPointState.Spent;
                } else if (i < previewTo) {
                    EvoPoints[i].State = previewState;
                } else {
                    EvoPoints[i].State = EvoPointState.Available;
                }
            }

            OnUpdate?.Invoke();

            void CountEvolutionSpend(Evolution evo) {
                int spent = 0;
                int preview = 0;

                if (evo.Valid && evo.EffectiveCurrentRank > 0) {
                    spent = evo.Blueprint.CostFor(evo.EffectiveCurrentRank);
                }
                if ((evo.PreviewRank != evo.EffectiveCurrentRank) || (evo.PreviewRank > 0 && !evo.Valid)) {
                    if (evo.PreviewRank == 0) {
                        preview = -spent;
                    } else {
                        preview = evo.Blueprint.CostFor(evo.PreviewRank) - spent;
                    }
                }
                str.Append("spent: ").Append(evo.CurrentRank).Append(", previewDelta: ").Append(evo.PreviewAdjustment).Append(";  ");

                CurrentSpend += spent;
                PreviewSpend += preview;
            }
        }

        internal void Add(Evolution gore) {
            Evolutions.Add(gore);
        }

        public readonly List<EvoPointSpend> EvoPoints = new();
    }

    public enum EvoPointState {
        Available,
        Spent,
        PreviewSpend,
        PreviewRefund,
    }

    public class EvoPointSpend {
        public EvoPointState State = EvoPointState.Available;

        public Color Color => State switch {
            EvoPointState.Available => Cols.Available,
            EvoPointState.Spent => Cols.SelectedNormal,
            EvoPointState.PreviewSpend => Cols.PreviewSpend,
            EvoPointState.PreviewRefund => Cols.PreviewRefund,
            _ => Color.magenta,
        };
    }
}
