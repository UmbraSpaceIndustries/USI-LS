using KolonyTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace LifeSupport
{
    public class LifeSupportManager : MonoBehaviour
    {
        // Static singleton instance
        private static LifeSupportManager instance;

        // Static singleton property
        public static LifeSupportManager Instance
        {
            get { return instance ?? (instance = new GameObject("LifeSupportManager").AddComponent<LifeSupportManager>()); }
        }

        //Backing variables
        private List<LifeSupportStatus> _LifeSupportInfo;
        private List<VesselSupplyStatus> _VesselSupplyInfo;

        public void ResetCache()
        {
            _LifeSupportInfo = null;
            _VesselSupplyInfo = null;
        }

        public List<LifeSupportStatus> LifeSupportInfo
        {
            get
            {
                if (_LifeSupportInfo == null)
                {
                    _LifeSupportInfo = new List<LifeSupportStatus>();
                    _LifeSupportInfo.AddRange(LifeSupportScenario.Instance.settings.GetStatusInfo());
                }
                return _LifeSupportInfo;
            }
        }

        public List<VesselSupplyStatus> VesselSupplyInfo
        {
            get
            {
                if (_VesselSupplyInfo == null)
                {
                    _VesselSupplyInfo = new List<VesselSupplyStatus>();
                    _VesselSupplyInfo.AddRange(LifeSupportScenario.Instance.settings.GetVesselInfo());
                }
                return _VesselSupplyInfo;
            }
        }

        public bool IsKerbalTracked(string kname)
        {
            //Does a node exist?
            return LifeSupportInfo.Any(n => n.KerbalName == kname);
        }

        public bool IsVesselTracked(string vesselId)
        {
            //Does a node exist?
            return VesselSupplyInfo.Any(n => n.VesselId == vesselId);
        }
        
        public void UntrackKerbal(string kname)
        {
            try
            {
                if (!IsKerbalTracked(kname))
                    return;
                var kerbal = LifeSupportInfo.First(k => k.KerbalName == kname);
                LifeSupportInfo.Remove(kerbal);
                //For saving to our scenario data
                LifeSupportScenario.Instance.settings.DeleteStatusNode(kname);
            }
            catch (Exception ex)
            {
                print(String.Format("ERROR {0} IN UntrackKerbal", ex.Message));
            }
        }
        public LifeSupportStatus FetchKerbal(ProtoCrewMember crew)
        {
            if (!IsKerbalTracked(crew.name))
            {
                var k = new LifeSupportStatus();
                k.KerbalName = crew.name;
                k.HomeBodyId = FlightGlobals.GetHomeBodyIndex();
                k.LastPlanet = FlightGlobals.GetHomeBodyIndex();
                k.LastMeal = Planetarium.GetUniversalTime();
                k.LastEC = Planetarium.GetUniversalTime();
                k.LastAtHome = Planetarium.GetUniversalTime();
                k.LastSOIChange = Planetarium.GetUniversalTime();
                k.MaxOffKerbinTime = 648000;    //TODO - make this configurable
                k.TimeEnteredVessel = Planetarium.GetUniversalTime();
                k.CurrentVesselId = "?UNKNOWN?";
                k.PreviousVesselId = "??UNKNOWN??";
                k.LastUpdate = Planetarium.GetUniversalTime();
                k.IsGrouchy = false;
                k.OldTrait = crew.experienceTrait.Title;
                TrackKerbal(k);
            }

            var kerbal = LifeSupportInfo.FirstOrDefault(k => k.KerbalName == crew.name);
            return kerbal;
        }

        public void TrackKerbal(LifeSupportStatus status)
        {
            if (LifeSupportInfo.All(n => n.KerbalName != status.KerbalName))
            {
                LifeSupportInfo.Add(status);
            }
            LifeSupportScenario.Instance.settings.SaveStatusNode(status);
        }

        public void TrackVessel(VesselSupplyStatus status)
        {
            if(VesselSupplyInfo.All(n => n.VesselId != status.VesselId))
            VesselSupplyInfo.Add(status);
            LifeSupportScenario.Instance.settings.SaveVesselNode(status);
        }

        public void UntrackVessel(string vesselId)
        {
            //print("Untracking " + vesselId);
            if (!IsVesselTracked(vesselId))
                return;

            //print("Finding " + vesselId);
            var vInfo = VesselSupplyInfo.First(v => v.VesselId == vesselId);
            //print("Removing " + vesselId);
            VesselSupplyInfo.Remove(vInfo);
            //For saving to our scenario data
            //print("Deleting " + vesselId);
            LifeSupportScenario.Instance.settings.DeleteVesselNode(vesselId);
        }
        public VesselSupplyStatus FetchVessel(string vesselId)
        {
            if (!IsVesselTracked(vesselId))
            {
                var v = new VesselSupplyStatus();
                v.LastFeeding = Planetarium.GetUniversalTime();
                v.LastECCheck = Planetarium.GetUniversalTime();
                v.LastUpdate = Planetarium.GetUniversalTime();
                v.NumCrew = 0;
                v.RecyclerMultiplier = 1;
                v.CrewCap = 0;
                v.VesselHabMultiplier = 0;
                v.ExtraHabSpace = 0;
                v.SuppliesLeft = 0f;
                v.ECLeft = 0f;
                v.VesselId = vesselId;
                v.VesselName = "??loading??";
                TrackVessel(v);
            }

            var vInfo = VesselSupplyInfo.FirstOrDefault(k => k.VesselId == vesselId);
            return vInfo;
        }


        public static bool isVet(string kName)
        {
            var firstname = kName.Replace(" Kerman", "");
            return (LifeSupportScenario.Instance.settings.GetSettings().VetNames.Contains(firstname));
        }


        internal void UpdateVesselStats()
        {
            //Clear stuff that is gone.
            var badIDs = new List<string>();
            foreach (var vInfo in Instance.VesselSupplyInfo)
            {
                //print("Checking " + vInfo.VesselId);
                var vsl = FlightGlobals.Vessels.FirstOrDefault(v => v.id.ToString() == vInfo.VesselId);
                //print("Finding vessel " + vInfo.VesselId);
                if (vsl == null || vInfo.NumCrew == 0)
                {
                    //print("Adding bad ID " + vInfo.VesselId);
                    badIDs.Add(vInfo.VesselId);
                }
               // else
                //{
                    //print("Found " + vInfo.VesselId);
                //}
            }
            //print("START COUNT: " + Instance.VesselSupplyInfo.Count);
            foreach (var id in badIDs)
            {
                //print("Removing " + id);
                Instance.UntrackVessel(id);
            }
            //print("END COUNT: " + Instance.VesselSupplyInfo.Count);
        }

        private static int GetColonyCrewCount(Vessel vsl)
        {
            var crewCount = vsl.GetCrewCount();
            var vList = LogisticsTools.GetNearbyVessels((float)LifeSupportScenario.Instance.settings.GetSettings().HabRange, false, vsl, false);
            foreach (var v in vList)
            {
                crewCount += v.GetCrewCount();
            }
            return crewCount;
        }

        internal static double GetRecyclerMultiplier(Vessel vessel)
        {
            if (!LifeSupportScenario.Instance.settings.GetSettings().EnableRecyclers)
                return 1d;

            var recyclerCap = 0f;
            var recyclerTot = 0f;
            var crewCount = GetColonyCrewCount(vessel);

            foreach (var r in vessel.FindPartModulesImplementing<ModuleLifeSupportRecycler>())
            {
                if (r.RecyclerIsActive && r.IsActivated)
                {
                    if (r.AdjustedRecyclePercent > recyclerCap)
                        recyclerCap = r.AdjustedRecyclePercent;
                    var recPercent = r.AdjustedRecyclePercent;
                    if (r.CrewCapacity < crewCount)
                        recPercent *= r.CrewCapacity/(float) crewCount;

                    recyclerTot += recPercent;
                }
            }

            var vList = LogisticsTools.GetNearbyVessels((float)LifeSupportScenario.Instance.settings.GetSettings().HabRange, false, vessel, false);
            foreach (var v in vList)
            {
                foreach (var r in v.FindPartModulesImplementing<ModuleLifeSupportRecycler>())
                {
                    if (r.IsActivated && r.RecyclerIsActive)
                    {
                        if (r.AdjustedRecyclePercent > recyclerCap)
                            recyclerCap = r.AdjustedRecyclePercent;
                        var recPercent = r.AdjustedRecyclePercent;
                        if (r.CrewCapacity < crewCount)
                            recPercent *= r.CrewCapacity / (float)crewCount;

                        recyclerTot += recPercent;
                    }
                }
            } 
            //Inverse because this is a multiplier - low is good!                
            double retVal = 1d - (Math.Min(recyclerTot, recyclerCap));
            return retVal;
        }


         internal static double GetTotalHabTime(VesselSupplyStatus sourceVessel)
         {
             int numSharedVessels = 0;
             return GetTotalHabTime(sourceVessel, out numSharedVessels);
         }
 
         internal static double GetTotalHabTime(VesselSupplyStatus sourceVessel, out int numSharedVessels)
         {
            var vsl = FlightGlobals.Vessels.FirstOrDefault(v => v.id.ToString() == sourceVessel.VesselId);
            double totHabSpace = (LifeSupportScenario.Instance.settings.GetSettings().BaseHabTime * sourceVessel.CrewCap) + sourceVessel.ExtraHabSpace;
            double totHabMult = sourceVessel.VesselHabMultiplier;

            int totCurCrew = sourceVessel.NumCrew;
            int totMaxCrew = sourceVessel.CrewCap;
            numSharedVessels = 0;

            var vList = LogisticsTools.GetNearbyVessels((float)LifeSupportScenario.Instance.settings.GetSettings().HabRange, false, vsl, false);
            foreach (var v in vList)
            {
                //Hab time starts with our baseline of the crew hab plus extra hab.
                //We then multiply it out based on the crew ratio, our global multiplier, and the vessel's multipler.
                //First - crew capacity. 
                int crewCap = v.GetCrewCapacity();
                totMaxCrew += crewCap;
                totCurCrew += v.GetCrewCount();
 
                if (crewCap > 0)
                {
                    numSharedVessels++;
                }
            }

            foreach (var v in vList)
            {
               // Calculate HabSpace and HabMult after we know totCurCrew and totMaxCrew
               totHabSpace += (LifeSupportScenario.Instance.settings.GetSettings().BaseHabTime * totMaxCrew) + CalculateVesselHabExtraTime(v);
               totHabMult += CalculateVesselHabMultiplier(v, totCurCrew);         
            }
            totHabMult += USI_GlobalBonuses.Instance.GetHabBonus(vsl.mainBody.flightGlobalsIndex);
            double habTotal = totHabSpace / (double)totCurCrew * (totHabMult + 1) * LifeSupportScenario.Instance.settings.GetSettings().HabMultiplier;
             //print(String.Format("THS: {0} TC:{1} THM: {2} HM: {3}", totHabSpace, totCurCrew, totHabMult, LifeSupportScenario.Instance.settings.GetSettings().HabMultiplier));

            return Math.Max(0,habTotal * LifeSupportUtilities.SecondsPerMonth());
        }

        internal static double GetRecyclerMultiplierForParts(List<Part> pList, int crewCount)
        {
            if (!LifeSupportScenario.Instance.settings.GetSettings().EnableRecyclers)
                return 1d;

            var recyclerCap = 0f;
            var recyclerTot = 0f;

            foreach (var p in pList)
            {
                var mod = p.FindModuleImplementing<ModuleLifeSupportRecycler>();
                if (mod == null) 
                    continue;

                if (!mod.RecyclerIsActive && !HighLogic.LoadedSceneIsEditor)
                    continue;

                if (mod.AdjustedRecyclePercent > recyclerCap)
                    recyclerCap = mod.AdjustedRecyclePercent;
                var recPercent = mod.AdjustedRecyclePercent;
                if (mod.CrewCapacity < crewCount)
                    recPercent *= mod.CrewCapacity / (float)crewCount;

                recyclerTot += recPercent;
            }
            //Inverse because this is a multiplier - low is good!                
            double retVal = 1d - (Math.Min(recyclerTot, recyclerCap));
            return retVal;
        }
        public static bool IsOnKerbin(Vessel v)
        {
            return (v.mainBody == FlightGlobals.GetHomeBody() && v.altitude < LifeSupportScenario.Instance.settings.GetSettings().HomeWorldAltitude);
        }

        public static double CalculateVesselHabExtraTime(Vessel v)
        {
            var habTime = 0d;
            foreach (var hab in v.FindPartModulesImplementing<ModuleHabitation>())
            {
                //Next.  Certain modules, in addition to crew capacity, have living space.
                habTime += hab.KerbalMonths;
            }
            return habTime;
        }

        public static double CalculateVesselHabMultiplier(Vessel v, int numCrew)
        {
            var habMulti = 0d;
            foreach (var hab in v.FindPartModulesImplementing<ModuleHabitation>())
            {
                //Lastly.  Some modules act more as 'multipliers', dramatically extending a hab's workable lifespan.
                habMulti += (hab.HabMultiplier * Math.Min(1, hab.CrewCapacity / numCrew));
            }
            return habMulti;
        }
    }
}

