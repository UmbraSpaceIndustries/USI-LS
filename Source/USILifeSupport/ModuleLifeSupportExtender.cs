using System.Linq;
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

        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            base.PostProcess(result, deltaTime);
            var baseTime = TimeMultiplier*result.TimeFactor;
            var kerbals =
                vessel.GetVesselCrew()
                    .Where(c => string.IsNullOrEmpty(restrictedClass) || c.experienceTrait.Title == restrictedClass);
            if(PartOnly)
                kerbals = 
                    part.protoModuleCrew
                        .Where(c => string.IsNullOrEmpty(restrictedClass) || c.experienceTrait.Title == restrictedClass);

            var timePerKerbal = baseTime/kerbals.Count();

            foreach (var k in kerbals)
            {
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