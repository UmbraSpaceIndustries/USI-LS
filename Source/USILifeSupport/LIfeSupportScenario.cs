using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LifeSupport
{
    public class LifeSupportScenario : ScenarioModule
    {
        public LifeSupportScenario()
        {
            Instance = this;
            settings = new LifeSupportPersistance();
        }

        public static LifeSupportScenario Instance { get; private set; }
        public LifeSupportPersistance settings { get; private set; }

        public override void OnLoad(ConfigNode gameNode)
        {
            base.OnLoad(gameNode);
            settings.Load(gameNode);
        }

        public override void OnSave(ConfigNode gameNode)
        {
            base.OnSave(gameNode);
            settings.Save(gameNode);
        }
    }
}
