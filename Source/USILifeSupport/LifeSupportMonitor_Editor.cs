using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using KSP.UI.Screens;
using UnityEngine;
using USITools;
using System.Linq;
using KSP.Localization;

namespace LifeSupport
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class LifeSupportMonitor_Editor : MonoBehaviour
    {
        private ApplicationLauncherButton _lifeSupportMonitorButton;
        private Rect _windowPosition = new Rect(300, 60, 665, 400);
        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _scrollStyle;
        private Vector2 _scrollPos = Vector2.zero;
        private bool _hasInitStyles = false;
        public static bool _renderDisplay = false;


        void Awake()
        {
            var texture = new Texture2D(36, 36, TextureFormat.RGBA32, false);
            var textureFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Supplies.png");
            print("Loading " + textureFile);
            texture.LoadImage(File.ReadAllBytes(textureFile));
            _lifeSupportMonitorButton = ApplicationLauncher.Instance.AddModApplication(GuiOn, GuiOff, null, null, null, null,
                ApplicationLauncher.AppScenes.ALWAYS, texture);
        }

        private void GuiOn()
        {
            _renderDisplay = true;
        }

        private bool useHabPenalties;
        private bool configLoaded;

        public void Start()
        {
            if (!_hasInitStyles)
                InitStyles();
            //GameEvents.onEditorShipModified.Add(UpdateGUIInfo);
        }

        private void GuiOff()
        {
            _renderDisplay = false;
        }

        private void OnGUI()
        {
            if (!_renderDisplay)
                return;

            if (!configLoaded && LifeSupportScenario.Instance.settings.isLoaded())
            {
                configLoaded = true;
                useHabPenalties = (LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffectVets +
                                   LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffect > 0);
            }


            if (Event.current.type == EventType.Repaint || Event.current.isMouse)
            {
                //preDrawQueue
            }
            UpdateGUIInfo(EditorLogic.fetch.ship);
            Ondraw();
        }


        private void Ondraw()
        {
            _windowPosition = GUILayout.Window(10, _windowPosition, OnWindow, Localizer.Format("#LOC_USILS_Monitortitle"), _windowStyle);//"Life Support Status"
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
          colonySupplies = 0d;
          extraHabTime = 0d;
          fertilizer = 0d;
          habMult = 1d;
          batteryAmount = 0d;
          habs = new List<USILS_HabitationSwapOption>();
          hab_curCrew = "";
          hab_maxCrew = "";
          supplyExt_curCrew = "";
          supplyExt_maxCrew = "";
          habExt_curCrew = "";
          habExt_maxCrew = "";
          supply_curCrew = "";
          supply_maxCrew = "";
          totalHabSpace = 0d;
          totalHabMult = 0d;
          totalBatteryTime = 0d;
          totalSupplyTime = 0d;
          recyclers = new List<USILS_LifeSupportRecyclerSwapOption>();
        }

        private int curCrew = 0;
        private int maxCrew = 0;
        private double supplies = 0d;
        private double colonySupplies = 0d;
        private double fertilizer = 0d;
        private double extraHabTime = 0d;
        private double habMult = 1d;
        private double batteryAmount = 0d;
        private List<USILS_HabitationSwapOption> habs;

        private string hab_curCrew = "";
        private string hab_maxCrew = "";
        private string supply_curCrew = "";
        private string supply_maxCrew = "";

        private string habExt_curCrew = "";
        private string habExt_maxCrew = "";
        private string supplyExt_curCrew = "";
        private string supplyExt_maxCrew = "";

        private double totalHabSpace = 0d;
        private double totalHabMult = 0d;
        private double totalBatteryTime = 0d;
        private double totalSupplyTime = 0d;
        private double totalFertilizerTime = 0d;
        private List<USILS_LifeSupportRecyclerSwapOption> recyclers;

        private void UpdateGUIInfo(ShipConstruct ship)
        {
            ResetValues();
            if (EditorLogic.fetch != null)
            {
                var parts = EditorLogic.fetch.ship.parts;
                var count = parts.Count;
                for (int i = 0; i < count; ++i)
                {
                    var part = parts[i];
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

                for (int i = 0; i < count; ++i)
                {
                    var part = parts[i];
                    var swapOptions = part.FindModulesImplementing<AbstractSwapOption>();
                    var bays = part.FindModulesImplementing<USI_SwappableBay>();
                    if (swapOptions != null && bays != null && swapOptions.Count > 0 && bays.Count > 0)
                    {
                        for (int x = 0; x < bays.Count; x++)
                        {
                            var bay = bays[x];
                            var loadout = swapOptions[bay.currentLoadout] as USILS_HabitationSwapOption;
                            if (loadout != null)
                            {
                                habs.Add(loadout);
                                //Certain modules, in addition to crew capacity, have living space.
                                extraHabTime += loadout.BaseKerbalMonths;
                                //Some modules act more as 'multipliers', dramatically extending a hab's workable lifespan.
                                habMult += loadout.BaseHabMultiplier * Math.Min(1, loadout.CrewCapacity / Math.Max(curCrew, 1));
                            }
                        }
                    }
                    if (part.Resources.Contains("ColonySupplies"))
                    {
                        colonySupplies += part.Resources["ColonySupplies"].amount;
                    }
                    if (part.Resources.Contains("Fertilizer"))
                    {
                        fertilizer += part.Resources["Fertilizer"].amount;
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
                totalFertilizerTime = fertilizer * 10 / LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount;

                if (EditorLogic.fetch.ship.parts.Count > 0)
                {
                    for (int i = 0; i < count; ++i)
                    {
                        var part = parts[i];
                        var swapOptions = part.FindModulesImplementing<AbstractSwapOption>();
                        var bays = part.FindModulesImplementing<USI_SwappableBay>();
                        if (swapOptions != null && bays != null && swapOptions.Count > 0 && bays.Count > 0)
                        {
                            for (int x = 0; x < bays.Count; x++)
                            {
                                var bay = bays[x];
                                var loadout = swapOptions[bay.currentLoadout] as USILS_LifeSupportRecyclerSwapOption;
                                if (loadout != null)
                                {
                                    this.recyclers.Add(loadout);
                                }
                            }
                        }
                    }
                    var recyclerMultiplier_curCrew = LifeSupportManager.GetRecyclerMultiplierForParts(EditorLogic.fetch.ship.parts, curCrew);
                    var recyclerMultiplier_maxCrew = LifeSupportManager.GetRecyclerMultiplierForParts(EditorLogic.fetch.ship.parts, maxCrew);

                    supply_curCrew = LifeSupportUtilities.DurationDisplay(
                        totalSupplyTime /
                        Math.Max(1, curCrew) /
                        recyclerMultiplier_curCrew
                    );
                    supply_maxCrew = LifeSupportUtilities.DurationDisplay(
                        totalSupplyTime /
                        Math.Max(1, maxCrew) /
                        recyclerMultiplier_maxCrew
                    );

                    hab_curCrew = LifeSupportUtilities.DurationDisplay(totalHabSpace / Math.Max(1, curCrew) * totalHabMult);
                    hab_maxCrew = LifeSupportUtilities.DurationDisplay(totalHabSpace / Math.Max(1, maxCrew) * totalHabMult);

                    supplyExt_curCrew = LifeSupportUtilities.DurationDisplay(
                        (totalSupplyTime + totalFertilizerTime) /
                        Math.Max(1, curCrew) /
                        recyclerMultiplier_curCrew
                    );
                    supplyExt_maxCrew = LifeSupportUtilities.DurationDisplay(
                        (totalSupplyTime + totalFertilizerTime) /
                        Math.Max(1, maxCrew) /
                        recyclerMultiplier_maxCrew
                    );
                    //Standard is half a colony supply per hour, or 0.000139 per second.
                    var csupPerSecond = 0.000139d;
                    habExt_curCrew = LifeSupportUtilities.DurationDisplay((totalHabSpace / Math.Max(1, curCrew) * totalHabMult)+(colonySupplies/csupPerSecond/curCrew));
                    habExt_maxCrew = LifeSupportUtilities.DurationDisplay((totalHabSpace / Math.Max(1, maxCrew) * totalHabMult)+(colonySupplies/csupPerSecond/maxCrew));
                }
            }
        }

        private void GenerateWindow()
        {
            GUILayout.BeginVertical();
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, _scrollStyle, GUILayout.Width(645), GUILayout.Height(350));
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
                    string bonCapColor = "F9C004";
                    string bonusColor = "F9D904";

                    // SUMMARY
                    {
                        // column widths
                        const int c1 = 90;
                        const int c2 = 160;
                        const int c3 = 160;
                        const int c4 = 160;

                        // LABELS
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label1"), _labelStyle, GUILayout.Width(c1));//"Crew"
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label2"), _labelStyle, GUILayout.Width(c2));//"Supplies"
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label3"), _labelStyle, GUILayout.Width(c3));//"Batteries"
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label4"), _labelStyle, GUILayout.Width(c4));//"Habitation"
                        GUILayout.EndHorizontal();

                        // CURRENT CREW
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(CTag(Localizer.Format("#LOC_USILS_Label5"), textColor) + CTag(Math.Max(1, curCrew).ToString(), crewColor) + CTag(")", textColor), _labelStyle, GUILayout.Width(c1));//"Current ("
                        GUILayout.Label(CTag(supply_curCrew, textColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(
                            CTag(LifeSupportUtilities.DurationDisplay(totalBatteryTime / Math.Max(1, curCrew)), textColor),
                            _labelStyle,
                            GUILayout.Width(c3)
                        );
                        if (useHabPenalties)
                            GUILayout.Label(CTag(hab_curCrew, textColor), _labelStyle, GUILayout.Width(160));
                        else
                            GUILayout.Label(CTag(Localizer.Format("#LOC_USILS_IndefiniteText"), textColor), _labelStyle, GUILayout.Width(c4));//"indefinite"
                        GUILayout.EndHorizontal();

                        // CURRENT CREW WITH EXTENSIONS
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(CTag(Localizer.Format("#LOC_USILS_Label6"), bonCapColor), _labelStyle, GUILayout.Width(c1));//"+Fertilizer:"
                        GUILayout.Label(CTag(supplyExt_curCrew, bonusColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(CTag(Localizer.Format("#LOC_USILS_Label7"), bonCapColor), _labelStyle, GUILayout.Width(c3));//"+Colony Supplies:"
                        if (useHabPenalties)
                            GUILayout.Label(CTag(habExt_curCrew, bonusColor), _labelStyle, GUILayout.Width(160));
                        else
                            GUILayout.Label(CTag(Localizer.Format("#LOC_USILS_IndefiniteText"), bonusColor), _labelStyle, GUILayout.Width(c4));//"indefinite"
                        GUILayout.EndHorizontal();

                        // MAX CREW
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(CTag(Localizer.Format("#LOC_USILS_Label8"), textColor) + CTag(Math.Max(1, maxCrew).ToString(), crewColor) + CTag(")", textColor), _labelStyle, GUILayout.Width(c1));//"Max ("
                        GUILayout.Label(CTag(supply_maxCrew, textColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(
                            CTag(LifeSupportUtilities.DurationDisplay(totalBatteryTime / Math.Max(1, maxCrew)), textColor),
                            _labelStyle,
                            GUILayout.Width(c3)
                        );
                        if (useHabPenalties)
                            GUILayout.Label(CTag(hab_maxCrew, textColor), _labelStyle, GUILayout.Width(160));
                        else
                            GUILayout.Label(CTag(Localizer.Format("#LOC_USILS_IndefiniteText"), textColor), _labelStyle, GUILayout.Width(160));//"indefinite"
                        GUILayout.EndHorizontal();

                        // MAX WITH EXTENSIONS
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(CTag(Localizer.Format("#LOC_USILS_Label6"), bonCapColor), _labelStyle, GUILayout.Width(c1));//"+Fertilizer:"
                        GUILayout.Label(CTag(supplyExt_maxCrew, bonusColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(CTag(Localizer.Format("#LOC_USILS_Label7"), bonCapColor),_labelStyle,GUILayout.Width(c3));//"+Colony Supplies:"
                        if (useHabPenalties)
                            GUILayout.Label(CTag(habExt_maxCrew, bonusColor), _labelStyle, GUILayout.Width(160));
                        else
                            GUILayout.Label(CTag(Localizer.Format("#LOC_USILS_IndefiniteText"), bonusColor), _labelStyle, GUILayout.Width(c4));//"indefinite"
                        GUILayout.EndHorizontal();

                    }

                    GUILayout.Space(20);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("<b>"+Localizer.Format("#LOC_USILS_Details")+"</b>", _labelStyle, GUILayout.Width(150));//"Details"
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
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label4"), _labelStyle, GUILayout.Width(c1 - 30));//"Habitation"
                        GUILayout.Label(CTag("= ( (", operColor), _labelStyle, GUILayout.Width(30));
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label9") + CTag(" *", operColor), _labelStyle, GUILayout.Width(c2));//"BaseTime "
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label10") + CTag(" ) +", operColor), _labelStyle, GUILayout.Width(c3));//"MaxCrew "
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label11") + CTag(" ) *", operColor), _labelStyle, GUILayout.Width(c4));//"ExtraTime "
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label12") + CTag(" /", operColor), _labelStyle, GUILayout.Width(c5));//"Multiplier "
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label13") + CTag(" *", operColor), _labelStyle, GUILayout.Width(c6));//"Crew "
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label14"), _labelStyle, GUILayout.Width(c7));//"Months"
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(CTag(hab_curCrew, textColor), _labelStyle, GUILayout.Width(c1));
                        GUILayout.Label(CTag(LifeSupportScenario.Instance.settings.GetSettings().BaseHabTime.ToString(), fadeColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(CTag(maxCrew.ToString(), crewColor), _labelStyle, GUILayout.Width(c3));
                        GUILayout.Label(CTag(extraHabTime.ToString(), textColor), _labelStyle, GUILayout.Width(c4));
                        GUILayout.Label(CTag("(1+" + (habMult-1d) +")", textColor), _labelStyle, GUILayout.Width(c5));
                        GUILayout.Label(CTag(Math.Max(1, curCrew).ToString(), crewColor), _labelStyle, GUILayout.Width(c6));
                        GUILayout.Label(CTag(LifeSupportScenario.Instance.settings.GetSettings().HabMultiplier.ToString(), fadeColor), _labelStyle, GUILayout.Width(c7));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(CTag(hab_maxCrew, textColor), _labelStyle, GUILayout.Width(c1));
                        GUILayout.Label(CTag(LifeSupportScenario.Instance.settings.GetSettings().BaseHabTime.ToString(), fadeColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(CTag(maxCrew.ToString(), crewColor), _labelStyle, GUILayout.Width(c3));
                        GUILayout.Label(CTag(extraHabTime.ToString(), textColor), _labelStyle, GUILayout.Width(c4));
                        GUILayout.Label(CTag("(1+" + (habMult - 1d) + ")", textColor), _labelStyle, GUILayout.Width(c5));
                        GUILayout.Label(CTag(Math.Max(1, maxCrew).ToString(), crewColor), _labelStyle, GUILayout.Width(c6));
                        GUILayout.Label(CTag(LifeSupportScenario.Instance.settings.GetSettings().HabMultiplier.ToString(), fadeColor), _labelStyle, GUILayout.Width(c7));
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.Space(20);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("<b>"+Localizer.Format("#LOC_USILS_Parts")+"</b>", _labelStyle, GUILayout.Width(150));//"Parts"
                    GUILayout.EndHorizontal();

                    // RECYCLERS
                    {
                        // column widths
                        const int c1 = 230;
                        const int c2 = 80;
                        const int c3 = 150;

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label15"), _labelStyle, GUILayout.Width(c1));//"Recycler"
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label16"), _labelStyle, GUILayout.Width(c2));//"Recycle %"
                        GUILayout.Label(Localizer.Format("#LOC_USILS_Label17"), _labelStyle, GUILayout.Width(c3));//"Crew-Capacity"
                        GUILayout.EndHorizontal();

                        var rCount = recyclers.Count;
                        for (int x = 0; x < rCount; ++x)
                        {
                            var recycler = recyclers[x];
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(CTag(recycler.part.partInfo.title, partColor), _labelStyle, GUILayout.Width(c1));
                            GUILayout.Label(CTag(((int)(recycler.RecyclePercent * 100)).ToString(), textColor), _labelStyle, GUILayout.Width(c2));
                            GUILayout.Label(CTag(recycler.CrewCapacity.ToString(), textColor), _labelStyle, GUILayout.Width(c3));
                            GUILayout.EndHorizontal();
                        }

                        // HABITATION
                        if (useHabPenalties)
                        {
                            GUILayout.Space(10);

                            GUILayout.BeginHorizontal();
                            GUILayout.Label(Localizer.Format("#LOC_USILS_Label4"), _labelStyle, GUILayout.Width(c1));//"Habitation"
                            GUILayout.Label(Localizer.Format("#LOC_USILS_Label11"), _labelStyle, GUILayout.Width(c2));//"ExtraTime"
                            GUILayout.Label(Localizer.Format("#LOC_USILS_Label12"), _labelStyle, GUILayout.Width(c3));//"Multiplier"
                            GUILayout.EndHorizontal();

                            var hCount = habs.Count;
                            for (int x = 0; x < hCount; ++x)
                            {
                                var hab = habs[x];
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(CTag(hab.part.partInfo.title, partColor), _labelStyle, GUILayout.Width(c1));
                                GUILayout.Label(CTag(hab.BaseKerbalMonths.ToString(), textColor), _labelStyle, GUILayout.Width(c2));
                                GUILayout.Label(CTag(hab.BaseHabMultiplier.ToString(), textColor), _labelStyle, GUILayout.Width(c3));
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
            if (_lifeSupportMonitorButton == null)
                return;
            ApplicationLauncher.Instance.RemoveModApplication(_lifeSupportMonitorButton);
            _lifeSupportMonitorButton = null;
            GameEvents.onEditorShipModified.Remove(UpdateGUIInfo);
        }

        private void InitStyles()
        {
            _windowStyle = new GUIStyle(HighLogic.Skin.window);
            _windowStyle.fixedWidth = _windowPosition.width;
            _windowStyle.fixedHeight = _windowPosition.height;
            _labelStyle = new GUIStyle(HighLogic.Skin.label);
            _scrollStyle = new GUIStyle(HighLogic.Skin.scrollView);
            _hasInitStyles = true;
        }

        private string CTag(string text, string colorHex)
        {
            return String.Format("<color=#{0}>{1}</color>", colorHex, text);
        }
    }
}