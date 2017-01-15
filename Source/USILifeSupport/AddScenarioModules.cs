using UnityEngine;

namespace LifeSupport
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class AddScenarioModules : MonoBehaviour
    {
        void Start()
        {
            var game = HighLogic.CurrentGame;

            var psm = game.scenarios.Find(s => s.moduleName == typeof(LifeSupportScenario).Name);
            if (psm == null)
            {
                game.AddProtoScenarioModule(typeof(LifeSupportScenario), GameScenes.SPACECENTER,
                    GameScenes.FLIGHT, GameScenes.EDITOR);
            }
            else
            {
                var addSpace = true;
                var addFlight = true;
                var addEditor = true;
                var count = psm.targetScenes.Count;
                for (int i = 0; i < count; ++i)
                {
                    var s = psm.targetScenes[i];
                    if (s == GameScenes.FLIGHT)
                        addFlight = false;
                    if (s == GameScenes.SPACECENTER)
                        addSpace = false;
                    if (s == GameScenes.EDITOR)
                        addEditor = false;
                }

                if (addSpace)
                {
                    psm.targetScenes.Add(GameScenes.SPACECENTER);
                }
                if (addFlight)
                {
                    psm.targetScenes.Add(GameScenes.FLIGHT);
                }
                if (addEditor)
                {
                    psm.targetScenes.Add(GameScenes.EDITOR);
                }
            }
        }
    }
}