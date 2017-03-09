using System;
using System.Text;
using USITools;

namespace LifeSupport
{
    public class ModuleLifeSupportRecycler : ModuleResourceConverter_USI
    {
        [KSPField] 
        public float CrewCapacity = 1f;

        [KSPField] 
        public float RecyclePercent = 0f;

        [KSPField(isPersistant = true)] 
        public bool RecyclerIsActive = false;

        protected override void PreProcessing()
        {
            base.PreProcessing();
            EfficiencyBonus = 1f;
        }

        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            base.PostProcess(result, deltaTime);
            RecyclerIsActive = result.TimeFactor > ResourceUtilities.FLOAT_TOLERANCE;
        }
        
        public override string GetInfo()
        {
            var output = new StringBuilder();
            output.Append(base.GetInfo());
            output.Append(Environment.NewLine);
            output.Append(String.Format("Recycler Percent: {0}%", RecyclePercent * 100));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Crew Affected: {0}", CrewCapacity));
            output.Append(Environment.NewLine);
            return output.ToString();
        }
    }
}