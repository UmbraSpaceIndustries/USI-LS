using USITools;

namespace LifeSupport
{
    public class USILS_HabitationConverterAddon : AbstractConverterAddon<USI_Converter>
    {
        public double BaseKerbalMonths = 1;
        public double CrewCapacity = 10;
        public double BaseHabMultiplier = 0;
        public bool HabIsActive = false;

        private double _habAdjustment = 1d;
        public double HabAdjustment
        {
            get
            {
                return HighLogic.LoadedSceneIsEditor ? 1d : _habAdjustment;
            }
            set { _habAdjustment = value; }
        }

        public double HabMultiplier
        {
            get
            {
                if ((HabIsActive && IsActive) || HighLogic.LoadedSceneIsEditor)
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

                if (!HabIsActive || !IsActive)
                    HabAdjustment = 0d;

                return BaseKerbalMonths * HabAdjustment;
            }
        }

        public USILS_HabitationConverterAddon(USI_Converter converter) : base(converter) { }

        public override void PostProcess(ConverterResults result, double deltaTime)
        {
            base.PostProcess(result, deltaTime);

            HabIsActive = result.TimeFactor > ResourceUtilities.FLOAT_TOLERANCE;
            HabAdjustment = result.TimeFactor / deltaTime;
        }
    }
}
