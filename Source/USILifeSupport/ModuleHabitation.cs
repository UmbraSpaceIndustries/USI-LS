using System;
using System.Text;
using USITools.Logistics;

namespace LifeSupport
{
    public class ModuleHabitation : BaseConverter
    {
        [KSPField] 
        public double BaseKerbalMonths = 1;
        [KSPField]
        public double CrewCapacity = 10;
        [KSPField] 
        public double BaseHabMultiplier = 0;

        [KSPField(isPersistant = true)]
        public bool HabIsActive = false;

        public double HabMultiplier
        {
            get
            {
                if (HabIsActive || HighLogic.LoadedSceneIsEditor)
                    return BaseHabMultiplier;

                return 0f;
            }
        }

        public double KerbalMonths
        {
            get
            {
                if (HabIsActive || HighLogic.LoadedSceneIsEditor)
                    return BaseKerbalMonths;
                return 0f;
            }
        }

        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            HabIsActive = result.TimeFactor > ResourceUtilities.FLOAT_TOLERANCE;
        }

        public override string GetInfo()
        {
            var output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.Append(String.Format("Kerbal-Months: {0}", KerbalMonths + part.CrewCapacity));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Crew Affected: {0}", CrewCapacity));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Hab Multipler: {0}", HabMultiplier));
            output.Append(Environment.NewLine);
            return output.ToString();
        }
    }
}