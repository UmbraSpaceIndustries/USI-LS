using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace LifeSupport
{
    using System;
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
                _Settings = SetupSettings();
                //Reset cache
                LifeSupportManager.Instance.ResetCache();
            }
            else
            {
                _StatusInfo = new List<LifeSupportStatus>();
                _VesselInfo = new List<VesselSupplyStatus>();
                _Settings = null;
            }
        }

        private LifeSupportConfig SetupSettings()
        {
            print("Loading Config");
            ConfigNode[] statNodes = SettingsNode.GetNodes("LIFE_SUPPORT_CONFIG");
            print("StatNodeCount:  " + statNodes.Count());
            if (statNodes.Count() > 0)
                return ImportConfig(statNodes[0]);
            else
                return LoadLifeSupportConfig();
        }

        private LifeSupportConfig LoadLifeSupportConfig()
        {
            var lsNodes = GameDatabase.Instance.GetConfigNodes("LIFE_SUPPORT_SETTINGS");
            var finalSettings = new LifeSupportConfig
            {
                HabMultiplier = int.MaxValue,
                BaseHabTime = double.MaxValue,
                ECAmount = 0f,
                EVAEffect = 0,
                EVAEffectVets = 0,
                EVATime = float.MaxValue,
                HomeWorldAltitude = int.MaxValue,
                NoHomeEffect = 0,
                NoHomeEffectVets = 0,
                NoSupplyEffect = 0,
                NoSupplyEffectVets = 0,
                SupplyTime = float.MaxValue,
                SupplyAmount = 0f,
                WasteAmount = 0f,
                ReplacementPartAmount = 0f,
                EnableRecyclers = false,
                HabRange = 2000,
                VetNames = ""
            };
            foreach (var lsNode in lsNodes)
            {
                var settings = ResourceUtilities.LoadNodeProperties<LifeSupportConfig>(lsNode);
                finalSettings.HabMultiplier = Math.Min(settings.HabMultiplier, finalSettings.HabMultiplier);
                finalSettings.BaseHabTime = Math.Min(settings.BaseHabTime, finalSettings.BaseHabTime);
                finalSettings.ECAmount = Math.Max(settings.ECAmount, finalSettings.ECAmount);
                finalSettings.HomeWorldAltitude = Math.Min(settings.HomeWorldAltitude, finalSettings.HomeWorldAltitude);
                finalSettings.NoHomeEffect = Math.Max(settings.NoHomeEffect, finalSettings.NoHomeEffect);
                finalSettings.NoHomeEffectVets = Math.Max(settings.NoHomeEffectVets, finalSettings.NoHomeEffectVets);
                finalSettings.NoSupplyEffect = Math.Max(settings.NoSupplyEffect, finalSettings.NoSupplyEffect);
                finalSettings.NoSupplyEffectVets = Math.Max(settings.NoSupplyEffectVets, finalSettings.NoSupplyEffectVets);
                finalSettings.SupplyTime = Math.Min(settings.SupplyTime, finalSettings.SupplyTime);
                finalSettings.EVATime = Math.Min(settings.EVATime, finalSettings.EVATime);
                finalSettings.SupplyAmount = Math.Max(settings.SupplyAmount, finalSettings.SupplyAmount);
                finalSettings.WasteAmount = Math.Max(settings.WasteAmount, finalSettings.WasteAmount);
                finalSettings.ReplacementPartAmount = Math.Max(settings.ReplacementPartAmount, finalSettings.ReplacementPartAmount);
                finalSettings.EVAEffect = Math.Max(settings.EVAEffect, finalSettings.EVAEffect);
                finalSettings.EVAEffectVets = Math.Max(settings.EVAEffectVets, finalSettings.EVAEffectVets);
                finalSettings.VetNames += settings.VetNames + ",";
                finalSettings.HabRange = Math.Min(settings.HabRange, finalSettings.HabRange);
                if (settings.EnableRecyclers)
                    finalSettings.EnableRecyclers = true;
            }
            return finalSettings;
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

        public LifeSupportConfig GetSettings()
        {
            return _Settings ?? (_Settings = SetupSettings());

        }


        private List<LifeSupportStatus> _StatusInfo;
        private List<VesselSupplyStatus> _VesselInfo;
        private LifeSupportConfig _Settings;

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

            if (_Settings == null)
                _Settings = LoadLifeSupportConfig();

            foreach (LifeSupportStatus r in _StatusInfo)
            {
                var rNode = new ConfigNode("STATUS_DATA");
                rNode.AddValue("KerbalName", r.KerbalName);
                rNode.AddValue("LastMeal", r.LastMeal);
                rNode.AddValue("LastOnKerbin", r.LastOnKerbin);
                rNode.AddValue("MaxOffKerbinTime", r.MaxOffKerbinTime);
                rNode.AddValue("CurrentVesselId", r.CurrentVesselId);
                rNode.AddValue("PreviousVesselId", r.PreviousVesselId);
                rNode.AddValue("TimeEnteredVessel", r.TimeEnteredVessel);
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
                rNode.AddValue("ExtraHabSpace", r.ExtraHabSpace);
                rNode.AddValue("VesselHabMultiplier", r.VesselHabMultiplier);
                rNode.AddValue("LastFeeding", r.LastFeeding);
                SettingsNode.AddNode(rNode);
            }

            var sNode = new ConfigNode("LIFE_SUPPORT_CONFIG");
            sNode.AddValue("HabMultiplier",_Settings.HabMultiplier);
            sNode.AddValue("BaseHabTime", _Settings.BaseHabTime);
            sNode.AddValue("ECAmount", _Settings.ECAmount);
            sNode.AddValue("EVAEffect", _Settings.EVAEffect);
            sNode.AddValue("EVAEffectVets", _Settings.EVAEffectVets);
            sNode.AddValue("EVATime", _Settings.EVATime);
            sNode.AddValue("HomeWorldAltitude", _Settings.HomeWorldAltitude);
            sNode.AddValue("NoHomeEffect", _Settings.NoHomeEffect);
            sNode.AddValue("NoHomeEffectVets", _Settings.NoHomeEffectVets);
            sNode.AddValue("NoSupplyEffect", _Settings.NoSupplyEffect);
            sNode.AddValue("NoSupplyEffectVets", _Settings.NoSupplyEffectVets);
            sNode.AddValue("SupplyTime", _Settings.SupplyTime);
            sNode.AddValue("SupplyAmount", _Settings.SupplyAmount);
            sNode.AddValue("WasteAmount", _Settings.WasteAmount);
            sNode.AddValue("ReplacementPartAmount", _Settings.ReplacementPartAmount);
            sNode.AddValue("EnableRecyclers", _Settings.EnableRecyclers);
            sNode.AddValue("HabRange", _Settings.HabRange);
            sNode.AddValue("VetNames", _Settings.VetNames);

            SettingsNode.AddNode(sNode);

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

        public void DeleteStatusNode(string kName)
        {
            if (_StatusInfo.All(n => n.KerbalName != kName))
                return;
            var k = _StatusInfo.First(n => n.KerbalName == kName);
            _StatusInfo.Remove(k);
        }

        public void DeleteVesselNode(string vId)
        {
            if (_VesselInfo.All(n => n.VesselId != vId))
                return;
            var v = _VesselInfo.First(n => n.VesselId == vId);
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

        public LifeSupportConfig ImportConfig(ConfigNode node)
        {
            var config = ResourceUtilities.LoadNodeProperties<LifeSupportConfig>(node);
            return config;
        }

        public void SaveConfig(LifeSupportConfig config)
        {
            _Settings.HabMultiplier = config.HabMultiplier;
            _Settings.BaseHabTime = config.BaseHabTime;
            _Settings.ECAmount = config.ECAmount;
            _Settings.EVAEffect = config.EVAEffect;
            _Settings.EVAEffectVets = config.EVAEffectVets;
            _Settings.EVATime = config.EVATime;
            _Settings.HomeWorldAltitude = config.HomeWorldAltitude;
            _Settings.NoHomeEffect = config.NoHomeEffect;
            _Settings.NoHomeEffectVets = config.NoHomeEffectVets;
            _Settings.NoSupplyEffect = config.NoSupplyEffect;
            _Settings.NoSupplyEffectVets = config.NoSupplyEffectVets;
            _Settings.SupplyTime = config.SupplyTime;
            _Settings.SupplyAmount = config.SupplyAmount;
            _Settings.WasteAmount = config.WasteAmount;
            _Settings.ReplacementPartAmount = config.ReplacementPartAmount;
            _Settings.EnableRecyclers = config.EnableRecyclers;
            _Settings.HabRange = config.HabRange;
            _Settings.VetNames = config.VetNames;
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
            kerbInfo.TimeEnteredVessel = status.TimeEnteredVessel;
            kerbInfo.CurrentVesselId = status.CurrentVesselId;
            kerbInfo.PreviousVesselId = status.PreviousVesselId;
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
            vesselInfo.ExtraHabSpace = status.ExtraHabSpace;
            vesselInfo.VesselHabMultiplier = status.VesselHabMultiplier;
            vesselInfo.SuppliesLeft = status.SuppliesLeft;
        }
    
    }
}
