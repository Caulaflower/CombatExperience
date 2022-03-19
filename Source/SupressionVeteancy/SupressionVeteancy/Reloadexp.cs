using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using UnityEngine;
using CombatExtended;
using System.Xml;
using HarmonyLib;

namespace SupressionVeteancy
{
    public static class Exts
    {
        public static float ToFloat(this bool boo)
        {
            if (boo)
            {
                return 100f;
            }
            return 0f;
        }

        public static float Round(this float source)
        {
            return (float)Math.Round(source, 2);
        }

        public static float Round(this float source, int digs)
        {
            return (float)Math.Round(source, digs);
        }
    }

    #region hands
    public class handnessWorker : StatWorker
    {
        public override void FinalizeValue(StatRequest req, ref float val, bool applyPostProcess)
        {
            val = 0f;

            if (req.Thing?.TryGetComp<DaComp>().hand == HandNess.left)
            {
                val = 1f;
            }
        }

        public override bool ShouldShowFor(StatRequest req)
        {
            return (req.Thing?.TryGetComp<DaComp>() ?? null) != null;
        }

        public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
        {
            if (finalVal == 1f)
            {
                return "Left";
            }
            return "Right";
        }
        public override string ValueToString(float val, bool finalized, ToStringNumberSense numberSense = ToStringNumberSense.Absolute)
        {
            if (val == 1f)
            {
                return "Left";
            }
            return "Right";
        }
    }
    public enum HandNess
    {
        right,
        left,
        ambi
    }
    public static class BetterCapacityUtil
    {
        public static float CalculateLimbEfficiencyHandNess(HediffSet diffSet, BodyPartTagDef limbCoreTag, BodyPartTagDef limbSegmentTag, BodyPartTagDef limbDigitTag, float appendageWeight, out float functionalPercentage, List<PawnCapacityUtility.CapacityImpactor> impactors, HandNess hadness = HandNess.right)
        {
            BodyDef body = diffSet.pawn.RaceProps.body;
            float num = 0f;
            int num2 = 0;
            int num3 = 0;
            foreach (BodyPartRecord item in body.GetPartsWithTag(limbCoreTag))
            {
                float num4 = PawnCapacityUtility.CalculateImmediatePartEfficiencyAndRecord(diffSet, item, impactors);
                foreach (BodyPartRecord connectedPart in item.GetConnectedParts(limbSegmentTag))
                {
                    num4 *= PawnCapacityUtility.CalculateImmediatePartEfficiencyAndRecord(diffSet, connectedPart, impactors);
                }
                if (item.HasChildParts(limbDigitTag))
                {
                    num4 = Mathf.Lerp(num4, num4 * item.GetChildParts(limbDigitTag).Average((BodyPartRecord digitPart) => PawnCapacityUtility.CalculateImmediatePartEfficiencyAndRecord(diffSet, digitPart, impactors)), appendageWeight);
                }

                #region handing effect
                HandNess hand = diffSet.pawn.TryGetComp<DaComp>().hand;

                HandNess hand2 = HandNess.right;

                if (item.Label.ToLower().Contains("left"))
                {
                    hand2 = HandNess.left;
                }

                if (hand == hand2)
                {
                    num *= 1.6f;
                }
                else
                {
                    num *= 0.4f;
                }
                #endregion

                num += num4;
                num2++;
                if (num4 > 0f)
                {
                    num3++;
                }

               

            }
            if (num2 == 0)
            {
                functionalPercentage = 0f;
                return 0f;
            }
            functionalPercentage = (float)num3 / (float)num2;
            return num / (float)num2;
        }
    }
    public class BetterManipulation : PawnCapacityWorker
    {
        public override float CalculateCapacityLevel(HediffSet diffSet, List<PawnCapacityUtility.CapacityImpactor> impactors = null)
        {
            float functionalPercentage = 0f;
            return BetterCapacityUtil.CalculateLimbEfficiencyHandNess(diffSet, BodyPartTagDefOf.ManipulationLimbCore, BodyPartTagDefOf.ManipulationLimbSegment, BodyPartTagDefOf.ManipulationLimbDigit, 0.8f, out functionalPercentage, impactors) * CalculateCapacityAndRecord(diffSet, PawnCapacityDefOf.Consciousness, impactors);
        }

        public override bool CanHaveCapacity(BodyDef body)
        {
            return body.HasPartWithTag(BodyPartTagDefOf.ManipulationLimbCore);
        }
    }
    #endregion

    public class PlatformExt : DefModExtension
    {
        public GunPlatformDef platform;

        public CaliberRange recoilCurve;

        public CaliberRange swayCurve;
    }

    public class CaliberRange
    {
        public SimpleCurve curve;

        public AmmoSetDef caliber;

        public AmmoSetDef similarTo;

        public AmmoSetDef Caliber
        {
            get
            {
                if (caliber != null)
                {
                    return caliber;
                }
                else
                {
                    return DefDatabase<AmmoSetDef>.AllDefs?.Where(x => x.defName == caliberStr).FirstOrDefault() ?? null;
                }
            }
        }

        public string caliberStr;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            foreach (XmlNode node in xmlRoot.ChildNodes)
            {
                if (node.Name == "caliber")
                {
                    DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "caliber", node.InnerText);
                    caliberStr = node.InnerText;
                }
                if (node.Name == "curve")
                {
                    SimpleCurve kurwa = new SimpleCurve();

                    foreach (XmlNode node2 in node.FirstChild.ChildNodes)
                    {
                        var idk = node2.InnerText.Split(',');

                        float x = ParseHelper.ParseFloat(idk[0]);

                        float y = ParseHelper.ParseFloat(idk[1]);

                        kurwa.Add(x, y);
                    }

                    curve = kurwa;
                }
                if (node.Name == "similarTo")
                {
                    DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "similarTo", node.InnerText);
                }
            }
        }
    }

    public class GunPlatformDef : Def
    {
        public List<string> names;

        public float xpGain;

        public SimpleCurve reloadSpeedCurve;

        public List<CaliberRange> recoilCurve;

        public List<CaliberRange> accuracyCurve;

        public SimpleCurve cooldownCurve;

        public HandNess hand;

        public bool MatchesNames(string label)  
        {
            bool result = false;

            string[] labels = label.ToLower().Replace("-", "").Replace(".", "").Split(' ');

            result = names.Intersect(labels).Any();

            return result;
        }

        public CaliberRange FindRangeRecoil(AmmoSetDef ammoset)
        {
            var result = recoilCurve.Find(x => x.Caliber == ammoset);

            if (result == null)
            {
                if (ammoset.similarTo != null)
                {
                    result = recoilCurve.Find(x => x.similarTo == ammoset.similarTo | x.similarTo == ammoset);
                }
            }

            if (result == null)
            {
                result = recoilCurve[0];            
            }

            return result;
        }
        public CaliberRange FindRangeSway(AmmoSetDef ammoset)
        {
            if (accuracyCurve == null)
            {
                Log.Error("accuracy curve is broken");
            }

            var result = accuracyCurve?.Find(x => x.Caliber == ammoset) ?? null;

            if (result == null)
            {
                if (ammoset.similarTo != null)
                {
                    result = accuracyCurve.Find(x => x.similarTo == ammoset.similarTo | x.similarTo == ammoset);
                }

            }

            if (result == null)
            {
                result = accuracyCurve[0];
            }

            return result;
        }
    }

    [StaticConstructorOnStartup]
    public class WeaponSystemAdder
    {
        static WeaponSystemAdder()
        {
            foreach (GunPlatformDef def in DefDatabase<GunPlatformDef>.AllDefs)
            {
                //Log.Message(def.label);
                foreach (ThingDef gun in DefDatabase<ThingDef>.AllDefs.Where(y => y.IsRangedWeapon))
                {
                    if (def.MatchesNames(gun.label))
                    {
                        if (gun.modExtensions == null)
                        {
                            gun.modExtensions = new List<DefModExtension>();
                        }

                        if (gun.comps == null)
                        {
                            gun.comps = new List<CompProperties>();
                        }

                        var ammoset = ((gun.comps?.Find(x => x is CompProperties_AmmoUser) ?? null) as CompProperties_AmmoUser)?.ammoSet ?? null;

                        if (ammoset != null)
                        {
                            gun.modExtensions.Add(new PlatformExt { platform = def, recoilCurve = def.FindRangeRecoil(ammoset), swayCurve = def.FindRangeSway(ammoset) });

                            gun.comps.Add(new CompProperties { compClass = typeof(Notifier) });

                            Log.Message(gun.label.Colorize(Color.blue));
                        }

                    }
                }
            }

        }
    }

    public class StatWorker_Experience : StatWorker
    {
        public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
        {
            if (req.Thing != null)
            {
                var pawn = req.Thing as Pawn;

                var comp = pawn.TryGetComp<DaComp>();

                string result = "";
                if (comp.shootingXP != null)
                {
                    foreach (var pair in comp.shootingXP.OrderByDescending(x => 
                    (x.Key == (pawn.equipment.Primary?.def.GetModExtension<PlatformExt>()?.platform ?? null)).ToFloat()
                    +
                    x.Value
                    ))
                    {
                        if (pair.Key == (pawn.equipment.Primary?.def.GetModExtension<PlatformExt>()?.platform ?? null))
                        {
                            result += pair.Key.label.Colorize(Color.cyan) + " (including equipped " + pawn.equipment.Primary.Label + ")\n \n";
                        }
                        else
                        {
                            result += pair.Key.label + "\n \n";
                        }
                      
                        result += "Shooting experience percentage: " + (pair.Value / 10000f).Round(4) + "%";

                        result += "\n \n Effects on recoil:";
                        if (pair.Key.recoilCurve != null)
                        {
                            foreach (var a1a in pair.Key.recoilCurve)
                            {
                                if (a1a.curve != null)
                                {
                                    if (a1a.caliber != null)
                                    {
                                        result += "\n For " + a1a.Caliber.label.Colorize(new Color(0f, 1f, 0.25f)) + " multiplier: " + a1a.curve.Evaluate((pair.Value / 10000f)).Round().ToString()
                                            .Colorize(new Color(0, 100f / 255f, 30f / 255f));
                                    }
                                    else
                                    {
                                        if (a1a.similarTo != null)
                                        {
                                            result += "\n For " + a1a.similarTo.label.Colorize(new Color(0f, 1f, 0.25f)) + " calibers multiplier is: " + a1a.curve.Evaluate((pair.Value / 10000f)).Round().ToString()
                                               .Colorize(new Color(0, 100f / 255f, 30f / 255f));
                                        }
                                    }
                                }
                            }
                        }
                        

                        if (comp.reloadXP != null)
                        {
                            float pairReload = 0f;
                            comp.reloadXP?.TryGetValue(pair.Key, out pairReload);
                            
                            result += "\n Reloading experience: " + (pairReload / 1000f).Round(3) + "%";

                            result += "\n Reload speed effect: " + pair.Key.reloadSpeedCurve.Evaluate((pairReload / 1000f)).Round(3) + "%";
                        }
                        result += "\n \n \n";
                    }

                }
                return result;
            }
            return base.GetExplanationFinalizePart(req, numberSense, finalVal);
        }
    }

    public class StatPart_EquippedXPRecoil : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            if ((req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>() ?? null) != null)
            {
                if (req.Thing.def.HasModExtension<PlatformExt>())
                {
                    var ext = req.Thing.def.GetModExtension<PlatformExt>();

                    float sneeze = 0f;
                    req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>().shootingXP.TryGetValue(ext.platform, out sneeze);

                    sneeze = (float)Math.Round(ext.recoilCurve.curve.Evaluate(sneeze / 10000f), 2);


                    return req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder.Name.ToStringShort
                        + "'s experience on " + ext.platform.label
                        + ": x" + (sneeze * 100f + "%").Colorize(new Color(0, 100f / 255f, 30f / 255f))
                            
                        ;
                }
            }
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if ((req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>() ?? null) != null)
            {
                if (req.Thing?.def.HasModExtension<PlatformExt>() ?? false)
                {
                    var ext = req.Thing.def.GetModExtension<PlatformExt>();

                    float sneeze = 0f;
                    req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>().shootingXP.TryGetValue(ext.platform, out sneeze);

                    sneeze = (float)Math.Round(ext.recoilCurve.curve.Evaluate(sneeze / 10000f), 2);


                    val *= sneeze;
                }
            }
        }
    }

    public class StatPart_EquippedXPSway : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            if ((req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>() ?? null) != null)
            {
                if (req.Thing.def.HasModExtension<PlatformExt>())
                {
                    var ext = req.Thing.def.GetModExtension<PlatformExt>();

                    float sneeze = 0f;
                    req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>().shootingXP.TryGetValue(ext.platform, out sneeze);

                    sneeze = (float)Math.Round(ext.swayCurve.curve.Evaluate(sneeze / 10000f), 2);


                    return req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder.Name.ToStringShort
                        + "'s experience on " + ext.platform.label
                        + ": x" + (sneeze * 100f + "%").Colorize(new Color(0, 100f / 255f, 30f / 255f))

                        ;
                }
            }
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if ((req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>() ?? null) != null)
            {
                if (req.Thing?.def.HasModExtension<PlatformExt>() ?? false)
                {
                    var ext = req.Thing.def.GetModExtension<PlatformExt>();

                    float sneeze = 0f;
                    req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>().shootingXP.TryGetValue(ext.platform, out sneeze);

                    sneeze = (float)Math.Round(ext.swayCurve.curve.Evaluate(sneeze / 10000f), 2);


                    val *= sneeze;
                }
            }
        }
    }

    public class StatPart_EquippedXPReloadSpeed : StatPart
    {
        public override string ExplanationPart(StatRequest req)
        {
            if ((req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>() ?? null) != null)
            {
                if (req.Thing.def.HasModExtension<PlatformExt>())
                {
                    var comp = req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>();

                    var ext = req.Thing.def.GetModExtension<PlatformExt>();

                    float sneeze = (float)Math.Round(ext.platform.reloadSpeedCurve.Evaluate((comp.reloadXP.GetValueSafe(ext.platform) / 1000f)), 2);

                    if (comp.hand != ext.platform.hand && !(ext.platform.hand == HandNess.ambi))
                    {
                        return req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder.Name.ToStringShort
                       + "'s experience on " + ext.platform.label
                       + ": x" + (sneeze * 100f + "%").Colorize(new Color(0, 100f / 255f, 30f / 255f))
                       + "\n \n"
                       + "Using a " + ext.platform.hand + "handed gun as a " + comp.hand + "hander x133%"
                       ;
                    }


                    return req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder.Name.ToStringShort
                        + "'s experience on " + ext.platform.label
                        + ": x" + (sneeze * 100f + "%").Colorize(new Color(0, 100f / 255f, 30f / 255f))

                        ;
                }
            }
            return null;
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if ((req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>() ?? null) != null)
            {
                if (req.Thing?.def.HasModExtension<PlatformExt>() ?? false)
                {
                    var comp = req.Thing?.TryGetComp<CompAmmoUser>()?.Wielder?.TryGetComp<DaComp>();

                    var ext = req.Thing.def.GetModExtension<PlatformExt>();

                    float sneeze = (float)Math.Round(ext.platform.reloadSpeedCurve.Evaluate((comp.reloadXP.GetValueSafe(ext.platform) / 1000f)), 2);

                    sneeze = (float)Math.Round(ext.swayCurve.curve.Evaluate(sneeze / 1000f), 2);

                    if (comp.hand != ext.platform.hand && !(ext.platform.hand == HandNess.ambi))
                    {

                        //should be changed to def value
                        sneeze *= 1.33f;
                    }

                    val *= sneeze;
                }
            }
        }
    }

    [HarmonyPatch(typeof(CompAmmoUser), "TryMakeReloadJob")]

    static class PostFixNotifyAuthorities
    {
        public static void Postfix(CompAmmoUser __instance)
        {
            var gun = __instance.parent;

            if (gun?.TryGetComp<CompAmmoUser>()?.Wielder != null)
            {
                if (gun.TryGetComp<Notifier>() != null)
                {
                    Pawn pawn = gun.TryGetComp<CompAmmoUser>().Wielder;
                    gun.TryGetComp<Notifier>().Reloaded(pawn);
                }
            }
            
        }
    }
}
