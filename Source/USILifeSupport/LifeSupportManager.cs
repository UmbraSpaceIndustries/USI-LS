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
            if (!IsKerbalTracked(kname))
                return;
            var kerbal = LifeSupportInfo.First(k => k.KerbalName == kname);
            LifeSupportInfo.Remove(kerbal);
            //For saving to our scenario data
            LifeSupportScenario.Instance.settings.DeleteStatusNode(kerbal);
        }
        public LifeSupportStatus FetchKerbal(ProtoCrewMember crew)
        {
            if (!IsKerbalTracked(crew.name))
            {
                var k = new LifeSupportStatus();
                k.KerbalName = crew.name;
                k.LastMeal = Planetarium.GetUniversalTime();
                k.LastOnKerbin = Planetarium.GetUniversalTime();
                k.MaxOffKerbinTime = Planetarium.GetUniversalTime() + 648000;
                k.TimeInVessel = 0d;
                k.LastVesselId = "??UNKNOWN??";
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
            LifeSupportStatus kerbInfo =
                LifeSupportInfo.FirstOrDefault(n => n.KerbalName == status.KerbalName);
            if (kerbInfo == null)
            {
                kerbInfo = new LifeSupportStatus();
                kerbInfo.KerbalName = status.KerbalName;
                LifeSupportInfo.Add(kerbInfo);
            }
            kerbInfo.LastMeal = status.LastMeal;
            kerbInfo.LastOnKerbin = status.LastOnKerbin;
            kerbInfo.MaxOffKerbinTime = status.MaxOffKerbinTime;
            kerbInfo.LastVesselId = status.LastVesselId;
            kerbInfo.TimeInVessel = status.TimeInVessel;
            kerbInfo.LastUpdate = status.LastUpdate;
            kerbInfo.IsGrouchy = status.IsGrouchy;
            kerbInfo.OldTrait = status.OldTrait;
            LifeSupportScenario.Instance.settings.SaveStatusNode(status);
        }

        public void TrackVessel(VesselSupplyStatus status)
        {
            VesselSupplyStatus vesselInfo =
                VesselSupplyInfo.FirstOrDefault(n => n.VesselId == status.VesselId);
            if (vesselInfo == null)
            {
                vesselInfo = new VesselSupplyStatus();
                vesselInfo.VesselId = status.VesselId;
                VesselSupplyInfo.Add(vesselInfo);
            }
            vesselInfo.VesselName = status.VesselName;
            vesselInfo.LastFeeding = status.LastFeeding;
            vesselInfo.LastUpdate = status.LastUpdate;
            vesselInfo.NumCrew = status.NumCrew;
            vesselInfo.CrewCap = status.CrewCap;
            vesselInfo.HabSpace = status.HabSpace;
            vesselInfo.HabMultiplier = status.HabMultiplier;
            vesselInfo.SuppliesLeft = status.SuppliesLeft;
            LifeSupportScenario.Instance.settings.SaveVesselNode(vesselInfo);
        }

        public void UntrackVessel(string vesselId)
        {
            if (!IsVesselTracked(vesselId))
                return;
            var vInfo = VesselSupplyInfo.First(v => v.VesselId == vesselId);
            VesselSupplyInfo.Remove(vInfo);
            //For saving to our scenario data
            LifeSupportScenario.Instance.settings.DeleteVesselNode(vInfo);
        }
        public VesselSupplyStatus FetchVessel(string vesselId)
        {
            if (!IsVesselTracked(vesselId))
            {
                var v = new VesselSupplyStatus();
                v.LastFeeding = Planetarium.GetUniversalTime();
                v.LastUpdate = Planetarium.GetUniversalTime();
                v.NumCrew = 0;
                v.CrewCap = 0;
                v.HabMultiplier = 0;
                v.HabSpace = 0;
                v.SuppliesLeft = 0f;
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
            return (LifeSupportSetup.Instance.LSConfig.VetNames.Contains(firstname));
        }


        internal void UpdateVesselStats()
        {
            //Clear stuff that is gone.
            var badIDs = new List<string>();
            foreach (var vInfo in LifeSupportManager.Instance.VesselSupplyInfo)
            {
                var vsl = FlightGlobals.Vessels.FirstOrDefault(v => v.id.ToString() == vInfo.VesselId);
                if(vsl == null || vInfo.NumCrew == 0)
                {
                    badIDs.Add(vInfo.VesselId);
                }
            }

            foreach (var id in badIDs)
            {
                LifeSupportManager.Instance.UntrackVessel(id);
            }
        }

        internal static double GetRecyclerMultiplier(Vessel vessel)
        {
            //TODO
            return 1d;
        }

        internal static double GetHabtime(VesselSupplyStatus thisVessel)
        {
            //Crew ratio is important - all of a vessel's hab capabilities are increased if a full crew is not present.
            var crewRatio = thisVessel.CrewCap / Math.Max(1,thisVessel.NumCrew);  //This will be 1 or greater
            //Hab time is a combination of four things
            //First - crew capacity. 
            var habTime = LifeSupportSetup.Instance.LSConfig.BaseHabTime + thisVessel.HabSpace; 
            //Now we can do our calculation. 
            var habTotal = habTime * thisVessel.HabMultiplier * crewRatio * LifeSupportSetup.Instance.LSConfig.HabMultiplier;
            //A Kerbal month is 30 six-hour Kerbin days.
            return habTotal*(60d*60d*6d*30d);
        }
    }
}

