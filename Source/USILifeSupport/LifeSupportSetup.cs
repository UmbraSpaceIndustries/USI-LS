using System;
using System.Linq;
using UnityEngine;

namespace LifeSupport
{
    public class LifeSupportSetup : MonoBehaviour
    {
        // Static singleton instance
        private static LifeSupportSetup instance;

        // Static singleton property
        public static LifeSupportSetup Instance
        {
            get { return instance ?? (instance = new GameObject("LifeSupportSetup").AddComponent<LifeSupportSetup>()); }
        }

        //Static data holding variables
        private static LifeSupportConfig _lsConfig;

        public LifeSupportConfig LSConfig
        {
            get { return _lsConfig ?? (_lsConfig = LoadLifeSupportConfig()); }
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
                SupplyTime= float.MaxValue,
                SupplyAmount = 0f,
                WasteAmount = 0f,
                ReplacementPartAmount = 0f,
                EnableRecyclers = false,
                HabRange = 2000
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
    }
}