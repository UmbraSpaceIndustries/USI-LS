using System;
using System.Text;

namespace LifeSupport
{
    public class ModuleLifeSupportRecycler : ModuleResourceConverter
    {
        [KSPField] 
        public int CrewCapacity = 1;

        [KSPField] 
        public float RecyclePercent = 0f;

        [KSPField(isPersistant = true)] 
        public bool RecyclerIsActive = false;

        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            RecyclerIsActive = Math.Abs(result.TimeFactor - deltaTime) < ResourceUtilities.FLOAT_TOLERANCE;
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