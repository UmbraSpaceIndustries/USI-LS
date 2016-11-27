using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using KSP.UI.Screens;
using UnityEngine;
using USITools;

namespace LifeSupport
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class LifeSupportMonitor_Editor : MonoBehaviour
    {
        private ApplicationLauncherButton orbLogButton;
        private Rect _windowPosition = new Rect(300, 60, 665, 400);
        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _scrollStyle;
        private Vector2 scrollPos = Vector2.zero;
        private bool _hasInitStyles = false;
        public static bool renderDisplay = false;


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
            renderDisplay = true;
        }

        private bool useHabPenalties;

        public void Start()
        {
            if (!_hasInitStyles)
                InitStyles();
            useHabPenalties = (LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffectVets +
                                   LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffect > 0);

            GameEvents.onEditorShipModified.Add(UpdateGUIInfo);
        }

        private void GuiOff()
        {
            renderDisplay = false;
        }

        private void OnGUI()
        {
            if (!renderDisplay)
                return;

            if (Event.current.type == EventType.Repaint || Event.current.isMouse)
            {
                //preDrawQueue
            }
            Ondraw();
        }


        private void Ondraw()
        {
            _windowPosition = GUILayout.Window(10, _windowPosition, OnWindow, "Life Support Status", _windowStyle);
        }

        private void OnWindow(int windowId)
        {
            GenerateWindow();
        }

        private void ResetValues()
        {
          curCrew = 0;
          maxCrew = 0;
          supplies = 0d;
          extraHabTime = 0d;
          habMult = 1d;
          batteryAmount = 0d;
          habs = new List<ModuleHabitation>();
          hab_curCrew = "";
          hab_maxCrew = "";
          supply_curCrew = "";
          supply_maxCrew = "";
          totalHabSpace = 0d;
          totalHabMult = 0d;
          totalBatteryTime = 0d;
          totalSupplyTime = 0d;
          recyclers = new List<ModuleLifeSupportRecycler>();
        }

        private int curCrew = 0;
        private int maxCrew = 0;
        private double supplies = 0d;
        private double extraHabTime = 0d;
        private double habMult = 1d;
        private double batteryAmount = 0d;
        private List<ModuleHabitation> habs;
        private string hab_curCrew = "";
        private string hab_maxCrew = "";
        private string supply_curCrew = "";
        private string supply_maxCrew = "";
        private double totalHabSpace = 0d;
        private double totalHabMult = 0d;
        private double totalBatteryTime = 0d;
        private double totalSupplyTime = 0d;
        private List<ModuleLifeSupportRecycler> recyclers;

        private void UpdateGUIInfo(ShipConstruct ship)
        {
            ResetValues();
            if (EditorLogic.fetch != null)
            {

                foreach (var part in EditorLogic.fetch.ship.parts)
                {
                    maxCrew += part.CrewCapacity;
                }

                var dialog = KSP.UI.CrewAssignmentDialog.Instance;

                if (dialog != null)
                {
                    VesselCrewManifest manifest = dialog.GetManifest();
                    if (manifest != null)
                    {
                        curCrew = manifest.CrewCount;
                    }
                }


                foreach (var part in EditorLogic.fetch.ship.parts)
                {
                    var hab = part.Modules.GetModule<ModuleHabitation>();
                    if (hab != null)
                    {
                        var conList = part.Modules.GetModules<BaseConverter>();
                        var bayList = part.Modules.GetModules<ModuleSwappableConverter>();
                        if (bayList == null || bayList.Count == 0)
                        {
                            habs.Add(hab);
                            //Certain modules, in addition to crew capacity, have living space.
                            extraHabTime += hab.KerbalMonths;
                            //Some modules act more as 'multipliers', dramatically extending a hab's workable lifespan.
                            habMult += hab.HabMultiplier * Math.Min(1, hab.CrewCapacity / Math.Max(curCrew, 1));
                        }
                        else
                        {
                            foreach (var bay in bayList)
                            {
                                var con = conList[bay.currentLoadout] as ModuleHabitation;
                                if (con != null)
                                {
                                    habs.Add(con);
                                    extraHabTime += con.KerbalMonths;
                                    habMult += con.HabMultiplier * Math.Min(1, con.CrewCapacity / Math.Max(curCrew, 1));
                                }
                            }
                        }
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


                totalHabSpace = (LifeSupportScenario.Instance.settings.GetSettings().BaseHabTime * maxCrew) + extraHabTime;
                //A Kerbal month is 30 six-hour Kerbin days.
                totalHabMult = habMult * LifeSupportScenario.Instance.settings.GetSettings().HabMultiplier * LifeSupportUtilities.SecondsPerMonth();
                totalBatteryTime = batteryAmount / LifeSupportScenario.Instance.settings.GetSettings().ECAmount;
                totalSupplyTime = supplies / LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount;

                if (EditorLogic.fetch.ship.parts.Count > 0)
                {
                    foreach (var p in EditorLogic.fetch.ship.parts)
                    {
                        var mod = p.FindModuleImplementing<ModuleLifeSupportRecycler>();
                        if (mod == null)
                            continue;

                        recyclers.Add(mod);
                    }
                    var recyclerMultiplier_curCrew = LifeSupportManager.GetRecyclerMultiplierForParts(EditorLogic.fetch.ship.parts, curCrew);
                    var recyclerMultiplier_maxCrew = LifeSupportManager.GetRecyclerMultiplierForParts(EditorLogic.fetch.ship.parts, maxCrew);

                    supply_curCrew = LifeSupportUtilities.SecondsToKerbinTime(
                        totalSupplyTime /
                        Math.Max(1, curCrew) /
                        recyclerMultiplier_curCrew
                    );
                    supply_maxCrew = LifeSupportUtilities.SecondsToKerbinTime(
                        totalSupplyTime /
                        Math.Max(1, maxCrew) /
                        recyclerMultiplier_maxCrew
                    );

                    hab_curCrew = LifeSupportUtilities.SecondsToKerbinTime(totalHabSpace / Math.Max(1, curCrew) * totalHabMult);
                    hab_maxCrew = LifeSupportUtilities.SecondsToKerbinTime(totalHabSpace / Math.Max(1, maxCrew) * totalHabMult);
                }
            }
        }


        private void GenerateWindow()
        {
            GUILayout.BeginVertical();
            scrollPos = GUILayout.BeginScrollView(scrollPos, _scrollStyle, GUILayout.Width(645), GUILayout.Height(350));
            GUILayout.BeginVertical();

            if (EditorLogic.fetch != null)
            {
                if (EditorLogic.fetch.ship.parts.Count > 0)
                {
                    // Colors
                    string operColor = "99FF33";
                    string textColor = "FFFFFF";
                    string crewColor = "ADD8E6";
                    string fadeColor = "909090";
                    string partColor = "FFCC00";

                    // SUMMARY
                    {
                        // column widths
                        const int c1 = 90;
                        const int c2 = 160;
                        const int c3 = 160;
                        const int c4 = 160;

                        // LABELS
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Crew", _labelStyle, GUILayout.Width(c1));
                        GUILayout.Label("Supplies", _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label("Batteries", _labelStyle, GUILayout.Width(c3));
                        GUILayout.Label("Habitation", _labelStyle, GUILayout.Width(c4));
                        GUILayout.EndHorizontal();

                        // CURRENT CREW
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(CTag("Current (", textColor) + CTag(Math.Max(1, curCrew).ToString(), crewColor) + CTag(")", textColor), _labelStyle, GUILayout.Width(c1));
                        GUILayout.Label(CTag(supply_curCrew, textColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(
                            CTag(LifeSupportUtilities.SecondsToKerbinTime(totalBatteryTime / Math.Max(1, curCrew)), textColor),
                            _labelStyle,
                            GUILayout.Width(c3)
                        );
                        if (useHabPenalties)
                            GUILayout.Label(CTag(hab_curCrew, textColor), _labelStyle, GUILayout.Width(160));
                        else
                            GUILayout.Label(CTag("indefinite", textColor), _labelStyle, GUILayout.Width(c4));
                        GUILayout.EndHorizontal();

                        // MAX CREW
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(CTag("Max (", textColor) + CTag(Math.Max(1, maxCrew).ToString(), crewColor) + CTag(")", textColor), _labelStyle, GUILayout.Width(c1));
                        GUILayout.Label(CTag(supply_maxCrew, textColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(
                            CTag(LifeSupportUtilities.SecondsToKerbinTime(totalBatteryTime / Math.Max(1, maxCrew)), textColor),
                            _labelStyle,
                            GUILayout.Width(c3)
                        );
                        if (useHabPenalties)
                            GUILayout.Label(CTag(hab_maxCrew, textColor), _labelStyle, GUILayout.Width(160));
                        else
                            GUILayout.Label(CTag("indefinite", textColor), _labelStyle, GUILayout.Width(160));
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.Space(20);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("<b>Details</b>", _labelStyle, GUILayout.Width(150));
                    GUILayout.EndHorizontal();

                    // HABITATION EQUATION
                    if (useHabPenalties)
                    {
                        // column widths
                        const int c1 = 150;
                        const int c2 = 80;
                        const int c3 = 80;
                        const int c4 = 90;
                        const int c5 = 80;
                        const int c6 = 50;
                        const int c7 = 50;

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Habitation", _labelStyle, GUILayout.Width(c1 - 30));
                        GUILayout.Label(CTag("= ( (", operColor), _labelStyle, GUILayout.Width(30));
                        GUILayout.Label("BaseTime " + CTag("*", operColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label("MaxCrew " + CTag(") +", operColor), _labelStyle, GUILayout.Width(c3));
                        GUILayout.Label("ExtraTime " + CTag(") *", operColor), _labelStyle, GUILayout.Width(c4));
                        GUILayout.Label("Multiplier " + CTag("/", operColor), _labelStyle, GUILayout.Width(c5));
                        GUILayout.Label("Crew " + CTag("*", operColor), _labelStyle, GUILayout.Width(c6));
                        GUILayout.Label("Months", _labelStyle, GUILayout.Width(c7));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(CTag(hab_curCrew, textColor), _labelStyle, GUILayout.Width(c1));
                        GUILayout.Label(CTag(LifeSupportScenario.Instance.settings.GetSettings().BaseHabTime.ToString(), fadeColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(CTag(maxCrew.ToString(), crewColor), _labelStyle, GUILayout.Width(c3));
                        GUILayout.Label(CTag(extraHabTime.ToString(), textColor), _labelStyle, GUILayout.Width(c4));
                        GUILayout.Label(CTag(habMult.ToString(), textColor), _labelStyle, GUILayout.Width(c5));
                        GUILayout.Label(CTag(Math.Max(1, curCrew).ToString(), crewColor), _labelStyle, GUILayout.Width(c6));
                        GUILayout.Label(CTag(LifeSupportScenario.Instance.settings.GetSettings().HabMultiplier.ToString(), fadeColor), _labelStyle, GUILayout.Width(c7));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(CTag(hab_maxCrew, textColor), _labelStyle, GUILayout.Width(c1));
                        GUILayout.Label(CTag(LifeSupportScenario.Instance.settings.GetSettings().BaseHabTime.ToString(), fadeColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(CTag(maxCrew.ToString(), crewColor), _labelStyle, GUILayout.Width(c3));
                        GUILayout.Label(CTag(extraHabTime.ToString(), textColor), _labelStyle, GUILayout.Width(c4));
                        GUILayout.Label(CTag(habMult.ToString(), textColor), _labelStyle, GUILayout.Width(c5));
                        GUILayout.Label(CTag(Math.Max(1, maxCrew).ToString(), crewColor), _labelStyle, GUILayout.Width(c6));
                        GUILayout.Label(CTag(LifeSupportScenario.Instance.settings.GetSettings().HabMultiplier.ToString(), fadeColor), _labelStyle, GUILayout.Width(c7));
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.Space(20);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("<b>Parts</b>", _labelStyle, GUILayout.Width(150));
                    GUILayout.EndHorizontal();

                    // RECYCLERS
                    {
                        // column widths
                        const int c1 = 230;
                        const int c2 = 80;
                        const int c3 = 150;

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Recycler", _labelStyle, GUILayout.Width(c1));
                        GUILayout.Label("Recycle %", _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label("Crew-Capacity", _labelStyle, GUILayout.Width(c3));
                        GUILayout.EndHorizontal();

                        foreach (var recycler in recyclers)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(CTag(recycler.part.partInfo.title, partColor), _labelStyle, GUILayout.Width(c1));
                            GUILayout.Label(CTag(((int)(recycler.AdjustedRecyclePercent * 100)).ToString(), textColor), _labelStyle, GUILayout.Width(c2));
                            GUILayout.Label(CTag(recycler.CrewCapacity.ToString(), textColor), _labelStyle, GUILayout.Width(c3));
                            GUILayout.EndHorizontal();
                        }

                        // HABITATION
                        if (useHabPenalties)
                        {
                            GUILayout.Space(10);

                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Habitation", _labelStyle, GUILayout.Width(c1));
                            GUILayout.Label("ExtraTime", _labelStyle, GUILayout.Width(c2));
                            GUILayout.Label("Multiplier", _labelStyle, GUILayout.Width(c3));
                            GUILayout.EndHorizontal();

                            foreach (var hab in habs)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(CTag(hab.part.partInfo.title, partColor), _labelStyle, GUILayout.Width(c1));
                                GUILayout.Label(CTag(hab.KerbalMonths.ToString(), textColor), _labelStyle, GUILayout.Width(c2));
                                GUILayout.Label(CTag(hab.HabMultiplier.ToString(), textColor), _labelStyle, GUILayout.Width(c3));
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
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
            GameEvents.onEditorShipModified.Remove(UpdateGUIInfo);
        }

        private void InitStyles()
        {
            _windowStyle = new GUIStyle(HighLogic.Skin.window);
            _windowStyle.fixedWidth = _windowPosition.width;
            _windowStyle.fixedHeight = _windowPosition.height;
            _labelStyle = new GUIStyle(HighLogic.Skin.label);
            _buttonStyle = new GUIStyle(HighLogic.Skin.button);
            _scrollStyle = new GUIStyle(HighLogic.Skin.scrollView);
            _hasInitStyles = true;
        }

        private string CTag(string text, string colorHex)
        {
            return String.Format("<color=#{0}>{1}</color>", colorHex, text);
        }
    }
}