using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace LifeSupport 
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class LifeSupportMonitor_Flight : LifeSupportMonitor
    { }

    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class LifeSupportMonitor_SpaceCenter : LifeSupportMonitor
    { }

    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class LifeSupportMonitor_TStation : LifeSupportMonitor
    { }


    public class LifeSupportMonitor : MonoBehaviour
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

        private double _lastGUIUpdate;
        private double _guiCheckInterval = 1d;

        private void CheckEVAKerbals()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            var vList = GetNearbyVessels(2000, false, FlightGlobals.ActiveVessel, false);
            foreach (var v in vList.Where(v => v.isEVA))
            {
                if(v.mainBody == FlightGlobals.GetHomeBody())
                    if (v.altitude < LifeSupportSetup.Instance.LSConfig.HomeWorldAltitude)
                        continue;

                var c = v.GetVesselCrew().First();
                //Check their status.
                var k = LifeSupportManager.Instance.FetchKerbal(c);
                if (v.missionTime > LifeSupportSetup.Instance.LSConfig.EVATime)
                {
                    print("Applying EVA Effect");
                    ApplyEVAEffect(k, c, v,
                        LifeSupportManager.isVet(k.KerbalName)
                            ? LifeSupportSetup.Instance.LSConfig.EVAEffectVets
                            : LifeSupportSetup.Instance.LSConfig.EVAEffect);
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
            foreach (var p in v.parts)
            {
                _demoParts.Add(p);
            }
            foreach (var p in _demoParts)
            {
                p.decouple();
                p.explode();
            }
            //v.DespawnCrew();
            //v.DestroyVesselComponents();
        }

        private void DestroyRandomPart(Vessel thisVessel)
        {
            Random r = new Random();
            var vlist = GetNearbyVessels(150, false, thisVessel, false);
            foreach (var v in vlist)
            {
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
                foreach (var v in FlightGlobals.Vessels.Where(
                    x => x.mainBody == thisVessel.mainBody
                    && (x.Landed || !landedOnly || x == thisVessel)))
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
            var useHabPenalties = (LifeSupportSetup.Instance.LSConfig.NoHomeEffectVets +
                                   LifeSupportSetup.Instance.LSConfig.NoHomeEffect > 0);
            LifeSupportManager.Instance.UpdateVesselStats();

            var statList = new List<LifeSupportVesselDisplayStat>();

            var checkVessels = new List<Guid>();
                foreach (var v in FlightGlobals.Vessels.Where(v => v.isEVA))
                {
                    checkVessels.Add(v.id);
                }


            foreach (var vslId in checkVessels)
            {
                var vsl = FlightGlobals.Vessels.FirstOrDefault(v => v.id == vslId);
                if (vsl == null)
                    continue;

                var lblColor = "FFD966";
                var vstat = new LifeSupportVesselDisplayStat();
                vstat.VesselName = String.Format("<color=#{0}>{1}</color>", lblColor, vsl.vesselName);
                vstat.LastUpdate = vsl.missionTime;
                var sitString = "(EVA)";

                var remEVATime = LifeSupportSetup.Instance.LSConfig.EVATime - vsl.missionTime;
                var timeString = LifeSupportUtilities.SecondsToKerbinTime(Math.Max(0,remEVATime));

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

            var vesselList = new List<VesselSupplyStatus>();
            vesselList.AddRange(LifeSupportManager.Instance.VesselSupplyInfo);


            foreach (var vsl in vesselList)
            {
                var vstat = new LifeSupportVesselDisplayStat();
                Vessel thisVessel = FlightGlobals.Vessels.First(v => v.id.ToString() == vsl.VesselId);
                double supmult = LifeSupportSetup.Instance.LSConfig.SupplyAmount * Convert.ToDouble(vsl.NumCrew) * vsl.RecyclerMultiplier;
                var supPerDay = (21600*supmult);
                var estFood = supmult*(Planetarium.GetUniversalTime() - vsl.LastFeeding);
                var habTime = LifeSupportManager.GetTotalHabTime(vsl);
                var supAmount = GetSuppliesInVessel(thisVessel);
                if(supAmount == 0)
                    supAmount = Math.Max(0, (vsl.SuppliesLeft * supmult) - estFood);

                var lblColor = "ACFF40";
                if (Planetarium.GetUniversalTime() - vsl.LastUpdate > 2)
                    lblColor = "C4C4C4";
                vstat.VesselName = String.Format("<color=#{0}>{1}</color>", lblColor, vsl.VesselName);
                vstat.LastUpdate = vsl.LastUpdate;
                var sitString = "Orbiting";
                if (thisVessel.Splashed || thisVessel.heightFromTerrain < 1000)
                    sitString = "Splashed"; 
                if (thisVessel.Landed || thisVessel.heightFromTerrain < 1000)
                    sitString = "Landed";

                var habString = "indefinite";
                if (useHabPenalties)
                    habString = LifeSupportUtilities.SecondsToKerbinTime(habTime,true);
                vstat.SummaryLabel = String.Format("<color=#3DB1FF>{0}/{1} - </color><color=#9EE4FF>{2:0}</color><color=#3DB1FF> supplies (</color><color=#9EE4FF>{3:0.0}</color><color=#3DB1FF>/day) hab for </color><color=#9EE4FF>{4}</color>"
                    ,thisVessel.mainBody.bodyName
                    ,sitString
                    , supAmount
                    , supPerDay
                    , habString);
                vstat.crew = new List<LifeSupportCrewDisplayStat>();

                foreach (var c in thisVessel.GetVesselCrew())
                {
                    var cStat = new LifeSupportCrewDisplayStat();
                    var cls = LifeSupportManager.Instance.FetchKerbal(c);
                    cStat.CrewName = String.Format("<color=#FFFFFF>{0} ({1})</color>", c.name,c.experienceTrait.Title.Substring(0,1));

                    var snacksLeft = supAmount / supPerDay * 60 * 60 * 6;
                    if (supAmount <= ResourceUtilities.FLOAT_TOLERANCE && !LifeSupportManager.IsOnKerbin(thisVessel))
                    {
                        snacksLeft = cls.LastMeal - Planetarium.GetUniversalTime();
                    }

                    var lblSup = "6FFF00";
                    if (snacksLeft < 60 * 60 * 6 * 15) //15 days
                    {
                        lblSup = "FFE100";
                    }
                    if (snacksLeft < 0)
                    {
                        lblSup = "FFAE00";
                    }
                    if (snacksLeft < -60 * 60 * 6 * 15)
                    {
                        lblSup = "FF5E5E";
                    }
                    cStat.SupplyLabel = String.Format("<color=#{0}>{1}</color>",lblSup,LifeSupportUtilities.SecondsToKerbinTime(snacksLeft));
                    var timeLeft = Math.Min(cls.MaxOffKerbinTime - Planetarium.GetUniversalTime(), habTime - (Planetarium.GetUniversalTime() - cls.TimeEnteredVessel));

                    var lblHab = "6FFF00";
                    if (timeLeft < 60 * 60 * 6 * 15) //15 days
                    {
                        lblHab = "FFE100";
                    }
                    if (timeLeft < 0)
                    {
                        lblHab = "FFAE00";
                    }
                    if (timeLeft < -60 * 60 * 6 * 15)
                    {
                        lblHab = "FF5E5E";
                    }
                    var crewHabString = "indefinite";
                    if (useHabPenalties)
                        crewHabString = LifeSupportUtilities.SecondsToKerbinTime(timeLeft);
                    cStat.HabLabel = String.Format("<color=#{0}>{1}</color>", lblHab, crewHabString);
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
                LifeSupportManager.Instance.UpdateVesselStats();
            }

            GUILayout.BeginVertical();
            scrollPos = GUILayout.BeginScrollView(scrollPos, _scrollStyle, GUILayout.Width(600), GUILayout.Height(350));
            GUILayout.BeginVertical();


            try
            {
                foreach (var v in _guiStats.OrderByDescending(s => (int)s.LastUpdate + " " + s.VesselName))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("", _labelStyle, GUILayout.Width(10));
                    GUILayout.Label(v.VesselName, _labelStyle, GUILayout.Width(155));
                    GUILayout.Label(v.SummaryLabel, _labelStyle, GUILayout.Width(370));
                    GUILayout.EndHorizontal();
                    foreach (var c in v.crew)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("", _labelStyle, GUILayout.Width(30));
                        GUILayout.Label(c.CrewName, _labelStyle, GUILayout.Width(135));
                        GUILayout.Label("<color=#EDEDED>sup:</color>", _labelStyle, GUILayout.Width(32));
                        GUILayout.Label(c.SupplyLabel, _labelStyle, GUILayout.Width(155));
                        GUILayout.Label("<color=#EDEDED>hab:</color>", _labelStyle, GUILayout.Width(32));
                        GUILayout.Label(c.HabLabel, _labelStyle, GUILayout.Width(155));
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

        private double GetSuppliesInVessel(Vessel thisVessel)
        {
            if (thisVessel == null)
                return 0d;

            var supAmount = 0d;

            foreach (var p in thisVessel.parts)
            {
                if (!p.Resources.Contains("Supplies")) 
                    continue;
                var res = p.Resources["Supplies"];
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
            _windowStyle.fixedWidth = 620f;
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
        public string SummaryLabel { get; set; }
        public double LastUpdate { get; set; }

        public List<LifeSupportCrewDisplayStat> crew { get; set; } 
    }

    public class LifeSupportCrewDisplayStat
    {
        public string CrewName { get; set; }
        public string SupplyLabel { get; set; }
        public string HabLabel { get; set; }
    }

}
