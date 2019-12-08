using System.Text;
using USITools;
using KSP.Localization;

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
            output.AppendLine(Localizer.Format("#LOC_USILS_LSRInfo1"));//"Reduces supplies consumption across Kolony"
            output.AppendLine(Localizer.Format("#LOC_USILS_LSRInfo2", RecyclePercent * 100));//string.Format("Recycler Percent: {0}%", )
            output.AppendLine(Localizer.Format("#LOC_USILS_LSRInfo3", CrewCapacity));//string.Format("Crew Affected: {0}", )

            return output.ToString();
        }
    }
}
