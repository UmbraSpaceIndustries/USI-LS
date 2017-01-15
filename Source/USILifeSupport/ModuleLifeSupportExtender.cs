using System;
using System.Collections.Generic;
using USITools;

namespace LifeSupport
{
    public class ModuleLifeSupportExtender : ModuleResourceConverter_USI
    {
        [KSPField]
        public float TimeMultiplier= 1f;

        [KSPField]
        public bool PartOnly = false;

        [KSPField]
        public string restrictedClass = "";

        [KSPField]
        public bool homeTimer = true;

        [KSPField]
        public bool habTimer = true;

        protected override void PreProcessing()
        {
            base.PreProcessing();
            var v = LifeSupportManager.Instance.FetchVessel(vessel.id.ToString());
            var e = 1d;
            if (v != null)
            {
                e += v.VesselHabMultiplier;
            }
            EfficiencyBonus = (float)e;
        }


        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            base.PostProcess(result, deltaTime);
            var baseTime = TimeMultiplier*result.TimeFactor;
            var kerbals = new List<ProtoCrewMember>();
            var crew = vessel.GetVesselCrew();
            if (PartOnly)
                crew = part.protoModuleCrew;

            var count = crew.Count;
            for (int i = 0; i < count; ++i)
            {
                var c = crew[i];
                if(string.IsNullOrEmpty(restrictedClass) || c.experienceTrait.Title == restrictedClass)
                    kerbals.Add(c);
            }

            var timePerKerbal = baseTime/kerbals.Count;

            count = kerbals.Count;
            for(int i = 0; i < count; ++i)
            {
                var k = kerbals[i];
                var lsKerbal = LifeSupportManager.Instance.FetchKerbal(k);
                if(homeTimer)
                    lsKerbal.MaxOffKerbinTime += timePerKerbal;
                if(habTimer)
                    lsKerbal.TimeEnteredVessel += timePerKerbal;

                LifeSupportManager.Instance.TrackKerbal(lsKerbal);
            }
        }
    }
}