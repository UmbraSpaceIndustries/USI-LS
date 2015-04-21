using System.Linq;
using UnityEngine;

public class LifeSupportSetup : MonoBehaviour
{
    public class LifeSupportConfig
    {
        public float SupplyTime { get; set; }
        public float ECAmount { get; set; }
        public bool CausesDeath { get; set; }
    }

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
        var mapNode = GameDatabase.Instance.GetConfigNodes("LIFE_SUPPORT_SETTINGS").FirstOrDefault();
        var settings = ResourceUtilities.LoadNodeProperties<LifeSupportConfig>(mapNode);
        return settings;
    }
}