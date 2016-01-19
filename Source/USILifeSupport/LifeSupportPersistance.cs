using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace LifeSupport
{

    using System.Linq;
    using UnityEngine;

    public class LifeSupportPersistance : MonoBehaviour
    {
        public ConfigNode SettingsNode { get; private set; }

        public void Load(ConfigNode node)
        {
            if (node.HasNode("LIFE_SUPPORT_SETTINGS"))
            {
                SettingsNode = node.GetNode("LIFE_SUPPORT_SETTINGS");
                _StatusInfo = SetupStatusInfo();
                _VesselInfo = SetupVesselInfo();
                //Reset cache
                LifeSupportManager.Instance.ResetCache();
            }
            else
            {
                _StatusInfo = new List<LifeSupportStatus>();
                _VesselInfo = new List<VesselSupplyStatus>();
            }
        }

        private List<LifeSupportStatus> SetupStatusInfo()
        {
            print("Loading Status Nodes");
            ConfigNode[] statNodes = SettingsNode.GetNodes("STATUS_DATA");
            print("StatNodeCount:  " + statNodes.Count());
            return ImportStatusNodeList(statNodes);
        }

        private List<VesselSupplyStatus> SetupVesselInfo()
        {
            print("Loading Vessel Nodes");
            ConfigNode[] vesselNodes = SettingsNode.GetNodes("VESSEL_DATA");
            print("StatNodeCount:  " + vesselNodes.Count());
            return ImportVesselNodeList(vesselNodes);
        }
        public List<LifeSupportStatus> GetStatusInfo()
        {
            return _StatusInfo ?? (_StatusInfo = SetupStatusInfo());
        }

        public List<VesselSupplyStatus> GetVesselInfo()
        {
            return _VesselInfo ?? (_VesselInfo = SetupVesselInfo());

        }

        private List<LifeSupportStatus> _StatusInfo;
        private List<VesselSupplyStatus> _VesselInfo;

        public void Save(ConfigNode node)
        {
            if (node.HasNode("LIFE_SUPPORT_SETTINGS"))
            {
                SettingsNode = node.GetNode("LIFE_SUPPORT_SETTINGS");
            }
            else
            {
                SettingsNode = node.AddNode("LIFE_SUPPORT_SETTINGS");
            }

            foreach (LifeSupportStatus r in _StatusInfo)
            {
                var rNode = new ConfigNode("STATUS_DATA");
                rNode.AddValue("KerbalName", r.KerbalName);
                rNode.AddValue("LastMeal", r.LastMeal);
                rNode.AddValue("LastOnKerbin", r.LastOnKerbin);
                rNode.AddValue("MaxOffKerbinTime", r.MaxOffKerbinTime);
                rNode.AddValue("LastVesselId", r.LastVesselId);
                rNode.AddValue("TimeInVessel", r.TimeInVessel);
                rNode.AddValue("IsGrouchy", r.IsGrouchy);
                rNode.AddValue("OldTrait", r.OldTrait);
                rNode.AddValue("LastUpdate", r.LastUpdate);
                SettingsNode.AddNode(rNode);
            }

            foreach (VesselSupplyStatus r in _VesselInfo)
            {
                var rNode = new ConfigNode("VESSEL_DATA");
                rNode.AddValue("VesselId", r.VesselId);
                rNode.AddValue("VesselName", r.VesselName);
                rNode.AddValue("SuppliesLeft", r.SuppliesLeft);
                rNode.AddValue("NumCrew", r.NumCrew);
                rNode.AddValue("RecyclerMultiplier", r.RecyclerMultiplier);
                rNode.AddValue("CrewCap", r.CrewCap);
                rNode.AddValue("HabSpace", r.HabSpace);
                rNode.AddValue("HabMultiplier", r.HabMultiplier);
                rNode.AddValue("LastFeeding", r.LastFeeding);
                SettingsNode.AddNode(rNode);
            }

            //Reset cache
            LifeSupportManager.Instance.ResetCache();
        }

        public static int GetValue(ConfigNode config, string name, int currentValue)
        {
            int newValue;
            if (config.HasValue(name) && int.TryParse(config.GetValue(name), out newValue))
            {
                return newValue;
            }
            return currentValue;
        }

        public static bool GetValue(ConfigNode config, string name, bool currentValue)
        {
            bool newValue;
            if (config.HasValue(name) && bool.TryParse(config.GetValue(name), out newValue))
            {
                return newValue;
            }
            return currentValue;
        }

        public static float GetValue(ConfigNode config, string name, float currentValue)
        {
            float newValue;
            if (config.HasValue(name) && float.TryParse(config.GetValue(name), out newValue))
            {
                return newValue;
            }
            return currentValue;
        }

        public void AddStatusNode(LifeSupportStatus kerbal)
        {
            if (_StatusInfo.Any(n => n.KerbalName == kerbal.KerbalName))
                return;
            _StatusInfo.Add(kerbal);
        }

        public void AddVesselNode(VesselSupplyStatus vInfo)
        {
            if (_VesselInfo.Any(n => n.VesselId == vInfo.VesselId))
                return;
            _VesselInfo.Add(vInfo);
        }

        public void DeleteStatusNode(LifeSupportStatus kerbal)
        {
            if (_StatusInfo.All(n => n.KerbalName != kerbal.KerbalName))
                return;
            var k = _StatusInfo.First(n => n.KerbalName == kerbal.KerbalName);
            _StatusInfo.Remove(k);
        }

        public void DeleteVesselNode(VesselSupplyStatus vInfo)
        {
            if (_VesselInfo.All(n => n.VesselId != vInfo.VesselId))
                return;
            var v = _VesselInfo.First(n => n.VesselId == vInfo.VesselId);
            _VesselInfo.Remove(v);
        }
        public static List<LifeSupportStatus> ImportStatusNodeList(ConfigNode[] nodes)
        {
            var nList = new List<LifeSupportStatus>();
            foreach (ConfigNode node in nodes)
            {
                var res = ResourceUtilities.LoadNodeProperties<LifeSupportStatus>(node);
                nList.Add(res);
            }
            return nList;
        }
        public static List<VesselSupplyStatus> ImportVesselNodeList(ConfigNode[] nodes)
        {
            var nList = new List<VesselSupplyStatus>();
            foreach (ConfigNode node in nodes)
            {
                var res = ResourceUtilities.LoadNodeProperties<VesselSupplyStatus>(node);
                nList.Add(res);
            }
            return nList;
        }

        public void SaveStatusNode(LifeSupportStatus status)
        {
            LifeSupportStatus kerbInfo =
                _StatusInfo.FirstOrDefault(n => n.KerbalName == status.KerbalName);
            if (kerbInfo == null)
            {
                kerbInfo = new LifeSupportStatus();
                kerbInfo.KerbalName = status.KerbalName;
                _StatusInfo.Add(kerbInfo);
            }
            kerbInfo.LastMeal = status.LastMeal;
            kerbInfo.LastOnKerbin = status.LastOnKerbin;
            kerbInfo.MaxOffKerbinTime = status.MaxOffKerbinTime;
            kerbInfo.TimeInVessel = status.TimeInVessel;
            kerbInfo.LastVesselId = status.LastVesselId;
            kerbInfo.IsGrouchy = status.IsGrouchy;
            kerbInfo.OldTrait = status.OldTrait;
            kerbInfo.LastUpdate = status.LastUpdate;
        }

        public void SaveVesselNode(VesselSupplyStatus status)
        {
            VesselSupplyStatus vesselInfo =
                _VesselInfo.FirstOrDefault(n => n.VesselId == status.VesselId);
            if (vesselInfo == null)
            {
                vesselInfo = new VesselSupplyStatus();
                vesselInfo.VesselId = status.VesselId;
                _VesselInfo.Add(vesselInfo);
            }
            vesselInfo.VesselName = status.VesselName;
            vesselInfo.LastFeeding = status.LastFeeding;
            vesselInfo.LastUpdate = status.LastUpdate;
            vesselInfo.NumCrew = status.NumCrew;
            vesselInfo.RecyclerMultiplier = status.RecyclerMultiplier;
            vesselInfo.CrewCap = status.CrewCap;
            vesselInfo.HabSpace = status.HabSpace;
            vesselInfo.HabSpace = status.HabSpace;
            vesselInfo.HabMultiplier = status.HabMultiplier;
            vesselInfo.SuppliesLeft = status.SuppliesLeft;
        }
    
    }
}
