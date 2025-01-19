using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using USITools;


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
            var count = LifeSupportInfo.Count;
            for (int i = 0; i < count; ++i)
            {
                if (LifeSupportInfo[i].KerbalName == kname)
                    return true;
            }
            return false;
        }

        public bool IsVesselTracked(string vesselId)
        {
            //Does a node exist?
            var count = VesselSupplyInfo.Count;
            for (int i = 0; i < count; ++i)
            {
                if (VesselSupplyInfo[i].VesselId == vesselId)
                    return true;
            }
            return false;
        }
        
        public void UntrackKerbal(string kname)
        {
            try
            {
                var count = LifeSupportInfo.Count;
                for (int i = 0; i < count; ++i)
                {
                    var kerbal = LifeSupportInfo[i];
                    if (kerbal.KerbalName == kname)
                    {
                        LifeSupportInfo.Remove(kerbal);
                        LifeSupportScenario.Instance.settings.DeleteStatusNode(kname);
                        return;
                    }
                }
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
                k.CurrentVesselId = "?UNKNOWN?";
                k.PreviousVesselId = "??UNKNOWN??";
                k.RemainingCabinTime = LifeSupportScenario.Instance.settings.GetSettings().BaseHabTime 
                    * LifeSupportUtilities.SecondsPerMonth();
                k.LastUpdate = Planetarium.GetUniversalTime();
                k.IsGrouchy = false;
                k.OldTrait = crew.experienceTrait.Config.Name;
                TrackKerbal(k);
            }

            LifeSupportStatus kerbal = null;
            var count = LifeSupportInfo.Count;
            for (int i = 0; i < count; ++i)
            {
                if (LifeSupportInfo[i].KerbalName == crew.name)
                {
                    kerbal = LifeSupportInfo[i];
                    break;
                }
            }
            return kerbal;
        }

        public void TrackKerbal(LifeSupportStatus status)
        {
            var count = LifeSupportInfo.Count;
            var isNew = true;
            for (int i = 0; i < count; ++i)
            {
                if (LifeSupportInfo[i].KerbalName == status.KerbalName)
                {
                    isNew = false;
                    break;
                }
            }
            if (isNew)
            {
                LifeSupportInfo.Add(status);
            }
            LifeSupportScenario.Instance.settings.SaveStatusNode(status);
        }

        public void TrackVessel(VesselSupplyStatus status)
        {
            var count = VesselSupplyInfo.Count;
            var isNew = true;
            for (int i = 0; i < count; ++i)
            {
                if (VesselSupplyInfo[i].VesselId == status.VesselId)
                {
                    isNew = false;
                    break;
                }
            }
            if (isNew)
            {
                VesselSupplyInfo.Add(status);
            }
            LifeSupportScenario.Instance.settings.SaveVesselNode(status);
        }

        public void UntrackVessel(string vesselId)
        {
            if (!IsVesselTracked(vesselId))
                return;
            var count = VesselSupplyInfo.Count;
            for (int i = count; i --> 0;)
            {
                var v = VesselSupplyInfo[i];
                if (v.VesselId == vesselId)
                {
                    VesselSupplyInfo.Remove(v);
                    break;
                }
            }
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
                v.CachedHabTime = 0;
                v.ExtraHabSpace = 0;
                v.SuppliesLeft = 0f;
                v.ECLeft = 0f;
                v.VesselId = vesselId;
                v.VesselName = "??loading??";
                TrackVessel(v);
            }


            VesselSupplyStatus vInfo = null;
            var count = VesselSupplyInfo.Count;
            for (int i = 0; i < count; ++i)
            {
                if (VesselSupplyInfo[i].VesselId == vesselId)
                {
                    vInfo = VesselSupplyInfo[i];
                    break;
                }
            }
            return vInfo;
        }


        public static bool isVet(string kName)
        {
            var firstname = kName.Replace(" Kerman", "");
            return (LifeSupportScenario.Instance.settings.GetSettings().VetNames.Contains(firstname));
        }

        public static int GetNoSupplyEffect(string kName)
        {
            return isVet(kName) ?
                LifeSupportScenario.Instance.settings.GetSettings().NoSupplyEffectVets
                : LifeSupportScenario.Instance.settings.GetSettings().NoSupplyEffect;
        }

        public static int GetNoHomeEffect(string kName)
        {
            return isVet(kName) ?
                LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffectVets
                    : LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffect;
        }

        public static int GetNoECEffect(string kName)
        {
            return isVet(kName) ?
                LifeSupportScenario.Instance.settings.GetSettings().NoECEffectVets
                    : LifeSupportScenario.Instance.settings.GetSettings().NoECEffect;
        }

        public static int GetEVAExcessEffect(string kName)
        {
            return isVet(kName) ?
                LifeSupportScenario.Instance.settings.GetSettings().EVAEffectVets
                    : LifeSupportScenario.Instance.settings.GetSettings().EVAEffect;
        }

        internal void UpdateVesselStats()
        {
            //Clear stuff that is gone.
            var badIDs = new List<string>();
            var vCount = FlightGlobals.Vessels.Count;
            var vsCount = Instance.VesselSupplyInfo.Count;

            for(int x = 0; x <vsCount; ++x)
            {
                var vInfo = Instance.VesselSupplyInfo[x];
                Vessel vsl = null;
                for (int i = 0; i < vCount; ++i)
                {
                    var v = FlightGlobals.Vessels[i];
                    if(v.id.ToString() == vInfo.VesselId)
                    {
                        vsl = v;
                        break;
                    }
                }

                if (vsl == null || vInfo.NumCrew == 0)
                {
                    badIDs.Add(vInfo.VesselId);
                }
            }
            var bCount = badIDs.Count;
            for (int q = 0; q < bCount; ++q)
            {
                var id = badIDs[q];
                Instance.UntrackVessel(id);
            }
        }

        private static int GetColonyCrewCount(Vessel vsl)
        {
            var crewCount = vsl.GetCrewCount();
            var vList = LogisticsTools.GetNearbyVessels((float)LifeSupportScenario.Instance.settings.GetSettings().HabRange, false, vsl, false);
            var count = vList.Count;
            for (int i = 0; i < count; ++i)
            {
                var v = vList[i];
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

            var recyclers = vessel.FindConverterAddonsImplementing<USILS_LifeSupportRecyclerConverterAddon>();
            for (int i = 0; i < recyclers.Count; ++i)
            {
                var recycler = recyclers[i];
                if (recycler.IsActive && recycler.IsOperational)
                {
                    if (recycler.RecyclePercent > recyclerCap)
                        recyclerCap = recycler.RecyclePercent;
                    var recPercent = recycler.RecyclePercent;
                    if (recycler.CrewCapacity < crewCount)
                        recPercent *= recycler.CrewCapacity/(float) crewCount;

                    recyclerTot += recPercent;
                }
            }

            var vessels = LogisticsTools.GetNearbyVessels((float)LifeSupportScenario.Instance.settings.GetSettings().HabRange, false, vessel, false);
            for(int i = 0; i < vessels.Count; ++i)
            {
                var v = vessels[i];
                var nearbyRecyclers = v.FindConverterAddonsImplementing<USILS_LifeSupportRecyclerConverterAddon>();
                for (int x = 0; x < nearbyRecyclers.Count; ++x)
                {
                    var recycler = nearbyRecyclers[x];
                    if (recycler.IsActive && recycler.IsOperational)
                    {
                        if (recycler.RecyclePercent > recyclerCap)
                            recyclerCap = recycler.RecyclePercent;
                        var recPercent = recycler.RecyclePercent;
                        if (recycler.CrewCapacity < crewCount)
                            recPercent *= recycler.CrewCapacity / (float)crewCount;

                        recyclerTot += recPercent;
                    }
                }
            }

            //Inverse because this is a multiplier - low is good!                
            double multiplier = 1d - (Math.Min(recyclerTot, recyclerCap));
            return multiplier;
        }

        internal static double GetTotalHabTime(VesselSupplyStatus sourceVessel, Vessel vsl)
        {
            return GetTotalHabTime(sourceVessel, vsl, out int numSharedVessels);
        }

        internal static double GetHabChecksum(VesselSupplyStatus sourceVessel, Vessel vsl)
        {
            //This routine just returns the total amount of hab factored by the multiplier.
            //It is used to determine if there was a situation change and thus reset the vessel time.
            //The main use case would be undocking/docking, or flying into range of a base.

            //In the event that a vessel is not loaded, return zero.
            if (!vsl.loaded)
            {
                return 0d;
            }

            int totMaxCrew = sourceVessel.CrewCap;
            var vList = LogisticsTools.GetNearbyVessels((float)LifeSupportScenario.Instance.settings.GetSettings().HabRange, false, vsl, false);
            var hList = new List<Vessel>();
            var vCount = vList.Count;
            for (int i = 0; i < vCount; ++i)
            {
                var v = vList[i];
                int crewCap = v.GetCrewCapacity();
                totMaxCrew += crewCap;
                if (crewCap > 0)
                {
                    hList.Add(v);
                }
            }

            double totHabSpace = sourceVessel.ExtraHabSpace;
            double totHabMult = CalculateVesselHabMultiplier(vsl, 1);

            var hCount = hList.Count;
            for (int i = 0; i < hCount; ++i)
            {
                var v = hList[i];
                totHabSpace += CalculateVesselHabExtraTime(v);
                totHabMult += CalculateVesselHabMultiplier(v, 1);
            }

            return totHabSpace*(totHabMult+1);
        }

         internal static double GetTotalHabTime(VesselSupplyStatus sourceVessel, Vessel vessel, out int numSharedVessels)
         {
            //In the event that a vessel is not loaded, we just return the cached value.
             if (!vessel.loaded)
             {
                numSharedVessels = 0;
                return sourceVessel.CachedHabTime;
            }

            int totCurCrew = sourceVessel.NumCrew;
            int totMaxCrew = sourceVessel.CrewCap;

            numSharedVessels = 0;

            var vList = LogisticsTools.GetNearbyVessels((float)LifeSupportScenario.Instance.settings.GetSettings().HabRange, false, vessel, false);
            var hList = new List<Vessel>();
            var vCount = vList.Count;
            for(int i = 0; i < vCount; ++i)
            {
                var v = vList[i];
                //Hab time starts with our baseline of the crew hab plus extra hab.
                //We then multiply it out based on the crew ratio, our global multiplier, and the vessel's multipler.
                //First - crew capacity. 
                int crewCap = v.GetCrewCapacity();
                totMaxCrew += crewCap;
                totCurCrew += v.GetCrewCount();
 
                if (crewCap > 0)
                {
                    numSharedVessels++;
                    hList.Add(v);
                }
            }
            double totHabSpace = sourceVessel.ExtraHabSpace;
            double totHabMult = CalculateVesselHabMultiplier(vessel,totCurCrew);

            var hCount = hList.Count;
            for (int i = 0; i < hCount; ++i)
            {
                var v = hList[i];
                // Calculate HabSpace and HabMult after we know totCurCrew and totMaxCrew
                totHabSpace += CalculateVesselHabExtraTime(v);
                totHabMult += CalculateVesselHabMultiplier(v, totCurCrew);
            }

            totHabMult += USI_GlobalBonuses.Instance.GetHabBonus(vessel.mainBody.flightGlobalsIndex);
            double habTotal = totHabSpace / (double)totCurCrew * (totHabMult + 1) * LifeSupportScenario.Instance.settings.GetSettings().HabMultiplier;
             //print(String.Format("THS: {0} TC:{1} THM: {2} HM: {3}", totHabSpace, totCurCrew, totHabMult, LifeSupportScenario.Instance.settings.GetSettings().HabMultiplier));
            sourceVessel.CachedHabTime = Math.Max(0, habTotal * LifeSupportUtilities.SecondsPerMonth());
            LifeSupportManager.Instance.TrackVessel(sourceVessel);
            return sourceVessel.CachedHabTime;
         }

        internal static double GetRecyclerMultiplierForParts(List<Part> parts, int crewCount)
        {
            if (!LifeSupportScenario.Instance.settings.GetSettings().EnableRecyclers)
                return 1d;

            var recyclerCap = 0f;
            var recyclerTot = 0f;

            for (int i = 0; i < parts.Count; ++i)
            {
                var part = parts[i];
                var recycler = part
                    .FindConverterAddonsImplementing<USILS_LifeSupportRecyclerConverterAddon>()
                    .FirstOrDefault();
                if (recycler == null) 
                    continue;

                if (!recycler.IsOperational && !HighLogic.LoadedSceneIsEditor)
                    continue;

                if (recycler.RecyclePercent > recyclerCap)
                    recyclerCap = recycler.RecyclePercent;
                var recPercent = recycler.RecyclePercent;
                if (recycler.CrewCapacity < crewCount)
                    recPercent *= recycler.CrewCapacity / (float)crewCount;

                recyclerTot += recPercent;
            }

            //Inverse because this is a multiplier - low is good!                
            double multiplier = 1d - (Math.Min(recyclerTot, recyclerCap));
            return multiplier;
        }
        public static bool IsOnKerbin(Vessel vessel)
        {
            return (vessel.mainBody == FlightGlobals.GetHomeBody() && vessel.altitude < LifeSupportScenario.Instance.settings.GetSettings().HomeWorldAltitude);
        }

        public static double CalculateVesselHabExtraTime(Vessel vessel)
        {
            var habTime = 0d;
            var habitats = vessel.FindConverterAddonsImplementing<USILS_HabitationConverterAddon>();
            for (int i = 0; i < habitats.Count; ++i)
            {
                var habitat = habitats[i];
                //Next.  Certain modules, in addition to crew capacity, have living space.
                habTime += habitat.KerbalMonths;
            }

            return habTime;
        }

        public static double CalculateVesselHabMultiplier(Vessel vessel, int crewCount)
        {
            if (crewCount == 0)
                return 0d;

            var habMultiplier = 0d;
            var habitats = vessel.FindConverterAddonsImplementing<USILS_HabitationConverterAddon>();
            var count = habitats.Count;
            for (int i = 0; i < count; ++i)
            {
                var habitat = habitats[i];
                habMultiplier += (habitat.HabMultiplier * Math.Min(1, habitat.CrewCapacity / crewCount));
            }

            return habMultiplier;
        }
    }
}

