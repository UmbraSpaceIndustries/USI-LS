using System;
using System.Text;

namespace LifeSupport
{
    public class ModuleLifeSupportRecycler : ModuleResourceConverter
    {
        [KSPField] 
        public float CrewCapacity = 1f;

        [KSPField] 
        public float RecyclePercent = 0f;

        [KSPField(isPersistant = true)] 
        public bool RecyclerIsActive = false;

        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            var diff = Math.Abs(deltaTime - result.TimeFactor);
            RecyclerIsActive = diff < 0.00001f;
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