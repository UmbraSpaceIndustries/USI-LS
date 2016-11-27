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

        private float _adjPercent;
        public float AdjustedRecyclePercent
        {
            get
            {
                if (!HighLogic.LoadedSceneIsFlight)
                    return RecyclePercent;
                if (!RecyclerIsActive || !IsActivated)
                    _adjPercent = 0f;
                return _adjPercent;
            }
            set
            {
                _adjPercent = value;
            }
        }

        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            base.PostProcess(result, deltaTime);
            RecyclerIsActive = result.TimeFactor > ResourceUtilities.FLOAT_TOLERANCE;
            AdjustedRecyclePercent = (float)(RecyclePercent*(result.TimeFactor/deltaTime));
        }
        
        public override string GetInfo()
        {
            var output = new StringBuilder();
            output.Append(base.GetInfo());
            output.Append(Environment.NewLine);
            output.Append(String.Format("Recycler Percent: {0}%", AdjustedRecyclePercent * 100));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Crew Affected: {0}", CrewCapacity));
            output.Append(Environment.NewLine);
            return output.ToString();
        }
    }
}