using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using Kingmaker.Utility;
using UnityEngine;
using UnityEngine.Animations;

namespace EidolonUI {
    public class EvolutionBlueprint {
        public string Name;
        public int MaxRank = 1;
        public List<EvolutionBlueprint> Selections;

        public Sprite Sprite;

        public int BaseCost;
        public int ExtraRankCost;

        public int CostFor(int rank) => BaseCost + ((rank - 1) * ExtraRankCost);

        public bool IsToggle => MaxRank == 1;

        public EvolutionBlueprint LinkedEvolution;
        public bool LimitLinkedEvolutionRanksToThis = false;
    }
    public enum EvolutionAvailability {
        Available,
        Selected,
        SelectedBySurge,
        SelectedByBase,
        RequiresPrerequisites,
        RequiresSummonerLevel,
        NeverAvailable,
    }

    public static class EvoExtensions {
        public static bool IsPrerequisiteOK(this EvolutionAvailability avail) {
            return avail is EvolutionAvailability.Selected or EvolutionAvailability.SelectedByBase;
        }
        public static bool IsSelectedManually(this EvolutionAvailability avail) {
            return avail is EvolutionAvailability.Selected or EvolutionAvailability.SelectedBySurge;
        }

        public static bool UnaffectedByPreview(this EvolutionAvailability avail) {
            return avail is EvolutionAvailability.NeverAvailable or EvolutionAvailability.SelectedByBase or EvolutionAvailability.RequiresSummonerLevel;
        }
        public static EvolutionAvailability Selected(bool bySurge) {
            return bySurge ? EvolutionAvailability.SelectedBySurge : EvolutionAvailability.Selected;
        }

        public static Color Color(this EvolutionAvailability avail) {
            return avail switch {
                EvolutionAvailability.Available => Cols.Available,
                EvolutionAvailability.Selected => Cols.SelectedNormal,
                EvolutionAvailability.SelectedBySurge => Cols.SelectedNormal,
                EvolutionAvailability.SelectedByBase => Cols.SelectedNormal,
                EvolutionAvailability.RequiresPrerequisites => Cols.NotAvailable,
                EvolutionAvailability.RequiresSummonerLevel => Cols.NotAvailable,
                EvolutionAvailability.NeverAvailable => Cols.NotAvailable,
                _ => throw new NotImplementedException(),
            };

        }

    }

    public class Evolution {
        public readonly EvolutionBlueprint Blueprint;
        private int _CurrentRank = 0;
        public int PreviewAdjustment = 0;

        public EvolutionAvailability Availability;

        public bool Valid => Parent == null || Parent.Valid && Parent.EffectiveCurrentRank > 0;

        public int PreviewRank {
            get {
                if (Parent == null || Parent.PreviewRank > 0) {
                    return Math.Min(CurrentRank + PreviewAdjustment, PreviewMaxRank);
                } else {
                    return 0;
                }
            }
        }
        public int EffectiveCurrentRank => Math.Min(CurrentRank, EffectiveMaxRank);
        public int PreviewMaxRank {
            get {
                if (Parent?.Blueprint?.LimitLinkedEvolutionRanksToThis == true) {
                    return Parent.PreviewRank;
                } else {
                    return Blueprint.MaxRank;
                }
            }
        }

        public int EffectiveMaxRank {
            get {
                if (Parent?.Blueprint?.LimitLinkedEvolutionRanksToThis == true) {
                    return Parent.CurrentRank;
                } else {
                    return Blueprint.MaxRank;
                }
            }
        }

        public EvolutionAvailability EffectiveAvailability {
            get {
                if (Valid && Parent?.Availability.IsPrerequisiteOK() != false) {
                    return Availability;
                }

                return EvolutionAvailability.RequiresPrerequisites;

            }
        }

        public bool NotAvailable {
            get {
                return EffectiveAvailability is EvolutionAvailability.NeverAvailable or EvolutionAvailability.RequiresSummonerLevel or EvolutionAvailability.RequiresPrerequisites;
            }
        }

        public int CurrentRank {
            get => _CurrentRank;
            private set {
                if (_CurrentRank != value) {
                    _CurrentRank = value;
                    OnChange?.Invoke();
                }
            }
        }

        public void SetRank(bool surge, int rank) {
            CurrentRank = rank;
            Availability = CurrentRank > 0 ? EvoExtensions.Selected(surge) : EvolutionAvailability.Available;
        }

        public Evolution(EvolutionBlueprint blueprint) {
            Blueprint = blueprint;
            foreach (var selection in blueprint.Selections.EmptyIfNull()) {
                Evolution model = new(selection);
                model.Parent = this;
                Selections.Add(selection.Name, model);
                SelectionsOrdered.Add(model);
            }
        }

        public Evolution Parent;

        public readonly Dictionary<string, Evolution> Selections = new();
        public readonly List<Evolution> SelectionsOrdered = new();

        public event Action OnChange;

        public void ToggleOnOff(bool isSurge) {
            CurrentRank = CurrentRank == 0 ? 1 : 0;
            if (CurrentRank > 0) {
                if (Availability != EvolutionAvailability.Available) {
                    throw new System.Exception("toggling on but not availbale");
                }
                Availability = EvoExtensions.Selected(isSurge);
            } else {
                if (!(Availability is EvolutionAvailability.Selected or EvolutionAvailability.SelectedByBase)) { 
                    throw new System.Exception("toggling off but not selected");
                }
                Availability = EvolutionAvailability.Available;
            }
        }
    }
}
