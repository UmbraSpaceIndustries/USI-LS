using System;
using System.Text;
using USITools;
using USITools.Logistics;

namespace LifeSupport
{
    public class ModuleHabitation : ModuleResourceConverter_USI
    {
        [KSPField] 
        public double BaseKerbalMonths = 1;
        [KSPField]
        public double CrewCapacity = 10;
        [KSPField] 
        public double BaseHabMultiplier = 0;

        [KSPField(isPersistant = true)]
        public bool HabIsActive = false;

        public double HabAdjustment
        {
            get { return HighLogic.LoadedSceneIsEditor ? 1d : _habAdjustment; }
            set { _habAdjustment = value;  }
        }
        private double _habAdjustment;

        public double HabMultiplier
        {

            get
            {
                if ((HabIsActive && IsActivated) || HighLogic.LoadedSceneIsEditor)
                    return BaseHabMultiplier * HabAdjustment;
                return 0f;
            }
        }

        public double KerbalMonths
        {
            get
            {
                if (HighLogic.LoadedSceneIsEditor)
                    return BaseKerbalMonths;
                if (!HabIsActive || !IsActivated)
                    HabAdjustment = 0d;
                return BaseKerbalMonths* HabAdjustment;
            }
        }

        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            base.PostProcess(result,deltaTime);
            HabIsActive = result.TimeFactor > ResourceUtilities.FLOAT_TOLERANCE;
            HabAdjustment = result.TimeFactor/deltaTime;
        }

        public override string GetInfo()
        {
            var output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.Append(String.Format("Kerbal-Months: {0}", BaseKerbalMonths + part.CrewCapacity));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Crew Affected: {0}", CrewCapacity));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Hab Multipler: {0}", BaseHabMultiplier));
            output.Append(Environment.NewLine);
            return output.ToString();
        }
    }
}