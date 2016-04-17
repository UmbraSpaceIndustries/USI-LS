using System;
using System.Text;
using USITools.Logistics;

namespace LifeSupport
{
    public class ModuleHabitation : PartModule
    {
        [KSPField] 
        public double KerbalMonths = 1;
        [KSPField]
        public double CrewCapacity = 10;
        [KSPField] 
        public double HabMultiplier = 0;

        public override string GetInfo()
        {
            var output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.Append(String.Format("Kerbal-Months: {0}", KerbalMonths + part.CrewCapacity));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Crew Affected: {0}", CrewCapacity));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Hab Multipler: {0}", HabMultiplier));
            output.Append(Environment.NewLine);
            return output.ToString();
        }
    }
}