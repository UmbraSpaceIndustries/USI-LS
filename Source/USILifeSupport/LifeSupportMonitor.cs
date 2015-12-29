using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace LifeSupport 
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class LifeSupportMonitor : MonoBehaviour
    {
        private ApplicationLauncherButton orbLogButton;
        private Rect _windowPosition = new Rect(300, 60, 600, 400);
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

        private void FixedUpdate()
        {
            foreach (var v in FlightGlobals.Vessels.Where(v => v.isEVA))
            {
                var c = v.GetVesselCrew().First();
                if (LifeSupportSetup.Instance.LSConfig.CausesDeath)
                {
                    //Lone exception is a landed EVA Kerbal on Kerbin.
                    if (v.situation == Vessel.Situations.LANDED && v.mainBody.bodyName == "Kerbin")
                        return;
                    //Check their status.
                    var k = LifeSupportManager.Instance.FetchKerbal(c);
                    //Only if they are unsupplied
                    if (k.LastMeal > Planetarium.GetUniversalTime() - LifeSupportSetup.Instance.LSConfig.SupplyTime)
                        return;
                    
                    string msg = string.Format("{0} has died of starvation", c.name);
                    LifeSupportManager.Instance.UntrackKerbal(c.name);
                    ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                    c.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                    v.DestroyVesselComponents();
                    return;
                }
            }
        }

        private void GenerateWindow()
        {
            

            GUILayout.BeginVertical();
            scrollPos = GUILayout.BeginScrollView(scrollPos, _scrollStyle, GUILayout.Width(580), GUILayout.Height(350));
            GUILayout.BeginVertical();
            var curTime = Planetarium.GetUniversalTime();
            LifeSupportManager.Instance.UpdateVesselStats();

            var statList = new List<LifeSupportDisplayStat>();
            foreach (var v in LifeSupportManager.Instance.VesselSupplyInfo)
            {
                statList.Add(new LifeSupportDisplayStat
                             {
                                 SupplyTime = v.SuppliesLeft,
                                 LastUpdate = v.LastUpdate,
                                 LastFeeding = v.LastFeeding,
                                 UpdateLabel = "last update:",
                                 DisplayTitle = String.Format("<color=#F9FF8A>{0}</color> <color=#FFFFFF>({1} crew)</color>", v.VesselName, v.NumCrew)
                             });

            }

            foreach (var v in FlightGlobals.Vessels.Where(v => v.isEVA))
            {
                var k = LifeSupportManager.Instance.FetchKerbal(v.GetVesselCrew().First());
                statList.Add(new LifeSupportDisplayStat
                {
                    SupplyTime = 0,
                    LastUpdate = k.LastMeal,
                    LastFeeding = k.LastMeal,
                    UpdateLabel = "EVA Duration:",
                    DisplayTitle = String.Format("<color=#F9FF8A>{0}</color> <color=#FFFFFF>(EVA)</color>", v.vesselName)
                });
            }

            foreach (var v in statList.OrderByDescending(s=>s.LastUpdate))
            {
                var tUp = curTime - v.LastUpdate;
                var foodEaten = Planetarium.GetUniversalTime() - v.LastFeeding;
                var snacksLeft = v.SupplyTime - foodEaten;
                
                var tSnack = snacksLeft;
                var lblColor = "6FFF00";
                if (tSnack < 60 * 60 * 6 * 15) //15 days
                {
                    lblColor = "FFE100";
                }
                if (tSnack < 0)
                {
                    lblColor = "FFAE00";
                }
                if (tSnack < -60 * 60 * 6 * 15)
                {
                    lblColor = "FF5E5E";
                }

                var lblUpdate = "BBBBBB";
                if (tUp < 1)
                {
                    lblUpdate = "FFFFFF";
                }

                string updateString = SecondsToKerbinTime(tUp);
                string snackString = SecondsToKerbinTime(tSnack);

                GUILayout.Label(String.Format("{0}",v.DisplayTitle), _labelStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("", _labelStyle, GUILayout.Width(20));
                GUILayout.Label("supplies:", _labelStyle, GUILayout.Width(80));
                GUILayout.Label(String.Format("<color=#{0}>{1}</color>", lblColor, snackString), _labelStyle, GUILayout.Width(160));
                GUILayout.Label(v.UpdateLabel, _labelStyle, GUILayout.Width(90));
                GUILayout.Label(String.Format("<color=#{0}>{1}</color>", lblUpdate, updateString), _labelStyle, GUILayout.Width(160));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private string SecondsToKerbinTime(double tSnack)
        {
            const double secsPerMinute = 60d;
            const double secsPerHour = secsPerMinute * 60d;
			double secsPerDay = GameSettings.KERBIN_TIME ? secsPerHour * 6d : secsPerHour * 24d ;
			double secsPerYear = GameSettings.KERBIN_TIME ? secsPerDay * 425d : secsPerDay * 365d ;
            double s = Math.Abs(tSnack);
            double y = Math.Floor(s/secsPerYear);
            s = s - (y * secsPerYear);
            double d = Math.Floor(s/secsPerDay);
            s = s - (d*secsPerDay);
            double h = Math.Floor(s / secsPerHour);
            s = s - (h * secsPerHour);
            double m = Math.Floor(s / secsPerMinute);
            s = s - (m * secsPerMinute);

            var sign = "";
            if (tSnack < 0)
                sign = "-";

            return string.Format("{0}{1:0}y:{2:0}d:{3:00}h:{4:00}m:{5:00}s", sign,y,d,h,m,s);
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
            _windowStyle.fixedWidth = 600f;
            _windowStyle.fixedHeight = 400f;
            _labelStyle = new GUIStyle(HighLogic.Skin.label);
            _buttonStyle = new GUIStyle(HighLogic.Skin.button);
            _scrollStyle = new GUIStyle(HighLogic.Skin.scrollView);
            _hasInitStyles = true;
        }
    }

    public class LifeSupportDisplayStat
    {
        public double LastUpdate { get; set; }
        public double LastFeeding { get; set; }
        public double SupplyTime { get; set; }
        public string DisplayTitle { get; set; }
        public string UpdateLabel { get; set; }
        public string UpdateColor { get; set; }
    }
}
