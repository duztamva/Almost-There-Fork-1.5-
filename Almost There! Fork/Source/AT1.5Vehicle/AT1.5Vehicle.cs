using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Vehicles;
using Verse;

namespace CaravanDontRest
{
    [StaticConstructorOnStartup]
    internal class HarmonyPatches
    {
        // Token: 0x06000011 RID: 17 RVA: 0x0000228C File Offset: 0x0000048C
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
            // 如果原逻辑已经判定为不需要休息，则跳过
            if (!__result) return;

            // 检查是否有我们的组件且禁用了休息
            var comp = __instance.GetComponent<CompNightRestControl>();
            if (comp != null)
            {
                switch (comp.AlmostThere)
                {
                    case 0:
                        float num;
                        if (__instance is VehicleCaravan)
                        {
                            num = (float)VehicleCaravanPathingHelper.EstimatedTicksToArrive((VehicleCaravan)__instance, true);

                        }
                        else
                        {
                            num = (float)CaravanArrivalTimeEstimator.EstimatedTicksToArrive(__instance, true);
                            int num2 = CaravanNightRestUtility.LeftRestTicksAt(__instance.Tile, (long)Find.TickManager.TicksAbs);
                            num -= (float)num2;
                            if (num / 2500f < (float)AlmostThereSettings.AlmostThereHours)
                            {
                                __result = false;
                            }
                        }
                        break;
                    case 1:
                        __result = false;
                        break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(WorldPathGrid), nameof(WorldPathGrid.CalculatedMovementDifficultyAt))]
    public static class WorldPathGrid_CalculatedMovementDifficultyAt_Patch
    {
        public static float Postfix(float result, int tile, bool perceivedStatic, int? ticksAbs, StringBuilder explanation)
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
        // Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
        public AlmostThereMod(ModContentPack content)
            : base(content)
        {
            base.GetSettings<AlmostThereSettings>();
        }

        // Token: 0x06000002 RID: 2 RVA: 0x00002064 File Offset: 0x00000264
        public override string SettingsCategory()
        {
            return "Almost There!";
        }

        // Token: 0x06000003 RID: 3 RVA: 0x0000207C File Offset: 0x0000027C
        public override void DoSettingsWindowContents(Rect rect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(rect);
            AlmostThereSettings.AlmostThereHours = (int)listing_Standard.SliderLabeled("AlmostThereHours_Title".Translate(AlmostThereSettings.AlmostThereHours), (float)AlmostThereSettings.AlmostThereHours, 0f, 100f, 0.25f, null);
            AlmostThereSettings.NightFactor = (float)listing_Standard.SliderLabeled("AlmostThereNightFactor_Title".Translate(AlmostThereSettings.NightFactor.ToStringPercent()), (float)AlmostThereSettings.NightFactor, 1f, 4f, 0.25f, null);
            listing_Standard.End();
            base.DoSettingsWindowContents(rect);
        }
    }
    internal class AlmostThereSettings : ModSettings
    {
        // Token: 0x06000004 RID: 4 RVA: 0x000020E2 File Offset: 0x000002E2
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref AlmostThereSettings.AlmostThereHours, "AlmostThereHours", 4, false);
            Scribe_Values.Look<float>(ref AlmostThereSettings.NightFactor, "AlmostThereNightFactor", 1.0f, false);
        }

        // Token: 0x04000001 RID: 1
        public static int AlmostThereHours = 4;
        public static float NightFactor = 1.0f;
    }

    public class WorldObjectCompProperties_NightRestControl : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_NightRestControl()
        {
            // 关联到组件实现类
            compClass = typeof(CompNightRestControl);
        }
    }
    public class CompNightRestControl : WorldObjectComp
    {
        private int almostThere = 0;
        // 公开访问属性
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

        // 序列化（保存/加载游戏）
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref almostThere, "almostThere", 0, false);
        }

        // 添加控制按钮
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