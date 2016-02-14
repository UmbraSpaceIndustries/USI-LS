using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LifeSupport
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class LifeSupportMonitor_Editor : MonoBehaviour
    {
        private ApplicationLauncherButton orbLogButton;
        private Rect _windowPosition = new Rect(300, 60, 620, 400);
        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _scrollStyle;
        private Vector2 scrollPos = Vector2.zero;
        private bool _hasInitStyles = false;

        void Awake()
        {
            var texture = new Texture2D(36, 36, TextureFormat.RGBA32, false);
            var textureFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Supplies.png");
            print("Loading " + textureFile);
            texture.LoadImage(File.ReadAllBytes(textureFile));
            this.orbLogButton = ApplicationLauncher.Instance.AddModApplication(GuiOn, GuiOff, null, null, null, null,
                ApplicationLauncher.AppScenes.ALWAYS, texture);
        }

        private void GuiOn()
        {
            RenderingManager.AddToPostDrawQueue(144, Ondraw);
        }

        public void Start()
        {
            if (!_hasInitStyles)
                InitStyles();
        }

        private void GuiOff()
        {
            RenderingManager.RemoveFromPostDrawQueue(144, Ondraw);
        }


        private void Ondraw()
        {
            _windowPosition = GUILayout.Window(10, _windowPosition, OnWindow, "Life Support Status", _windowStyle);
        }

        private void OnWindow(int windowId)
        {
            GenerateWindow();
        }

        private void GenerateWindow()
        {
            GUILayout.BeginVertical();
            scrollPos = GUILayout.BeginScrollView(scrollPos, _scrollStyle, GUILayout.Width(600), GUILayout.Height(350));
            GUILayout.BeginVertical();

            var useHabPenalties = (LifeSupportSetup.Instance.LSConfig.NoHomeEffectVets +
                                   LifeSupportSetup.Instance.LSConfig.NoHomeEffect > 0);
            if (EditorLogic.fetch != null)
            {
                var curCrew = 0;
                var maxCrew = 0;
                var supplies = 0d;
                var extraHabTime = 0d;
                var habMult = 1d;
                var batteryAmount = 0d;

                foreach (var part in EditorLogic.fetch.ship.parts)
                {
                    maxCrew += part.CrewCapacity;
                }

                CMAssignmentDialog dialog = CMAssignmentDialog.Instance;
                if (dialog != null)
                {
                    VesselCrewManifest manifest = dialog.GetManifest();
                    if (manifest != null)
                    {
                        foreach (PartCrewManifest pcm in manifest)
                        {
                            int partCrewCount = pcm.GetPartCrew().Count(c => c != null);
                            if (partCrewCount > 0)
                            {
                                curCrew += partCrewCount;
                            }
                        }
                    }
                }

                foreach (var part in EditorLogic.fetch.ship.parts)
                {
                    var hab = part.Modules.GetModules<ModuleHabitation>().FirstOrDefault();
                    if(hab != null)
                    {
                        //Certain modules, in addition to crew capacity, have living space.
                        extraHabTime += hab.KerbalMonths;
                        //Some modules act more as 'multipliers', dramatically extending a hab's workable lifespan.
                        habMult += hab.HabMultiplier*Math.Min(1,(hab.CrewCapacity/curCrew));
                    }

                    if (part.Resources.Contains("Supplies"))
                    {
                        supplies += part.Resources["Supplies"].amount;
                    }
                    if (part.Resources.Contains("ElectricCharge"))
                    {
                        batteryAmount += part.Resources["ElectricCharge"].maxAmount;
                    }
                }


                var totalHabSpace = (LifeSupportSetup.Instance.LSConfig.BaseHabTime * maxCrew) + extraHabTime;
                //A Kerbal month is 30 six-hour Kerbin days.
                var totalHabMult = habMult * LifeSupportSetup.Instance.LSConfig.HabMultiplier * 60d * 60d * 6d * 30d;

                var totalBatteryTime = batteryAmount / LifeSupportSetup.Instance.LSConfig.ECAmount;
                var totalSupplyTime = supplies / LifeSupportSetup.Instance.LSConfig.SupplyAmount;

                if (EditorLogic.fetch.ship.parts.Count > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Crew", _labelStyle, GUILayout.Width(90));
                    GUILayout.Label("Supplies", _labelStyle, GUILayout.Width(160));
                    GUILayout.Label("Batteries", _labelStyle, GUILayout.Width(160));
                    GUILayout.Label("Habitation", _labelStyle, GUILayout.Width(160));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Current (" + curCrew + ")", _labelStyle, GUILayout.Width(90));
                    GUILayout.Label(LifeSupportUtilities.SecondsToKerbinTime(totalSupplyTime / Math.Max(1, curCrew) / LifeSupportManager.GetRecyclerMultiplierForParts(EditorLogic.fetch.ship.parts,curCrew)), _labelStyle,
                        GUILayout.Width(160));
                    GUILayout.Label(LifeSupportUtilities.SecondsToKerbinTime(totalBatteryTime / Math.Max(1, curCrew)), _labelStyle,
                        GUILayout.Width(160));
                    if (useHabPenalties)
                        GUILayout.Label(LifeSupportUtilities.SecondsToKerbinTime(totalHabSpace / Math.Max(1, curCrew) * totalHabMult), _labelStyle,
                            GUILayout.Width(160));
                    else
                        GUILayout.Label("indefinite", _labelStyle, GUILayout.Width(160));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Max (" + maxCrew + ")", _labelStyle, GUILayout.Width(90));
                    GUILayout.Label(LifeSupportUtilities.SecondsToKerbinTime(totalSupplyTime / Math.Max(1, maxCrew) / LifeSupportManager.GetRecyclerMultiplierForParts(EditorLogic.fetch.ship.parts, maxCrew)), _labelStyle,
                        GUILayout.Width(160));
                    GUILayout.Label(LifeSupportUtilities.SecondsToKerbinTime(totalBatteryTime / Math.Max(1, maxCrew)), _labelStyle,
                        GUILayout.Width(160));
                    if (useHabPenalties)
                        GUILayout.Label(LifeSupportUtilities.SecondsToKerbinTime(totalHabSpace / Math.Max(1, maxCrew) * totalHabMult), _labelStyle,
                            GUILayout.Width(160));
                    else
                        GUILayout.Label("indefinite", _labelStyle, GUILayout.Width(160));
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        internal void OnDestroy()
        {
            if (orbLogButton == null)
                return;
            ApplicationLauncher.Instance.RemoveModApplication(orbLogButton);
            orbLogButton = null;
        }

        private void InitStyles()
        {
            _windowStyle = new GUIStyle(HighLogic.Skin.window);
            _windowStyle.fixedWidth = 620f;
            _windowStyle.fixedHeight = 400f;
            _labelStyle = new GUIStyle(HighLogic.Skin.label);
            _buttonStyle = new GUIStyle(HighLogic.Skin.button);
            _scrollStyle = new GUIStyle(HighLogic.Skin.scrollView);
            _hasInitStyles = true;
        }
    }
}