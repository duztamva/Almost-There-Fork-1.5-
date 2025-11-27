using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace CaravanDontRest
{
    [StaticConstructorOnStartup]
    internal class HarmonyPatches
    {
        static HarmonyPatches()
        {
            Harmony harmony = new Harmony("duztamva.caravandontrest");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Caravan), "NightResting", MethodType.Getter)]
    public static class Caravan_NightResting_Patch
    {
        public static void Postfix(Caravan __instance, ref bool __result)
        {
            if (!__result) return;

            var comp = __instance.GetComponent<CompNightRestControl>();
            if (comp != null)
            {
                switch (comp.AlmostThere)
                {
                    case 0:
                        float num;
                        num = (float)CaravanArrivalTimeEstimator.EstimatedTicksToArrive(__instance, true);
                        int num2 = CaravanNightRestUtility.LeftRestTicksAt(__instance.Tile, (long)Find.TickManager.TicksAbs);
                        num -= (float)num2;
                        if (num / 2500f < (float)AlmostThereSettings.AlmostThereHours)
                        {
                            __result = false;
                        }
                        break;
                    case 1:
                        __result = false;
                        break;
                }
            }
        }
    }

    // 新添加的补丁：在远行队创建时设置默认模式
    [HarmonyPatch(typeof(Caravan), nameof(Caravan.PostAdd))]
    public static class Caravan_PostAdd_Patch
    {
        public static void Postfix(Caravan __instance)
        {
            var comp = __instance.GetComponent<CompNightRestControl>();
            if (comp != null && comp.AlmostThere == 0) // 只在初始状态下设置
            {
                comp.AlmostThere = AlmostThereSettings.DefaultCaravanMode;
            }
        }
    }

    [HarmonyPatch(typeof(WorldPathGrid), nameof(WorldPathGrid.CalculatedMovementDifficultyAt))]
    public static class WorldPathGrid_CalculatedMovementDifficultyAt_Patch
    {
        public static float Postfix(float result, PlanetTile tile, bool perceivedStatic, int? ticksAbs, StringBuilder explanation)
        {
            if (!CaravanNightRestUtility.WouldBeRestingAt(tile, ticksAbs ?? GenTicks.TicksAbs))
                return result;
            if (AlmostThereSettings.NightFactor == 1f)
                return result;
            result *= AlmostThereSettings.NightFactor;
            if (explanation != null)
            {
                explanation.AppendLine();
                explanation.Append("AlmostThereNightFactor".Translate() + ": x" + AlmostThereSettings.NightFactor.ToStringPercent());
            }
            return result;
        }
    }

    public class AlmostThereMod : Mod
    {
        public AlmostThereMod(ModContentPack content)
            : base(content)
        {
            base.GetSettings<AlmostThereSettings>();
        }

        public override string SettingsCategory()
        {
            return "Almost There!";
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(rect);

            // 原有的设置
            AlmostThereSettings.AlmostThereHours = (int)listing_Standard.SliderLabeled("AlmostThereHours_Title".Translate(AlmostThereSettings.AlmostThereHours), (float)AlmostThereSettings.AlmostThereHours, 0f, 100f, 0.25f, null);
            AlmostThereSettings.NightFactor = (float)listing_Standard.SliderLabeled("AlmostThereNightFactor_Title".Translate(AlmostThereSettings.NightFactor.ToStringPercent()), (float)AlmostThereSettings.NightFactor, 1f, 4f, 0.25f, null);

            listing_Standard.GapLine();

            // 新添加的默认远行队模式设置
            listing_Standard.Label("DefaultCaravanMode_Title".Translate());
            listing_Standard.Gap(4f);

            if (listing_Standard.RadioButton("AlmostThereLabel0".Translate(), AlmostThereSettings.DefaultCaravanMode == 0))
            {
                AlmostThereSettings.DefaultCaravanMode = 0;
            }

            if (listing_Standard.RadioButton("AlmostThereLabel1".Translate(), AlmostThereSettings.DefaultCaravanMode == 1))
            {
                AlmostThereSettings.DefaultCaravanMode = 1;
            }

            if (listing_Standard.RadioButton("AlmostThereLabel2".Translate(), AlmostThereSettings.DefaultCaravanMode == 2))
            {
                AlmostThereSettings.DefaultCaravanMode = 2;
            }

            listing_Standard.Gap(4f);
            listing_Standard.Label("DefaultCaravanMode_Desc".Translate());

            listing_Standard.End();
            base.DoSettingsWindowContents(rect);
        }
    }

    internal class AlmostThereSettings : ModSettings
    {
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref AlmostThereSettings.AlmostThereHours, "AlmostThereHours", 4, false);
            Scribe_Values.Look<float>(ref AlmostThereSettings.NightFactor, "AlmostThereNightFactor", 1.0f, false);
            Scribe_Values.Look<int>(ref AlmostThereSettings.DefaultCaravanMode, "DefaultCaravanMode", 0, false);
        }

        public static int AlmostThereHours = 4;
        public static float NightFactor = 1.0f;
        public static int DefaultCaravanMode = 0; // 0=几乎到达, 1=不休息, 2=强制休息
    }

    public class WorldObjectCompProperties_NightRestControl : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_NightRestControl()
        {
            compClass = typeof(CompNightRestControl);
        }
    }

    public class CompNightRestControl : WorldObjectComp
    {
        private int almostThere = 0;

        public int AlmostThere
        {
            get => almostThere;
            set
            {
                if (almostThere != value)
                {
                    almostThere = value;
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref almostThere, "almostThere", 0, false);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            yield return new Command_Toggle
            {
                isActive = () => AlmostThere <= 1,
                toggleAction = delegate {
                    if (AlmostThere >= 2)
                    {
                        AlmostThere = 0;
                    }
                    else AlmostThere++;
                },
                defaultDesc = (AlmostThere <= 0 ? "AlmostThereDesc0".Translate(AlmostThereSettings.AlmostThereHours) : (AlmostThere <= 1 ? "AlmostThereDesc1".Translate() : "AlmostThereDesc2".Translate())),
                icon = (AlmostThere <= 0 ? ContentFinder<Texture2D>.Get("UI/AlmostThere", true) : (AlmostThere <= 1 ? ContentFinder<Texture2D>.Get("UI/DontRest", true) : ContentFinder<Texture2D>.Get("UI/ForceRest", true))),
                defaultLabel = (AlmostThere <= 0 ? "AlmostThereLabel0".Translate() : (AlmostThere <= 1 ? "AlmostThereLabel1".Translate() : "AlmostThereLabel2".Translate()))
            };
        }
    }
}