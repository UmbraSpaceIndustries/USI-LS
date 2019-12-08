using System.Text;
using USITools;
using KSP.Localization;

namespace LifeSupport
{
    public class USILS_HabitationSwapOption : USI_ConverterSwapOption
    {
        [KSPField]
        public double BaseKerbalMonths = 1;

        [KSPField]
        public double CrewCapacity = 10;

        [KSPField]
        public double BaseHabMultiplier = 0;

        public override void ApplyConverterChanges(USI_Converter converter)
        {
            UseEfficiencyBonus = false;

            converter.Addons.Add(new USILS_HabitationConverterAddon(converter)
            {
                BaseHabMultiplier = BaseHabMultiplier,
                BaseKerbalMonths = BaseKerbalMonths,
                CrewCapacity = CrewCapacity
            });

            base.ApplyConverterChanges(converter);
        }

        public override string GetInfo()
        {
            var output = new StringBuilder();
            output.AppendLine();
            output.AppendLine(base.GetInfo());
            output.AppendLine(Localizer.Format("#LOC_USILS_HOInfo1", BaseKerbalMonths + part.CrewCapacity));//string.Format("Kerbal-Months: {0}", )
            output.AppendLine(Localizer.Format("#LOC_USILS_HOInfo2", CrewCapacity));//string.Format("Crew Affected: {0}", )
            output.AppendLine(Localizer.Format("#LOC_USILS_HOInfo3", BaseHabMultiplier));//string.Format("Hab Multipler: {0}", )

            return output.ToString();
        }
    }
}
