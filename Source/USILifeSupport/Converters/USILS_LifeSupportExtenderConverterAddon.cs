using System.Collections.Generic;
using USITools;

namespace LifeSupport
{
    public class USILS_LifeSupportExtenderConverterAddon : AbstractConverterAddon<USI_Converter>
    {
        public float TimeMultiplier = 1f;
        public bool AffectsPartOnly = false;
        public string RestrictedToClass = "";
        public bool AffectsHomeTimer = true;
        public bool AffectsHabTimer = true;

        public USILS_LifeSupportExtenderConverterAddon(USI_Converter converter) : base(converter) { }

        public override void PreProcessing()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            base.PreProcessing();

            var v = LifeSupportManager.Instance.FetchVessel(Converter.vessel.id.ToString());

            var e = 1d;
            if (v != null)
            {
                e += v.VesselHabMultiplier;
            }

            Converter.EfficiencyBonus = (float)e;
        }

        public override void PostProcess(ConverterResults result, double deltaTime)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            base.PostProcess(result, deltaTime);
            var baseTime = TimeMultiplier * result.TimeFactor;
            var kerbals = new List<LifeSupportStatus>();
            var crew = Converter.vessel.GetVesselCrew();
            if (AffectsPartOnly)
                crew = Converter.part.protoModuleCrew;

            var moduleLifeSupportSystem = Converter.vessel.FindVesselModuleImplementing<ModuleLifeSupportSystem>();
            var habTime = -1.0d;
            if (moduleLifeSupportSystem != null)
                habTime = LifeSupportManager.GetTotalHabTime(moduleLifeSupportSystem.VesselStatus, Converter.vessel);

            var now = Planetarium.GetUniversalTime();
            var count = crew.Count;
            for (int i = 0; i < count; ++i)
            {
                var c = crew[i];
                var lsKerbal = LifeSupportManager.Instance.FetchKerbal(c);

                // Kerbals get healed either when they are tourists or when their LastAtHome or TimeEnteredVessel lie in the past
                if (string.IsNullOrEmpty(RestrictedToClass) || c.experienceTrait.Config.Name == RestrictedToClass || lsKerbal.LastAtHome < now || lsKerbal.TimeEnteredVessel < now)
                    kerbals.Add(lsKerbal);
            }

            if (kerbals.Count == 0)
                return;

            var timePerKerbal = baseTime / kerbals.Count;

            count = kerbals.Count;
            for (int i = 0; i < count; ++i)
            {
                var lsKerbal = kerbals[i];
                if (AffectsHomeTimer)
                {
                    // Calculate time adjustment value
                    var delta = timePerKerbal;
                    if (now - lsKerbal.LastAtHome > 0 && now - lsKerbal.LastAtHome < delta)
                        delta = now - lsKerbal.LastAtHome;

                    // Adjust both values, that are responsible for the home timer and keep their interval (TotalHabTime of vessel) the same
                    lsKerbal.MaxOffKerbinTime += delta;
                    lsKerbal.LastAtHome += delta;
                    
                    // make sure that LastAtHome is not in the future
                    if (lsKerbal.LastAtHome > now)
                    {
                        lsKerbal.MaxOffKerbinTime -= lsKerbal.LastAtHome - now;
                        lsKerbal.LastAtHome = now;
                    }

                    // make sure that MaxOffKerbinTime is not too far in the future, but adjusted to the habTime of the current vessel
                    if (habTime > 0.0f && now + habTime < lsKerbal.MaxOffKerbinTime)
                        lsKerbal.MaxOffKerbinTime = now + habTime;
                }
                if (AffectsHabTimer)
                {
                    lsKerbal.TimeEnteredVessel += timePerKerbal;
                    if (lsKerbal.TimeEnteredVessel > now)
                        lsKerbal.TimeEnteredVessel = now;
                }

                LifeSupportManager.Instance.TrackKerbal(lsKerbal);
            }
        }
    }
}
