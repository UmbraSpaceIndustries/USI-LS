using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LifeSupport
{
    public class LifeSupportResourceSetup : MonoBehaviour
    {
        public class LifeSupportResourceConfig
        {
            public string ResourceName { get; set; }
            public float Ratio { get; set; }
        }

        // Static singleton instance
        private static LifeSupportResourceSetup instance;

        // Static singleton property
        public static LifeSupportResourceSetup Instance
        {
            get { return instance ?? (instance = new GameObject("LifeSupportResourceSetup").AddComponent<LifeSupportResourceSetup>()); }
        }

        //Static data holding variables
        private static List<LifeSupportResourceConfig> _inRes;
        private static List<LifeSupportResourceConfig> _outRes;

        public List<LifeSupportResourceConfig> GetInputResources
        {
            get { return _inRes ?? (_inRes = LoadResourceConfig("LIFE_SUPPORT_INPUT")); }
        }

        public List<LifeSupportResourceConfig> GetOutputResources
        {
            get { return _outRes ?? (_outRes = LoadResourceConfig("LIFE_SUPPORT_OUTPUT")); }
        }


        private List<LifeSupportResourceConfig> LoadResourceConfig(string rootNode)
        {
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes(rootNode);
            return nodes
                .Select(ResourceUtilities.LoadNodeProperties<LifeSupportResourceConfig>)
                .ToList();
        }
    }
}