using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using KSP.UI.Screens;
using UnityEngine;
using Random = System.Random;

namespace LifeSupport 
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class LifeSupportMonitor_Flight : LifeSupportMonitor
    {
        protected override bool IsActive()
        {
            return HighLogic.LoadedSceneIsFlight;
        }
    }

    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class LifeSupportMonitor_TStation : LifeSupportMonitor
    {
        protected override bool IsActive()
        {
            return HighLogic.LoadedScene == GameScenes.TRACKSTATION;
        }
    }


    public class LifeSupportMonitor : MonoBehaviour
    {
        private ApplicationLauncherButton orbLogButton;
        private Rect _windowPosition = new Rect(300, 60, 820, 400);
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

        public void Start()
        {
            if (!_hasInitStyles)
                InitStyles();
        }

        private void GuiOff()
        {
            renderDisplay = false;
        }


        private void OnGUI()
        {
            if (!renderDisplay)
                return;
            if (!IsActive())
                return;


            if (Event.current.type == EventType.Repaint || Event.current.isMouse)
            {
                //preDrawQueue
            }
            Ondraw();
        }

        protected virtual bool IsActive()
        {
            return false;
        }


        private void Ondraw()
        {
            _windowPosition = GUILayout.Window(10, _windowPosition, OnWindow, "Life Support Status", _windowStyle);
        }

        private void OnWindow(int windowId)
        {
            GenerateWindow();
        }

        private double _lastGUIUpdate;
        private double _guiCheckInterval = 1d;

        private void CheckEVAKerbals()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            var vList = GetNearbyVessels(2000, false, FlightGlobals.ActiveVessel, false);
            var count = vList.Count;
            for (int i = 0; i < count; ++i)
            {
                var v = vList[i];
                if(v.isEVA)
                    continue;

                if (v.mainBody == FlightGlobals.GetHomeBody())
                    if (v.altitude < LifeSupportScenario.Instance.settings.GetSettings().HomeWorldAltitude)
                        continue;

                var c = v.GetVesselCrew()[0];
                //Check their status.
                var k = LifeSupportManager.Instance.FetchKerbal(c);
                if (v.missionTime > LifeSupportScenario.Instance.settings.GetSettings().EVATime)
                {
                    print("Applying EVA Effect");
                    ApplyEVAEffect(k, c, v,
                        LifeSupportManager.isVet(k.KerbalName)
                            ? LifeSupportScenario.Instance.settings.GetSettings().EVAEffectVets
                            : LifeSupportScenario.Instance.settings.GetSettings().EVAEffect);
                }
            }
        }

        private void ApplyEVAEffect(LifeSupportStatus kStat, ProtoCrewMember crew, Vessel v, int effectId)
        {
            /*
             *  SIDE EFFECTS:
             * 
             *  0 = No Effect (The feature is effectively turned off
             *  1 = Grouchy (they become a Tourist until rescued)
             *  2 = Mutinous (A tourist, but destroys a part of a nearby vessel...)
             *  3 = Instantly 'wander' back to the KSC - don't ask us how!
             *  4 = M.I.A. (will eventually respawn)
             *  5 = K.I.A. 
             * 
             */

            var msg = "";
            switch (effectId)
            {
                case 1: //Grouchy
                    if (crew.type != ProtoCrewMember.KerbalType.Tourist)
                    {
                        msg = string.Format("{0} refuses to work", crew.name);
                        kStat.OldTrait = crew.experienceTrait.Title;
                        crew.type = ProtoCrewMember.KerbalType.Tourist;
                        KerbalRoster.SetExperienceTrait(crew, "Tourist");
                        kStat.IsGrouchy = true;
                        LifeSupportManager.Instance.TrackKerbal(kStat);
                    }
                    break;
                case 2:  //Mutinous
                    {
                        msg = string.Format("{0} has become mutinous", crew.name);
                        kStat.OldTrait = crew.experienceTrait.Title;
                        crew.type = ProtoCrewMember.KerbalType.Tourist;
                        KerbalRoster.SetExperienceTrait(crew, "Tourist");
                        kStat.IsGrouchy = true;
                        LifeSupportManager.Instance.TrackKerbal(kStat);
                        DestroyRandomPart(v);
                    }
                    break;
                case 3: //Return to KSC
                    msg = string.Format("{0} gets fed up and wanders back to the KSC", crew.name);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                    DestroyVessel(v);
                    break;
                case 4: //Despawn
                    msg = string.Format("{0} has gone missing", crew.name);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Missing;
                    DestroyVessel(v);
                    break;
                case 5: //Kill
                    msg = string.Format("{0} has died", crew.name);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                    DestroyVessel(v);
                    break;
            }

            ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void DestroyVessel(Vessel v)
        {
            var _demoParts = new List<Part>();
            var count = v.parts.Count;
            for (int i = 0; i < count; ++i)
            {
                _demoParts.Add(v.parts[i]);
            }

            for (int i = 0; i < count; ++i)
            {
                var p = _demoParts[i];
                p.decouple();
                p.explode();
            }
        }

        private void DestroyRandomPart(Vessel thisVessel)
        {
            Random r = new Random();
            var vlist = GetNearbyVessels(150, false, thisVessel, false);
            var count = vlist.Count;
            for (int i = 0; i < count; ++i)
            {
                var v = vlist[i];
                var idx = r.Next(1, v.parts.Count - 1);
                var p = v.parts[idx];
                if (p.parent != null)
                    p.decouple();
            }
        }

        public static List<Vessel> GetNearbyVessels(int range, bool includeSelf, Vessel thisVessel, bool landedOnly = true)
        {
            try
            {
                var vessels = new List<Vessel>();
                var count = FlightGlobals.Vessels.Count;
                for(int i = 0; i < count; ++i)
                {
                    var v = FlightGlobals.Vessels[i];
                    if (v.mainBody == thisVessel.mainBody
                        && (v.Landed || !landedOnly || v == thisVessel))
                    {
                        if (v == thisVessel && !includeSelf) continue;
                        var posCur = thisVessel.GetWorldPos3D();
                        var posNext = v.GetWorldPos3D();
                        var distance = Vector3d.Distance(posCur, posNext);
                        if (distance < range)
                        {
                            vessels.Add(v);
                        }
                    }
                }
                return vessels;
            }
            catch (Exception ex)
            {
                Debug.Log(String.Format("[LS] - ERROR in GetNearbyVessels - {0}", ex.Message));
                return new List<Vessel>();
            }
        }

        private List<LifeSupportVesselDisplayStat> UpdateGUIStats()
        {
            var secondsPerDay = GameSettings.KERBIN_TIME ? (21600) : (86400);
            var useHabPenalties = (LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffectVets +
                                   LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffect > 0);
            LifeSupportManager.Instance.UpdateVesselStats();

            var statList = new List<LifeSupportVesselDisplayStat>();

            var gvCount = FlightGlobals.Vessels.Count;
            for (int i = 0; i < gvCount; ++i)
            {
                var vsl = FlightGlobals.Vessels[i];
                if (vsl.isEVA)
                {

                    var lblColor = "FFD966";
                    var vstat = new LifeSupportVesselDisplayStat();
                    vstat.VesselName = String.Format("<color=#{0}>{1}</color>", lblColor, vsl.vesselName);
                    vstat.LastUpdate = vsl.missionTime;
                    var sitString = "(EVA)";

                    var remEVATime = LifeSupportScenario.Instance.settings.GetSettings().EVATime - vsl.missionTime;
                    var timeString = LifeSupportUtilities.SecondsToKerbinTime(Math.Max(0, remEVATime));

                    if (remEVATime > 0)
                    {
                        vstat.SummaryLabel = String.Format(
                            "<color=#3DB1FF>{0}/{1} - </color><color=#9EE4FF>{2}</color><color=#3DB1FF> time remaining</color>"
                            , vsl.mainBody.bodyName
                            , sitString
                            , timeString.Substring(timeString.IndexOf(':') + 1));
                    }
                    else
                    {
                        vstat.SummaryLabel = "<color=#FF8585>EVA Time Expired</color>";
                    }

                    vstat.crew = new List<LifeSupportCrewDisplayStat>();
                    statList.Add(vstat);
                }
            }

            var vesselList = new List<VesselSupplyStatus>();
            vesselList.AddRange(LifeSupportManager.Instance.VesselSupplyInfo);

            var vlCount = vesselList.Count;
            for(int i = 0; i < vlCount; ++i)
            {
                var vsl = vesselList[i];
                var vstat = new LifeSupportVesselDisplayStat();
                var vCount = FlightGlobals.Vessels.Count;
                Vessel thisVessel = null;
                for (int x = 0; x < vCount; ++x)
                {
                    var tmpV = FlightGlobals.Vessels[x];
                    if (tmpV.id.ToString() == vsl.VesselId)
                    {
                        thisVessel = tmpV;
                        break;
                    }
                }
                double supmult = LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount * Convert.ToDouble(vsl.NumCrew) * vsl.RecyclerMultiplier;
				var supPerDay = secondsPerDay*supmult;
                var estFood = supmult*(Planetarium.GetUniversalTime() - vsl.LastFeeding);

                double ecmult = LifeSupportScenario.Instance.settings.GetSettings().ECAmount * Convert.ToDouble(vsl.NumCrew);
                var ecPerDay = secondsPerDay* ecmult;
                var estEC = ecmult * (Planetarium.GetUniversalTime() - vsl.LastECCheck);

                int numSharedHabVessels = 0;
                var habTime = LifeSupportManager.GetTotalHabTime(vsl, thisVessel, out numSharedHabVessels);

                var supAmount = GetResourceInVessel(thisVessel,"Supplies");
                var calcSup = supAmount;
                if (supAmount == 0)
                {
                    supAmount = Math.Max(0, (vsl.SuppliesLeft * supmult) - estFood);
                    calcSup = (vsl.SuppliesLeft*supmult) - estFood;
                }

                var ecAmount = GetResourceInVessel(thisVessel,"ElectricCharge");
                if (ecAmount == 0)
                    ecAmount = Math.Max(0, (vsl.ECLeft * ecmult) - estEC);

                bool isOldData = Planetarium.GetUniversalTime() - vsl.LastUpdate > 2;
                var lblColor = isOldData ? "C4C4C4" : "ACFF40";

                vstat.VesselName = String.Format("<color=#{0}>{1}</color>", lblColor, vsl.VesselName);
                vstat.VesselId = vsl.VesselId;
                vstat.LastUpdate = vsl.LastUpdate;
                var sitString = "Orbiting";


                thisVessel.checkSplashed();
                if (thisVessel.Splashed)
                    sitString = "Splashed";
                thisVessel.checkLanded();
                if (thisVessel.Landed)
                    sitString = "Landed";

                var habString = "indefinite";
                if (useHabPenalties)
                {
                    habString = LifeSupportUtilities.SecondsToKerbinTime(habTime, true);
                }
                vstat.SummaryLabel = String.Format(
                    "<color=#3DB1FF>{0}/{1} - </color><color=#9EE4FF>{2:0}</color><color=#3DB1FF> supplies (</color><color=#9EE4FF>{3:0.0}</color><color=#3DB1FF>/day) hab for </color><color=#9EE4FF>{4}</color>"                               
                    ,thisVessel.mainBody.bodyName
                    ,sitString
                    , supAmount
                    , supPerDay
                    , habString);
                vstat.crew = new List<LifeSupportCrewDisplayStat>();
                if (useHabPenalties)
                {
                    vstat.SummaryLabel += String.Format(
                        "<color=#3DB1FF> (</color><color=#9EE4FF>{0}</color><color=#3DB1FF> {1} shared within </color><color=#9EE4FF>{2}</color><color=#3DB1FF>m)</color>",
                        isOldData ? "?" : numSharedHabVessels.ToString(),
                        numSharedHabVessels == 1 ? "vessel" : "vessels",
                        LifeSupportScenario.Instance.settings.GetSettings().HabRange);
                }

                var cCount = thisVessel.GetVesselCrew().Count;
                for(int q = 0; q < cCount; ++q)
                {
                    var c = thisVessel.GetVesselCrew()[q];
                    var cStat = new LifeSupportCrewDisplayStat();
                    var cls = LifeSupportManager.Instance.FetchKerbal(c);
                    //Guard clause in case we just changed vessels
                    if (cls.CurrentVesselId != thisVessel.id.ToString()
                        && cls.PreviousVesselId != thisVessel.id.ToString())
                    {
                        cls.PreviousVesselId = cls.CurrentVesselId;
                        cls.CurrentVesselId = thisVessel.id.ToString();
                        cls.TimeEnteredVessel = Planetarium.GetUniversalTime();
                        LifeSupportManager.Instance.TrackKerbal(cls);
                    }

                    cStat.CrewName = String.Format("<color=#FFFFFF>{0} ({1})</color>", c.name,c.experienceTrait.Title.Substring(0,1));

                    var ecLeft = (ecAmount / ecPerDay * secondsPerDay) + LifeSupportScenario.Instance.settings.GetSettings().ECTime;
                    if (ecAmount <= LifeSupportScenario.Instance.settings.GetSettings().ECAmount && !LifeSupportManager.IsOnKerbin(thisVessel))
                    {
                       ecLeft = cls.LastEC - Planetarium.GetUniversalTime() + LifeSupportScenario.Instance.settings.GetSettings().ECTime;
                    }
                    var lblECTime = LifeSupportUtilities.SecondsToKerbinTime(ecLeft);

                    var lblEC = "6FFF00";
                    if (ecLeft < LifeSupportScenario.Instance.settings.GetSettings().ECTime)
                    {
                        lblEC = "FFE100";
                    }
                    if (ecLeft < LifeSupportScenario.Instance.settings.GetSettings().ECTime / 2)
                    {
                        lblEC = "FFAE00";
                    }
                    if (ecLeft <= ResourceUtilities.FLOAT_TOLERANCE)
                    {
                        lblEC = "FF5E5E";
                        lblECTime = "expired";
                    }
                    cStat.ECLabel = String.Format("<color=#{0}>{1}</color>",lblEC,lblECTime);


                    var snacksLeft = (calcSup/supPerDay*secondsPerDay) + LifeSupportScenario.Instance.settings.GetSettings().SupplyTime;
                    var lblSupTime = LifeSupportUtilities.SecondsToKerbinTime(snacksLeft);

                    var lblSup = "6FFF00";
                    if (snacksLeft < LifeSupportScenario.Instance.settings.GetSettings().SupplyTime)
                    {
                        lblSup = "FFE100";
                    }
                    if (snacksLeft < LifeSupportScenario.Instance.settings.GetSettings().SupplyTime / 2)
                    {
                        lblSup = "FFAE00";
                    }
                    if (snacksLeft <= ResourceUtilities.FLOAT_TOLERANCE)
                    {
                        lblSup = "FF5E5E";
                        lblSupTime = "expired";
                    }
                    cStat.SupplyLabel = String.Format("<color=#{0}>{1}</color>", lblSup, lblSupTime);


                    var habTimeLeft = habTime - (Planetarium.GetUniversalTime() - cls.TimeEnteredVessel);
                    var homeTimeLeft = cls.MaxOffKerbinTime - Planetarium.GetUniversalTime();

                    var crewHabString = "indefinite";
                    var crewHomeString = "indefinite";
                    var lblHab = "6FFF00";
                    var lblHome = "6FFF00";

                    if (useHabPenalties)
                    {
                        crewHomeString = LifeSupportUtilities.SecondsToKerbinTime(homeTimeLeft);
                        crewHabString = LifeSupportUtilities.SecondsToKerbinTime(habTimeLeft);
                    }

                    

                    if (habTimeLeft < secondsPerDay * 30) 
                    {
                        lblHab = "FFE100";
                    }
                    if (habTimeLeft < secondsPerDay * 15)
                    {
                        lblHab = "FFAE00";
                    }
                    if (habTimeLeft < 0)
                    {
                        lblHab = "FF5E5E";
                        crewHabString = "expired";
                    }
                    cStat.HabLabel = String.Format("<color=#{0}>{1}</color>", lblHab, crewHabString);

                    if (homeTimeLeft < secondsPerDay * 30) //15 days
                    {
                        lblHome = "FFE100";
                    }
                    if (homeTimeLeft < secondsPerDay * 15)
                    {
                        lblHome = "FFAE00";
                    }
                    if (homeTimeLeft < 0)
                    {
                        lblHome = "FF5E5E";
                        crewHomeString = "expired";
                    }
                    cStat.HomeLabel = String.Format("<color=#{0}>{1}</color>", lblHome, crewHomeString);

                    LifeSupportManager.Instance.TrackKerbal(cls);
                    vstat.crew.Add(cStat);
                }
                statList.Add(vstat);
            }

            return statList;
        }

        private List<LifeSupportVesselDisplayStat> _guiStats; 

        private void GenerateWindow()
        {
            if (Planetarium.GetUniversalTime() > _lastGUIUpdate + _guiCheckInterval)
            {
                _lastGUIUpdate = Planetarium.GetUniversalTime();
                _guiStats = UpdateGUIStats();
                CheckEVAKerbals();
            }

            GUILayout.BeginVertical();
            scrollPos = GUILayout.BeginScrollView(scrollPos, _scrollStyle, GUILayout.Width(800), GUILayout.Height(350));
            GUILayout.BeginVertical();


            try
            {
                foreach (var v in _guiStats.OrderByDescending(s => (int)(s.LastUpdate / 10) + " " + s.VesselId))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("", _labelStyle, GUILayout.Width(10));
                    GUILayout.Label(v.VesselName, _labelStyle, GUILayout.Width(155));
                    GUILayout.Label(v.SummaryLabel, _labelStyle, GUILayout.Width(570));
                    GUILayout.EndHorizontal();

                    var cCount = v.crew.Count;
                    for(int i = 0; i < cCount; ++i)
                    {
                        var c = v.crew[i];
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("", _labelStyle, GUILayout.Width(30));
                        GUILayout.Label(c.CrewName, _labelStyle, GUILayout.Width(135));
                        GUILayout.Label("<color=#EDEDED>sup:</color>", _labelStyle, GUILayout.Width(35));
                        GUILayout.Label(c.SupplyLabel, _labelStyle, GUILayout.Width(145));
                        GUILayout.Label("<color=#EDEDED>EC:</color>", _labelStyle, GUILayout.Width(35));
                        GUILayout.Label(c.ECLabel, _labelStyle, GUILayout.Width(145));
                        GUILayout.Label("<color=#EDEDED>hab:</color>", _labelStyle, GUILayout.Width(40));
                        GUILayout.Label(c.HabLabel, _labelStyle, GUILayout.Width(145));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("", _labelStyle, GUILayout.Width(30));
                        GUILayout.Label("", GUILayout.Width(135));
                        GUILayout.Label("", _labelStyle, GUILayout.Width(35));
                        GUILayout.Label("", _labelStyle, GUILayout.Width(145));
                        GUILayout.Label("", _labelStyle, GUILayout.Width(35));
                        GUILayout.Label("", _labelStyle, GUILayout.Width(145));

                        GUILayout.Label("<color=#EDEDED>home:</color>", _labelStyle, GUILayout.Width(40));
                        GUILayout.Label(c.HomeLabel, _labelStyle, GUILayout.Width(145));
                        GUILayout.EndHorizontal();

                    }
                }

            }
            catch (Exception ex)
            {
                Debug.Log(ex.StackTrace);
            }
            finally
            {
                GUILayout.EndVertical();
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUI.DragWindow();
            }
        }

        private double GetResourceInVessel(Vessel thisVessel, string resName)
        {
            if (thisVessel == null)
                return 0d;

            var supAmount = 0d;
            var count = thisVessel.parts.Count;
            for (int i = 0; i < count; ++i)
            {
                var p = thisVessel.parts[i];
                if (!p.Resources.Contains("resName")) 
                    continue;
                var res = p.Resources["resName"];
                supAmount += res.amount;
            }
            return supAmount;
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
            _windowStyle.fixedWidth = 820f;
            _windowStyle.fixedHeight = 400f;
            _labelStyle = new GUIStyle(HighLogic.Skin.label);
            _buttonStyle = new GUIStyle(HighLogic.Skin.button);
            _scrollStyle = new GUIStyle(HighLogic.Skin.scrollView);
            _hasInitStyles = true;
        }
    }

    public class LifeSupportVesselDisplayStat
    {
        public string VesselName { get; set; }
        public string VesselId { get; set; }
        public string SummaryLabel { get; set; }
        public double LastUpdate { get; set; }

        public List<LifeSupportCrewDisplayStat> crew { get; set; } 
    }

    public class LifeSupportCrewDisplayStat
    {
        public string CrewName { get; set; }
        public string SupplyLabel { get; set; }
        public string ECLabel { get; set; }
        public string HabLabel { get; set; }
        public string HomeLabel { get; set; }
    }

}
