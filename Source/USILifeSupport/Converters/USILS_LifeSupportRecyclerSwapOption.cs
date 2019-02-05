using System.Text;
using USITools;

namespace LifeSupport
{
    public class USILS_LifeSupportRecyclerSwapOption : USI_ConverterSwapOption
    {
        [KSPField]
        public float CrewCapacity = 1f;

        [KSPField]
        public float RecyclePercent = 0f;

        public override void ApplyConverterChanges(USI_Converter converter)
        {
            UseEfficiencyBonus = false;

            converter.Addons.Add(new USILS_LifeSupportRecyclerConverterAddon(converter)
            {
                CrewCapacity = CrewCapacity,
                RecyclePercent = RecyclePercent
            });
            base.ApplyConverterChanges(converter);
        }

        public override string GetInfo()
        {
            var output = new StringBuilder();
            output.AppendLine(base.GetInfo());
            output.AppendLine("Reduces supplies consumption across Kolony");
            output.AppendLine(string.Format("Recycler Percent: {0}%", RecyclePercent * 100));
            output.AppendLine(string.Format("Crew Affected: {0}", CrewCapacity));

            return output.ToString();
        }
    }
}
