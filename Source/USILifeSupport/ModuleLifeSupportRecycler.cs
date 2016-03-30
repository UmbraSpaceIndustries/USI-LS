using System;

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
    }
}