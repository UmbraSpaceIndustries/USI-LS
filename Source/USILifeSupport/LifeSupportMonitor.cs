using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

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
        private ApplicationLauncherButton _lifeSupportMonitorButton;
        private Rect _windowPosition = new Rect(300, 60, 970, 400);
        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _scrollStyle;
        private Vector2 _scrollPos = Vector2.zero;
        private bool _hasInitStyles = false;
        internal static string EcGraceTimeDisplay;
        internal static string SuppliesGraceTimeDisplay;
        public static bool _renderDisplay = false;

        void Awake()
        {
            var texture = new Texture2D(36, 36, TextureFormat.RGBA32, false);
            var textureFile = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "Assets",
                "Supplies.png");
            print("Loading " + textureFile);
            texture.LoadImage(File.ReadAllBytes(textureFile));
            _lifeSupportMonitorButton = ApplicationLauncher.Instance.AddModApplication(GuiOn, GuiOff, null, null, null, null,
                ApplicationLauncher.AppScenes.ALWAYS, texture);
        }

        private void GuiOn()
        {
            _renderDisplay = true;
        }

        public void Start()
        {
            if (!_hasInitStyles)
                InitStyles();
        }

        private void GuiOff()
        {
            _renderDisplay = false;
        }

        private void OnGUI()
        {
            if (!_renderDisplay)
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

        private LifeSupportVesselDisplayStat GetEvaKerbalStats(Vessel kerbal)
        {
            var lblColor = "FFD966";
            var vstat = new LifeSupportVesselDisplayStat();
            vstat.VesselName = String.Format("<color=#{0}>{1}</color>", lblColor, kerbal.vesselName);
            vstat.LastUpdate = kerbal.missionTime;
            var sitString = "(EVA)";

            var evaKerbal = kerbal.GetVesselCrew()[0];
            var kerbalStatus = LifeSupportManager.Instance.FetchKerbal(evaKerbal);
            var cabinTime = kerbalStatus.RemainingCabinTime;
            var cabinTimeString = LifeSupportUtilities.SmartDurationDisplay(Math.Max(0, cabinTime));

            var remEVATime = LifeSupportScenario.Instance.settings.GetSettings().EVATime - kerbal.missionTime;
            var timeString = LifeSupportUtilities.SmartDurationDisplay(Math.Max(0, remEVATime));

            var secondsPerDay = LifeSupportUtilities.SecondsPerDay();
            

            var lblCabin = "6FFF00";
            if (cabinTime < secondsPerDay * 3)
            {
                lblCabin = "FFE100";
            }
            if (cabinTime < secondsPerDay * 1)
            {
                lblCabin = "FFAE00";
            }

            if (remEVATime > 0)
            {
                vstat.SummaryLabel = String.Format(
                    "<color=#3DB1FF>{0}/{1} - </color><color=#9EE4FF>{2}</color><color=#3DB1FF> time remaining   -   </color>" +
                    "<color=#{3}>{4}</color><color=#3DB1FF> cabin time</color>"
                    , kerbal.mainBody.bodyName
                    , sitString
                    , timeString
                    , lblCabin
                    , cabinTimeString);
            }
            else
            {
                vstat.SummaryLabel = "<color=#FF8585>EVA Time Expired</color>";
            }

            vstat.crew = new List<LifeSupportCrewDisplayStat>();
            return vstat;
        }

        private LifeSupportVesselDisplayStat GetVesselStats(VesselSupplyStatus vsl)
        {
            var secondsPerDay = LifeSupportUtilities.SecondsPerDay();
            var useHabPenalties = (LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffectVets +
                                   LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffect > 0);

            Vessel thisVessel = FlightGlobals.Vessels.Find(v => v.id.ToString() == vsl.VesselId);
            var vstat = new LifeSupportVesselDisplayStat();
            bool isOldData = Planetarium.GetUniversalTime() - vsl.LastUpdate > 2;
            var lblColor = isOldData ? "C4C4C4" : "ACFF40";
            vstat.VesselName = String.Format("<color=#{0}>{1}</color>", lblColor, vsl.VesselName);
            vstat.VesselId = vsl.VesselId;
            vstat.LastUpdate = vsl.LastUpdate;
            var situationString = GetSituationString(thisVessel);

            double suppliesPerSecond = LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount * Convert.ToDouble(vsl.NumCrew) * vsl.RecyclerMultiplier;
            var suppliesPerDay = secondsPerDay * suppliesPerSecond;
            var supAmount = GetResourceInVessel(thisVessel, "Supplies");
            var suppliesTimeLeft = (suppliesPerSecond > ResourceUtilities.FLOAT_TOLERANCE) ? (supAmount / suppliesPerSecond) : 0;
            if (supAmount == 0)
            {
                var suppliesConsumedSinceLastCheck = suppliesPerSecond * (Planetarium.GetUniversalTime() - vsl.LastFeeding);
                supAmount = Math.Max(0, (vsl.SuppliesLeft * suppliesPerSecond) - suppliesConsumedSinceLastCheck);
                suppliesTimeLeft = (vsl.SuppliesLeft - (Planetarium.GetUniversalTime() - vsl.LastFeeding));
            }

            double ecPerSecond = LifeSupportScenario.Instance.settings.GetSettings().ECAmount * Convert.ToDouble(vsl.NumCrew);
            var ecAmount = GetResourceInVessel(thisVessel, "ElectricCharge");
            var ecTimeLeft = (ecPerSecond > ResourceUtilities.FLOAT_TOLERANCE) ? (ecAmount / ecPerSecond) : 0;
            if (ecAmount == 0)
            {
                var ecConsumedSinceLastCheck = ecPerSecond * (Planetarium.GetUniversalTime () - vsl.LastECCheck);
                ecAmount = Math.Max (0, (vsl.ECLeft * ecPerSecond) - ecConsumedSinceLastCheck);
                ecTimeLeft = (vsl.ECLeft - (Planetarium.GetUniversalTime() - vsl.LastECCheck));
            }

            int numSharedHabVessels = 0;
            var habTime = LifeSupportManager.GetTotalHabTime(vsl, thisVessel, out numSharedHabVessels);

            var habString = "indefinite";
            if (useHabPenalties)
            {
                habString = LifeSupportUtilities.DurationDisplay(habTime, LifeSupportUtilities.TimeFormatLength.Short);
            }
            vstat.SummaryLabel = String.Format(
                "<color=#3DB1FF>{0}/{1} - </color><color=#9EE4FF>{2:0}</color><color=#3DB1FF> supplies (</color><color=#9EE4FF>{3:0.0}</color><color=#3DB1FF>/day) hab for </color><color=#9EE4FF>{4}</color>"                               
                , thisVessel.mainBody.bodyName
                , situationString
                , supAmount
                , suppliesPerDay
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

            var numCrew = vsl.NumCrew;
            var crewCap = vsl.CrewCap;
            var vesselHabMultiplier = vsl.VesselHabMultiplier;
            foreach (var c in thisVessel.GetVesselCrew())
            {
                var crewStat = GetCrewStat(c, thisVessel, suppliesTimeLeft, ecTimeLeft, ecAmount, habTime, numCrew, crewCap, vesselHabMultiplier);
                vstat.crew.Add(crewStat);
            }
            vstat.crew = vstat.crew.OrderBy(crewStat => crewStat.EarliestExpiration).ToList();
            if (vstat.crew.Any())
            {
                vstat.EarliestExpiration = vstat.crew.First().EarliestExpiration;
            }
            return vstat;
        }

        private IEnumerable<LifeSupportVesselDisplayStat> UpdateGUIStats()
        {
            LifeSupportManager.Instance.UpdateVesselStats();

            var statList = new List<LifeSupportVesselDisplayStat>();

            foreach (var vessel in FlightGlobals.Vessels)
            {
                if (vessel.isEVA)
                {
                    statList.Add(GetEvaKerbalStats(vessel));
                }
            }

            foreach(var trackedVessel in LifeSupportManager.Instance.VesselSupplyInfo)
            {
                statList.Add(GetVesselStats(trackedVessel));
            }
            var activeVesselId = FlightGlobals.ActiveVessel ? FlightGlobals.ActiveVessel.id.ToString() : "";
            return statList.OrderByDescending(s => s.VesselId == activeVesselId).ThenBy(s => (int)(s.EarliestExpiration));
        }

        private IEnumerable<LifeSupportVesselDisplayStat> _guiStats; 

        private void GenerateWindow()
        {
            if (Planetarium.GetUniversalTime() > _lastGUIUpdate + _guiCheckInterval)
            {
                _lastGUIUpdate = Planetarium.GetUniversalTime();
                EcGraceTimeDisplay = LifeSupportUtilities.CompactDurationDisplay(LifeSupportScenario.Instance.settings.GetSettings().ECTime);
                SuppliesGraceTimeDisplay = LifeSupportUtilities.CompactDurationDisplay(LifeSupportScenario.Instance.settings.GetSettings().SupplyTime);
                _guiStats = UpdateGUIStats();
            }

            GUILayout.BeginVertical();
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, _scrollStyle, GUILayout.Width(950), GUILayout.Height(350));
            GUILayout.BeginVertical();
            try
            {
                foreach (var v in _guiStats)
                {
                    DisplayVesselStats(v);
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

        private void DisplayVesselStats(LifeSupportVesselDisplayStat v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("", _labelStyle, GUILayout.Width(10));
            GUILayout.Label(v.VesselName, _labelStyle, GUILayout.Width(155));
            GUILayout.Label(v.SummaryLabel, _labelStyle, GUILayout.Width(570));
            GUILayout.EndHorizontal();
            foreach (var c in v.crew)
            {
                DisplayCrewStats(c);
            }
        }

        private void DisplayCrewStats(LifeSupportCrewDisplayStat c)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("", _labelStyle, GUILayout.Width(30));
            GUILayout.Label(c.CrewName, _labelStyle, GUILayout.Width(135));
            GUILayout.Label("<color=#EDEDED>sup:</color>", _labelStyle, GUILayout.Width(35));
            GUILayout.Label(c.SupplyLabel, _labelStyle, GUILayout.Width(145));
            GUILayout.Label("<color=#EDEDED>EC:</color>", _labelStyle, GUILayout.Width(35));
            GUILayout.Label(c.ECLabel, _labelStyle, GUILayout.Width(145));
            GUILayout.Label("<color=#EDEDED>hab:</color>", _labelStyle, GUILayout.Width(40));
            GUILayout.Label(c.HabLabel, _labelStyle, GUILayout.Width(145));
            GUILayout.Label("<color=#EDEDED>cabin:</color>", _labelStyle, GUILayout.Width(40));
            GUILayout.Label(c.CabinLabel, _labelStyle, GUILayout.Width(145));
            GUILayout.EndHorizontal();
        }

        private double GetResourceInVessel(Vessel vessel, string resName)
        {
            if (vessel == null)
                return 0d;

            var amount = 0d;
            var count = vessel.parts.Count;
            for (int i = 0; i < count; ++i)
            {
                var p = vessel.parts[i];
                if (!p.Resources.Contains(resName))
                    continue;
                var res = p.Resources[resName];
                if(res.flowState)
                    amount += res.amount;
            }
            return amount;
        }

        private string GetSituationString(Vessel vessel)
        {
            var sitString = "Orbiting";
            vessel.checkSplashed();
            if (vessel.Splashed)
                sitString = "Splashed";
            vessel.checkLanded();
            if (vessel.Landed)
                sitString = "Landed";
            return sitString;
        }

        private LifeSupportCrewDisplayStat GetCrewStat(ProtoCrewMember c, Vessel vessel, double vesselSuppliesTimeLeft, double vesselEcTimeLeft, double vesselEcAmount, double vesselHabTime, int numCrew, int crewCap, double vesselHabMultiplier)
        {
            var cls = LifeSupportManager.Instance.FetchKerbal(c);
            //Guard clause in case we just changed vessels
            if (cls.CurrentVesselId != vessel.id.ToString()
                && cls.PreviousVesselId != vessel.id.ToString())
            {
                cls.PreviousVesselId = cls.CurrentVesselId;
                cls.CurrentVesselId = vessel.id.ToString();
                LifeSupportManager.Instance.TrackKerbal(cls);
            }

            // not sure this is correct or needed
            var ecTimeLeft = vesselEcTimeLeft;
            if (vesselEcAmount <= LifeSupportScenario.Instance.settings.GetSettings().ECAmount && !LifeSupportManager.IsOnKerbin(vessel))
            {
                ecTimeLeft = cls.LastEC - Planetarium.GetUniversalTime ();
            }

            var cStat = new LifeSupportCrewDisplayStat();
            cStat.CrewName = GetCrewNameLabel(c, cls);
            cStat.ComputeEc(ecTimeLeft, c);
            cStat.ComputeSupply(vesselSuppliesTimeLeft, c);
            cStat.ComputeHab(vesselHabTime, c, cls);
            cStat.ComputeCabin(c, cls, numCrew, crewCap, vesselHabMultiplier);

            LifeSupportManager.Instance.TrackKerbal(cls);
            return cStat;
        }

        private string GetCrewNameLabel(ProtoCrewMember c, LifeSupportStatus cls)
        {
            var traitColor = cls.IsGrouchy ? "#FF0000" : "#FFFFFF";
            var traitLabel = c.experienceTrait.Title.Substring (0, 1); // Could choose to display OldTrait instead
            return String.Format("<color=#FFFFFF>{0}</color> <color={1}>({2})</color>", c.name, traitColor, traitLabel);
        }

        internal void OnDestroy()
        {
            if (_lifeSupportMonitorButton == null)
                return;
            ApplicationLauncher.Instance.RemoveModApplication(_lifeSupportMonitorButton);
            _lifeSupportMonitorButton = null;
        }

        private void InitStyles()
        {
            _windowStyle = new GUIStyle(HighLogic.Skin.window);
            _windowStyle.fixedWidth = 970f;
            _windowStyle.fixedHeight = 400f;
            _labelStyle = new GUIStyle(HighLogic.Skin.label);
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
        public double EarliestExpiration { get; set; }

        public List<LifeSupportCrewDisplayStat> crew { get; set; }

        public LifeSupportVesselDisplayStat()
        {
            EarliestExpiration = double.PositiveInfinity;
        }
    }

    public class LifeSupportCrewDisplayStat
    {
        public string CrewName { get; set; }
        public string ECLabel { get; set; }
        public string SupplyLabel { get; set; }
        public string HabLabel { get; set; }
        public string CabinLabel { get; set; }
        public double EarliestExpiration { get; set; }

        public LifeSupportCrewDisplayStat()
        {
            EarliestExpiration = double.PositiveInfinity;
        }

        private void UpdateEarliestExpiration(double expirationIn)
        {
            var expiration = Planetarium.GetUniversalTime() + expirationIn;
            if (expiration < EarliestExpiration)
            {
                EarliestExpiration = expiration;
            }
        }

        internal void ComputeHab(double vesselHabTime, ProtoCrewMember c, LifeSupportStatus cls)
        {
            var crewHabString = "indefinite";
            var lblHab = "6FFF00";
            var useHabPenalties = LifeSupportManager.GetNoHomeEffect(c.name) > 0;
            if (useHabPenalties)
            {
                UpdateEarliestExpiration(vesselHabTime);
                var habTimeLeft = vesselHabTime - (Planetarium.GetUniversalTime() - cls.LastAtHome);
                var isScout = c.HasEffect("ExplorerSkill") && habTimeLeft >= LifeSupportScenario.Instance.settings.GetSettings().ScoutHabTime;
                var isPermaHab = habTimeLeft >= LifeSupportScenario.Instance.settings.GetSettings().PermaHabTime;

                if(isScout || isPermaHab)
                {
                    crewHabString = "indefinite";
                }
                else if (habTimeLeft < 0)
                {
                    lblHab = "FF5E5E";
                    crewHabString = "expired";
                }
                else
                {
                    crewHabString = LifeSupportUtilities.SmartDurationDisplay(habTimeLeft);
                    var secondsPerDay = LifeSupportUtilities.SecondsPerDay();
                    if (habTimeLeft < secondsPerDay * 30)
                    {
                        lblHab = "FFE100";
                    }
                    if (habTimeLeft < secondsPerDay * 15)
                    {
                        lblHab = "FFAE00";
                    }
                }
            }
            HabLabel = String.Format("<color=#{0}>{1}</color>", lblHab, crewHabString);
        }

        internal void ComputeCabin(ProtoCrewMember c, LifeSupportStatus cls, int numCrew, int crewCap, double vesselHabMultiplier)
        {
            var crewCabinString = "indefinite";
            var lblCabin = "6FFF00";
            var useHabPenalties = LifeSupportManager.GetNoHomeEffect(c.name) > 0;
            if (useHabPenalties)
            {
                var adjustedCabinTimeLeft = cls.RemainingCabinTime * ((double)crewCap / (double)numCrew) * (1 + vesselHabMultiplier);
                UpdateEarliestExpiration(adjustedCabinTimeLeft);

                // Debug.Log(String.Format("{0} has {1} remaining CabinTime", cls.KerbalName, cabinTimeLeft ));

                var isScout = c.HasEffect("ExplorerSkill") && adjustedCabinTimeLeft >= LifeSupportScenario.Instance.settings.GetSettings().ScoutHabTime;
                var isPermaHab = adjustedCabinTimeLeft >= LifeSupportScenario.Instance.settings.GetSettings().PermaHabTime;

                if (isScout || isPermaHab)
                {
                    crewCabinString = "indefinite";
                }
                else if (adjustedCabinTimeLeft < 0)
                {
                    lblCabin = "FF5E5E";
                    crewCabinString = "expired";
                }
                else
                {

                    crewCabinString = LifeSupportUtilities.SmartDurationDisplay(adjustedCabinTimeLeft);
                    var secondsPerDay = LifeSupportUtilities.SecondsPerDay();
                    if (adjustedCabinTimeLeft < secondsPerDay * 3)
                    {
                        lblCabin = "FFE100";
                    }
                    if (adjustedCabinTimeLeft < secondsPerDay * 1)
                    {
                        lblCabin = "FFAE00";
                    }
                }
            }
            CabinLabel = String.Format("<color=#{0}>{1}</color>", lblCabin, crewCabinString);
        }

        private string GetRemainingTimeWithGraceLabel(double timeLeft, double graceTime, string graceTimeDisplay, string inGraceTimeMessage, int effectWhenExpires)
        {
            if (effectWhenExpires == 0)
            {
                return "<color=#6FFF00>indefinite</color>";
            }
            if (timeLeft > 0)
            {
                return String.Format("<color=#6FFF00>{0} (+{1})</color>",
                                     LifeSupportUtilities.SmartDurationDisplay(timeLeft),
                                     graceTimeDisplay);
            }
            else
            {
                var exceededTime = - timeLeft;
                if (exceededTime < graceTime / 2)
                {
                    return String.Format("<color=#FFE100>{0} ({1})</color>",
                                         LifeSupportUtilities.SmartDurationDisplay(graceTime - exceededTime),
                                         inGraceTimeMessage);
                }
                else if (exceededTime < graceTime)
                {
                    return String.Format("<color=#FFAE00>{0} ({1})</color>",
                                         LifeSupportUtilities.SmartDurationDisplay(graceTime - exceededTime),
                                         inGraceTimeMessage);
                }
                else // exceededTime > graceTime
                {
                    return "<color=#FF5E5E>expired</color>";
                }
            }
        }

        internal void ComputeEc(double ecTimeLeft, ProtoCrewMember c)
        {
            var noEcEffect = LifeSupportManager.GetNoECEffect(c.name);
            ECLabel = GetRemainingTimeWithGraceLabel(
                ecTimeLeft,
                LifeSupportScenario.Instance.settings.GetSettings().ECTime,
                LifeSupportMonitor.EcGraceTimeDisplay,
                "out of EC",
                noEcEffect);
        }

        internal void ComputeSupply(double vesselSuppliesTimeLeft, ProtoCrewMember c)
        {
            var noSupplyEffect = LifeSupportManager.GetNoSupplyEffect(c.name);
            if (noSupplyEffect > 0)
            {
                UpdateEarliestExpiration(vesselSuppliesTimeLeft);
            }
            SupplyLabel = GetRemainingTimeWithGraceLabel(
                vesselSuppliesTimeLeft,
                LifeSupportScenario.Instance.settings.GetSettings().SupplyTime,
                LifeSupportMonitor.SuppliesGraceTimeDisplay,
                "starving",
                noSupplyEffect);
        }

    }
}
