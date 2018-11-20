using USITools;

namespace LifeSupport
{
    public class USILS_LifeSupportRecyclerConverterAddon : AbstractConverterAddon<USI_Converter>
    {
        public float CrewCapacity = 1f;
        public float RecyclePercent = 0f;
        public bool IsOperational = false;

        public USILS_LifeSupportRecyclerConverterAddon(USI_Converter converter) : base(converter) { }

        public override void PostProcess(ConverterResults result, double deltaTime)
        {
            base.PostProcess(result, deltaTime);
            IsOperational = result.TimeFactor > ResourceUtilities.FLOAT_TOLERANCE;
        }
    }
}
