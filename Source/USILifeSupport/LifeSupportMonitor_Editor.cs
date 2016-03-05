using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using USITools;

namespace LifeSupport
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class LifeSupportMonitor_Editor : MonoBehaviour
    {
        private ApplicationLauncherButton orbLogButton;
        private IButton orbLogTButton;
        private Rect _windowPosition = new Rect(300, 60, 665, 400);
        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _scrollStyle;
        private Vector2 scrollPos = Vector2.zero;
        private bool _hasInitStyles = false;
        private bool windowVisible;

        void Awake()
        {
            if (ToolbarManager.ToolbarAvailable)
            {
                this.orbLogTButton = ToolbarManager.Instance.add("USILS", "orbLog");
                orbLogTButton.TexturePath = "UmbraSpaceIndustries/LifeSupport/Supplies24";
                orbLogTButton.ToolTip = "USI Life Support";
                orbLogTButton.Enabled = true;
                orbLogTButton.OnClick += (e) => { if (windowVisible) { GuiOff(); windowVisible = false; } else { GuiOn(); windowVisible = true; } };
            }
            else
            {
                var textureFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Supplies.png");
                var texture = new Texture2D(36, 36, TextureFormat.RGBA32, false);
                print("Loading " + textureFile);
                texture.LoadImage(File.ReadAllBytes(textureFile));
                this.orbLogButton = ApplicationLauncher.Instance.AddModApplication(GuiOn, GuiOff, null, null, null, null,
                    ApplicationLauncher.AppScenes.ALWAYS, texture);
            }
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
            scrollPos = GUILayout.BeginScrollView(scrollPos, _scrollStyle, GUILayout.Width(645), GUILayout.Height(350));
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

                List<ModuleHabitation> habs = new List<ModuleHabitation>();

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
                        habs.Add(hab);

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
                    List<ModuleLifeSupportRecycler> recyclers = new List<ModuleLifeSupportRecycler>();
                    foreach (var p in EditorLogic.fetch.ship.parts)
                    {
                        var mod = p.FindModuleImplementing<ModuleLifeSupportRecycler>();
                        if (mod == null)
                            continue;

                        recyclers.Add(mod);
                    }
                    var recyclerMultiplier_curCrew = LifeSupportManager.GetRecyclerMultiplierForParts(EditorLogic.fetch.ship.parts, curCrew);
                    var recyclerMultiplier_maxCrew = LifeSupportManager.GetRecyclerMultiplierForParts(EditorLogic.fetch.ship.parts, maxCrew);

                    var supply_curCrew = LifeSupportUtilities.SecondsToKerbinTime(
                        totalSupplyTime /
                        Math.Max(1, curCrew) /
                        recyclerMultiplier_curCrew
                    );
                    var supply_maxCrew = LifeSupportUtilities.SecondsToKerbinTime(
                        totalSupplyTime /
                        Math.Max(1, maxCrew) /
                        recyclerMultiplier_maxCrew
                    );

                    var hab_curCrew = LifeSupportUtilities.SecondsToKerbinTime(totalHabSpace / Math.Max(1, curCrew) * totalHabMult);
                    var hab_maxCrew = LifeSupportUtilities.SecondsToKerbinTime(totalHabSpace / Math.Max(1, maxCrew) * totalHabMult);

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

                        // hab = ((LSConfig.BaseHabTime * maxCrew) + ExtraHabTime) * Hab-Multiplier / Crew * LSConfig.HabMultiplier[Kerbin-Months]
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
                        GUILayout.Label(CTag(LifeSupportSetup.Instance.LSConfig.BaseHabTime.ToString(), fadeColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(CTag(maxCrew.ToString(), crewColor), _labelStyle, GUILayout.Width(c3));
                        GUILayout.Label(CTag(extraHabTime.ToString(), textColor), _labelStyle, GUILayout.Width(c4));
                        GUILayout.Label(CTag(habMult.ToString(), textColor), _labelStyle, GUILayout.Width(c5));
                        GUILayout.Label(CTag(Math.Max(1, curCrew).ToString(), crewColor), _labelStyle, GUILayout.Width(c6));
                        GUILayout.Label(CTag(LifeSupportSetup.Instance.LSConfig.HabMultiplier.ToString(), fadeColor), _labelStyle, GUILayout.Width(c7));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(CTag(hab_maxCrew, textColor), _labelStyle, GUILayout.Width(c1));
                        GUILayout.Label(CTag(LifeSupportSetup.Instance.LSConfig.BaseHabTime.ToString(), fadeColor), _labelStyle, GUILayout.Width(c2));
                        GUILayout.Label(CTag(maxCrew.ToString(), crewColor), _labelStyle, GUILayout.Width(c3));
                        GUILayout.Label(CTag(extraHabTime.ToString(), textColor), _labelStyle, GUILayout.Width(c4));
                        GUILayout.Label(CTag(habMult.ToString(), textColor), _labelStyle, GUILayout.Width(c5));
                        GUILayout.Label(CTag(Math.Max(1, maxCrew).ToString(), crewColor), _labelStyle, GUILayout.Width(c6));
                        GUILayout.Label(CTag(LifeSupportSetup.Instance.LSConfig.HabMultiplier.ToString(), fadeColor), _labelStyle, GUILayout.Width(c7));
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
                            GUILayout.Label(CTag(((int)(recycler.RecyclePercent * 100)).ToString(), textColor), _labelStyle, GUILayout.Width(c2));
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
            if (orbLogButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(orbLogButton);
                orbLogButton = null;
            }
            if (orbLogTButton != null)
            {
                orbLogTButton.Destroy();
                orbLogTButton = null;
            }
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