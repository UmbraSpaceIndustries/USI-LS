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
            var kerbals = new List<ProtoCrewMember>();
            var crew = Converter.vessel.GetVesselCrew();
            if (AffectsPartOnly)
                crew = Converter.part.protoModuleCrew;

            var count = crew.Count;
            for (int i = 0; i < count; ++i)
            {
                var c = crew[i];
                if (string.IsNullOrEmpty(RestrictedToClass) || c.experienceTrait.Config.Name == RestrictedToClass)
                    kerbals.Add(c);
            }

            if (kerbals.Count == 0)
                return;

            var timePerKerbal = baseTime / kerbals.Count;

            count = kerbals.Count;
            for (int i = 0; i < count; ++i)
            {
                var k = kerbals[i];
                var lsKerbal = LifeSupportManager.Instance.FetchKerbal(k);

                // Experimental patch.
                // I don't like changing LastAtHome but the alternative is that we add another variable...
                if (AffectsHomeTimer || AffectsHabTimer)
                    lsKerbal.LastAtHome += timePerKerbal;

                LifeSupportManager.Instance.TrackKerbal(lsKerbal);
            }
        }
    }
}
