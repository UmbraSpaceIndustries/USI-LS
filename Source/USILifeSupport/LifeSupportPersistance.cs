using System.Collections.Generic;
using System;
using UnityEngine;

namespace LifeSupport
{


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

        public bool isLoaded()
        {
            return SettingsNode != null;
        }

        private LifeSupportConfig SetupSettings()
        {
            print("Loading Config");
            ConfigNode[] statNodes = SettingsNode.GetNodes("LIFE_SUPPORT_CONFIG");
            print("StatNodeCount:  " + statNodes.Length);

            LifeSupportConfig tmpSettings = null;
            var defSettings = LoadLifeSupportConfig();
            if (statNodes.Length > 0)
            {
                tmpSettings = ImportConfig(statNodes[0]);
                //Guard clauses
                if (tmpSettings.ECTime < ResourceUtilities.FLOAT_TOLERANCE)
                    tmpSettings.ECTime = defSettings.ECTime;
                if (tmpSettings.SupplyAmount < ResourceUtilities.FLOAT_TOLERANCE)
                    tmpSettings.SupplyAmount = defSettings.SupplyAmount;
                if (tmpSettings.ScoutHabTime < ResourceUtilities.FLOAT_TOLERANCE)
                    tmpSettings.ScoutHabTime = defSettings.ScoutHabTime;
                if (tmpSettings.PermaHabTime < ResourceUtilities.FLOAT_TOLERANCE)
                    tmpSettings.PermaHabTime = defSettings.PermaHabTime;
            }
            else
                tmpSettings = LoadLifeSupportConfig();

            return tmpSettings;
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
                ECTime = float.MaxValue,
                NoECEffectVets = 0,
                NoECEffect = 0,
                SupplyAmount = 0f,
                WasteAmount = 0f,
                ReplacementPartAmount = 0f,
                EnableRecyclers = false,
                ScoutHabTime = 9180000,
                PermaHabTime = 459000000,
                HabRange = 2000,
                RecoverySpeed = 1f,
                VetNames = ""
            };

            var count = lsNodes.Length;
            for (int i = 0; i < count; ++i)
            {
                var lsNode = lsNodes[i];
                var settings = ResourceUtilities.LoadNodeProperties<LifeSupportConfig>(lsNode);
                finalSettings.HabMultiplier = Math.Min(settings.HabMultiplier, finalSettings.HabMultiplier);
                finalSettings.BaseHabTime = Math.Min(settings.BaseHabTime, finalSettings.BaseHabTime);
                finalSettings.HomeWorldAltitude = Math.Min(settings.HomeWorldAltitude, finalSettings.HomeWorldAltitude);
                finalSettings.NoHomeEffect = Math.Max(settings.NoHomeEffect, finalSettings.NoHomeEffect);
                finalSettings.NoHomeEffectVets = Math.Max(settings.NoHomeEffectVets, finalSettings.NoHomeEffectVets);
                finalSettings.NoSupplyEffect = Math.Max(settings.NoSupplyEffect, finalSettings.NoSupplyEffect);
                finalSettings.NoSupplyEffectVets = Math.Max(settings.NoSupplyEffectVets, finalSettings.NoSupplyEffectVets);

                if(settings.ECTime > ResourceUtilities.FLOAT_TOLERANCE)
                    finalSettings.ECTime = Math.Min(settings.ECTime, finalSettings.ECTime);
                if (settings.EVATime > ResourceUtilities.FLOAT_TOLERANCE)
                    finalSettings.EVATime = Math.Min(settings.EVATime, finalSettings.EVATime);
                if (settings.SupplyTime > ResourceUtilities.FLOAT_TOLERANCE)
                    finalSettings.SupplyTime = Math.Min(settings.SupplyTime, finalSettings.SupplyTime);
                if (settings.ScoutHabTime > 1000d)
                    finalSettings.ScoutHabTime = Math.Min(settings.ScoutHabTime, finalSettings.ScoutHabTime);
                if (settings.PermaHabTime > 1000d)
                    finalSettings.PermaHabTime = Math.Min(settings.PermaHabTime, finalSettings.PermaHabTime);

                finalSettings.SupplyAmount = Math.Max(settings.SupplyAmount, finalSettings.SupplyAmount);
                finalSettings.ECAmount = Math.Max(settings.ECAmount, finalSettings.ECAmount);
                finalSettings.NoECEffect = Math.Max(settings.NoECEffect, finalSettings.NoECEffect);
                finalSettings.NoECEffectVets = Math.Max(settings.NoECEffectVets, finalSettings.NoECEffectVets);
                finalSettings.WasteAmount = Math.Max(settings.WasteAmount, finalSettings.WasteAmount);
                finalSettings.ReplacementPartAmount = Math.Max(settings.ReplacementPartAmount, finalSettings.ReplacementPartAmount);
                finalSettings.EVAEffect = Math.Max(settings.EVAEffect, finalSettings.EVAEffect);
                finalSettings.EVAEffectVets = Math.Max(settings.EVAEffectVets, finalSettings.EVAEffectVets);
                finalSettings.VetNames += settings.VetNames + ",";
                finalSettings.HabRange = Math.Min(settings.HabRange, finalSettings.HabRange);
                finalSettings.RecoverySpeed = Math.Min(settings.RecoverySpeed, finalSettings.RecoverySpeed);
                if (settings.EnableRecyclers)
                    finalSettings.EnableRecyclers = true;
            }
            return finalSettings;
        }

        private List<LifeSupportStatus> SetupStatusInfo()
        {
            print("Loading Status Nodes");
            ConfigNode[] statNodes = SettingsNode.GetNodes("STATUS_DATA");
            print("StatNodeCount:  " + statNodes.Length);
            return ImportStatusNodeList(statNodes);
        }

        private List<VesselSupplyStatus> SetupVesselInfo()
        {
            print("Loading Vessel Nodes");
            ConfigNode[] vesselNodes = SettingsNode.GetNodes("VESSEL_DATA");
            print("StatNodeCount:  " + vesselNodes.Length);
            return ImportVesselNodeList(vesselNodes);
        }
        public List<LifeSupportStatus> GetStatusInfo()
        {
            return _StatusInfo ?? (_StatusInfo = SetupStatusInfo());
        }

        public List<VesselSupplyStatus> GetVesselInfo()
        {
            if (_VesselInfo == null)
                _VesselInfo = SetupVesselInfo();
            return _VesselInfo;
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

            if (_StatusInfo != null)
            {
                var count = _StatusInfo.Count;
                for(int i = 0; i < count; ++i)
                {
                    var r = _StatusInfo[i];
                    var rNode = new ConfigNode("STATUS_DATA");
                    rNode.AddValue("KerbalName", r.KerbalName);
                    rNode.AddValue("HomeBodyId", r.HomeBodyId);
                    rNode.AddValue("LastMeal", r.LastMeal);
                    rNode.AddValue("LastEC", r.LastEC);
                    rNode.AddValue("LastAtHome", r.LastAtHome);
                    rNode.AddValue("LastSOIChange", r.LastSOIChange);
                    rNode.AddValue("LastPlanet", r.LastPlanet);
                    rNode.AddValue("CurrentVesselId", r.CurrentVesselId);
                    rNode.AddValue("PreviousVesselId", r.PreviousVesselId);
                    rNode.AddValue("RemainingCabinTime", r.RemainingCabinTime);
                    rNode.AddValue("IsGrouchy", r.IsGrouchy);
                    rNode.AddValue("OldTrait", r.OldTrait);
                    rNode.AddValue("LastUpdate", r.LastUpdate);
                    SettingsNode.AddNode(rNode);
                }
            }

            if (_VesselInfo != null)
            {
                var count = _VesselInfo.Count;
                for(int i = 0; i < count; ++i)
                {
                    var r = _VesselInfo[i];
                    if (string.IsNullOrEmpty(r.VesselName))
                        continue;
                    var rNode = new ConfigNode("VESSEL_DATA");
                    rNode.AddValue("VesselId", r.VesselId);
                    rNode.AddValue("VesselName", r.VesselName);
                    rNode.AddValue("SuppliesLeft", r.SuppliesLeft);
                    rNode.AddValue("ECLeft", r.ECLeft);
                    rNode.AddValue("NumCrew", r.NumCrew);
                    rNode.AddValue("RecyclerMultiplier", r.RecyclerMultiplier);
                    rNode.AddValue("CrewCap", r.CrewCap);
                    rNode.AddValue("ExtraHabSpace", r.ExtraHabSpace);
                    rNode.AddValue("VesselHabMultiplier", r.VesselHabMultiplier);
                    rNode.AddValue("CachedHabTime", r.CachedHabTime);
                    rNode.AddValue("LastFeeding", r.LastFeeding);
                    rNode.AddValue("LastECCheck", r.LastECCheck);
                    SettingsNode.AddNode(rNode);
                }
            }

            if (_Settings != null)
            {
                var sNode = new ConfigNode("LIFE_SUPPORT_CONFIG");
                sNode.AddValue("HabMultiplier", _Settings.HabMultiplier);
                sNode.AddValue("BaseHabTime", _Settings.BaseHabTime);
                sNode.AddValue("ECAmount", _Settings.ECAmount);
                sNode.AddValue("ScoutHabTime", _Settings.ScoutHabTime);
                sNode.AddValue("PermaHabTime", _Settings.PermaHabTime);
                sNode.AddValue("SupplyAmount", _Settings.SupplyAmount);
                sNode.AddValue("EVAEffect", _Settings.EVAEffect);
                sNode.AddValue("EVAEffectVets", _Settings.EVAEffectVets);
                sNode.AddValue("EVATime", _Settings.EVATime);
                sNode.AddValue("HomeWorldAltitude", _Settings.HomeWorldAltitude);
                sNode.AddValue("NoHomeEffect", _Settings.NoHomeEffect);
                sNode.AddValue("NoHomeEffectVets", _Settings.NoHomeEffectVets);
                sNode.AddValue("NoSupplyEffect", _Settings.NoSupplyEffect);
                sNode.AddValue("NoSupplyEffectVets", _Settings.NoSupplyEffectVets);
                sNode.AddValue("SupplyTime", _Settings.SupplyTime);
                sNode.AddValue("ECTime", _Settings.ECTime);
                sNode.AddValue("NoECEffectVets", _Settings.NoECEffectVets);
                sNode.AddValue("NoECEffect", _Settings.NoECEffect);
                sNode.AddValue("WasteAmount", _Settings.WasteAmount);
                sNode.AddValue("ReplacementPartAmount", _Settings.ReplacementPartAmount);
                sNode.AddValue("EnableRecyclers", _Settings.EnableRecyclers);
                sNode.AddValue("HabRange", _Settings.HabRange);
                sNode.AddValue("RecoverySpeed", _Settings.RecoverySpeed);
                sNode.AddValue("VetNames", _Settings.VetNames);
                SettingsNode.AddNode(sNode);
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
            var count = _StatusInfo.Count;
            for (int i = 0; i < count; ++i)
            {
                if (_StatusInfo[i].KerbalName == kerbal.KerbalName)
                    return;
            }
            _StatusInfo.Add(kerbal);
        }

        public void AddVesselNode(VesselSupplyStatus vInfo)
        {
            var count = _VesselInfo.Count;
            for (int i = 0; i < count; ++i)
            {
                if (_VesselInfo[i].VesselId == vInfo.VesselId)
                    return;
            }
            _VesselInfo.Add(vInfo);
        }

        public void DeleteStatusNode(string kName)
        {
            var count = _StatusInfo.Count;
            for (int i = 0; i < count; ++i)
            {
                var k = _StatusInfo[i];
                if (k.KerbalName == kName)
                {
                    _StatusInfo.Remove(k);
                    return;
                }
            }
        }

        public void DeleteVesselNode(string vId)
        {
            var count = _VesselInfo.Count;
            for (int i = 0; i < count; ++i)
            {
                var v = _VesselInfo[i];
                if (v.VesselId == vId)
                {
                    _VesselInfo.Remove(v);
                    return;
                }
            }
        }

        public static List<LifeSupportStatus> ImportStatusNodeList(ConfigNode[] nodes)
        {
            var nList = new List<LifeSupportStatus>();
            var count = nodes.Length;
            for(int i = 0; i < count; ++i)
            {
                var node = nodes[i];
                var res = ResourceUtilities.LoadNodeProperties<LifeSupportStatus>(node);
                nList.Add(res);
            }
            return nList;
        }
        public static List<VesselSupplyStatus> ImportVesselNodeList(ConfigNode[] nodes)
        {
            var nList = new List<VesselSupplyStatus>();
            var count = nodes.Length;
            for (int i = 0; i < count; ++i)
            {
                var node = nodes[i];
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
            _Settings.PermaHabTime = config.PermaHabTime;
            _Settings.ScoutHabTime = config.ScoutHabTime;
            _Settings.EVAEffect = config.EVAEffect;
            _Settings.EVAEffectVets = config.EVAEffectVets;
            _Settings.EVATime = config.EVATime;
            _Settings.HomeWorldAltitude = config.HomeWorldAltitude;
            _Settings.NoHomeEffect = config.NoHomeEffect;
            _Settings.NoHomeEffectVets = config.NoHomeEffectVets;
            _Settings.NoSupplyEffect = config.NoSupplyEffect;
            _Settings.NoSupplyEffectVets = config.NoSupplyEffectVets;
            _Settings.SupplyTime = config.SupplyTime;
            _Settings.ECTime = config.ECTime;
            _Settings.NoECEffectVets = config.NoECEffectVets;
            _Settings.NoECEffect = config.NoECEffect;
            _Settings.SupplyAmount = config.SupplyAmount;
            _Settings.WasteAmount = config.WasteAmount;
            _Settings.ReplacementPartAmount = config.ReplacementPartAmount;
            _Settings.EnableRecyclers = config.EnableRecyclers;
            _Settings.HabRange = config.HabRange;
            _Settings.RecoverySpeed = config.RecoverySpeed;
            _Settings.VetNames = config.VetNames;
        }

        public void SaveStatusNode(LifeSupportStatus status)
        {
            LifeSupportStatus kerbInfo = null;
            var count = _StatusInfo.Count;
            for (int i = 0; i < count; ++i)
            {
                var n = _StatusInfo[i];
                if (n.KerbalName == status.KerbalName)
                {
                    kerbInfo = n;
                    break;
                }
            }

            if (kerbInfo == null)
            {
                kerbInfo = new LifeSupportStatus();
                kerbInfo.KerbalName = status.KerbalName;
                _StatusInfo.Add(kerbInfo);
            }
            kerbInfo.LastMeal = status.LastMeal;
            kerbInfo.LastEC = status.LastEC;
            kerbInfo.LastAtHome = status.LastAtHome;
            kerbInfo.LastSOIChange = status.LastSOIChange;
            kerbInfo.HomeBodyId = status.HomeBodyId;
            kerbInfo.LastPlanet = status.LastPlanet;
            kerbInfo.CurrentVesselId = status.CurrentVesselId;
            kerbInfo.PreviousVesselId = status.PreviousVesselId;
            kerbInfo.RemainingCabinTime = status.RemainingCabinTime;
            kerbInfo.IsGrouchy = status.IsGrouchy;
            kerbInfo.OldTrait = status.OldTrait;
            kerbInfo.LastUpdate = status.LastUpdate;
        }

        public void SaveVesselNode(VesselSupplyStatus status)
        {
            if (status.VesselName == null)
                return;

            VesselSupplyStatus vesselInfo = null;
            var count = _VesselInfo.Count;
            for (int i = 0; i < count; ++i)
            {
                var n = _VesselInfo[i];
                if (n.VesselId == status.VesselId)
                {
                    vesselInfo = n;
                    break;
                }
            }

            if (vesselInfo == null)
            {
                vesselInfo = new VesselSupplyStatus();
                vesselInfo.VesselId = status.VesselId;
                _VesselInfo.Add(vesselInfo);
            }
            vesselInfo.VesselName = status.VesselName;
            vesselInfo.LastFeeding = status.LastFeeding;
            vesselInfo.LastECCheck = status.LastECCheck;
            vesselInfo.LastUpdate = status.LastUpdate;
            vesselInfo.NumCrew = status.NumCrew;
            vesselInfo.RecyclerMultiplier = status.RecyclerMultiplier;
            vesselInfo.CrewCap = status.CrewCap;
            vesselInfo.ExtraHabSpace = status.ExtraHabSpace;
            vesselInfo.VesselHabMultiplier = status.VesselHabMultiplier;
            vesselInfo.CachedHabTime = status.CachedHabTime;
            vesselInfo.SuppliesLeft = status.SuppliesLeft;
            vesselInfo.ECLeft = status.ECLeft;
        }

    }
}
