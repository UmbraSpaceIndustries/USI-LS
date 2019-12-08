using System.Text;
using USITools;
using KSP.Localization;

namespace LifeSupport
{
    public class USILS_LifeSupportExtenderSwapOption : USI_ConverterSwapOption
    {
        [KSPField]
        public float TimeMultiplier = 1f;

        [KSPField]
        public bool AffectsPartOnly = false;

        [KSPField]
        public string RestrictedToClass = "";

        [KSPField]
        public bool AffectsHomeTimer = true;

        [KSPField]
        public bool AffectsHabTimer = true;

        public override void ApplyConverterChanges(USI_Converter converter)
        {
            UseEfficiencyBonus = false;

            converter.Addons.Add(new USILS_LifeSupportExtenderConverterAddon(converter)
            {
                TimeMultiplier = TimeMultiplier,
                AffectsPartOnly = AffectsPartOnly,
                RestrictedToClass = RestrictedToClass,
                AffectsHomeTimer = AffectsHomeTimer,
                AffectsHabTimer = AffectsHabTimer
            });

            base.ApplyConverterChanges(converter);
        }

        public override string GetInfo()
        {
            var output = new StringBuilder();
            output.AppendLine(base.GetInfo());
            output.AppendLine(Localizer.Format("#LOC_USILS_EOInfo1"));//"Pushes back onboard kerbals hab/home timers"
            output.AppendLine(Localizer.Format("#LOC_USILS_EOInfo2",TimeMultiplier));//string.Format("Rated for: {0} kerbals", )
            if (AffectsPartOnly)
            {
                output.AppendLine(Localizer.Format("#LOC_USILS_EOInfo3"));//"Effects only kerbals in this part"
            }
            if (!string.IsNullOrEmpty(RestrictedToClass))
            {
                output.AppendLine(Localizer.Format("#LOC_USILS_EOInfo4",RestrictedToClass));//string.Format("Effects only {0}s", )
            }

            return output.ToString();
        }
    }
}
