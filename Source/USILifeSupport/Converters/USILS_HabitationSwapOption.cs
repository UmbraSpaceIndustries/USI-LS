using System.Text;
using USITools;

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
            output.AppendLine(string.Format("Kerbal-Months: {0}", BaseKerbalMonths));
            output.AppendLine(string.Format("Crew Affected: {0}", CrewCapacity));
            output.AppendLine(string.Format("Hab Multipler: {0}", BaseHabMultiplier));

            return output.ToString();
        }
    }
}
