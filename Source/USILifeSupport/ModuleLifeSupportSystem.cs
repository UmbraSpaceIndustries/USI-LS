using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeSupport
{
    public class ModuleLifeSupportSystem : VesselModule
    {
        [KSPField(isPersistant = true)]
        public double LastUpdateTime;

        private readonly double _checkInterval = 1d;
        private double _lastProcessingTime;
        private double _lastUpdate;
        private bool _isDirty = true;
        private double _oldHabChecksum;
        private int _currentCrewCount;
        private Part _crewPart;
        private int _partCount;
        bool _isStatusRefreshRequired = false;

        protected ResourceConverter _resourceConverter;
        protected IResourceBroker _resourceBroker;
        private VesselSupplyStatus _vesselStatus;

        public ResourceConverter Converter
        {
            get { return _resourceConverter ?? (_resourceConverter = new ResourceConverter(ResourceBroker)); }
        }

        public IResourceBroker ResourceBroker
        {
            get { return _resourceBroker ?? (_resourceBroker = new ResourceBroker()); }
        }

        public VesselSupplyStatus VesselStatus
        {
            get { return _vesselStatus ?? (_vesselStatus = SetupVesselStatus()); }
            set { _vesselStatus = value; }
        }

        private ConversionRecipe SupplyRecipe
        {
            get { return GenerateSupplyRecipe(); }
        }

        private ConversionRecipe ECRecipe
        {
            get  { return GenerateECRecipe(); }
        }

        private VesselSupplyStatus SetupVesselStatus()
        {
            UpdateVesselInfo();

            var id = base.vessel.id.ToString();
            var vessel = LifeSupportManager.Instance.FetchVessel(id);
            LifeSupportManager.Instance.TrackVessel(vessel);

            return vessel;
        }

        private ConversionRecipe GenerateSupplyRecipe()
        {
            var crewCount = _currentCrewCount;
            var recyclerMultiplier = VesselStatus.RecyclerMultiplier;
            var suppliesConsumption = LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount;
            var wasteOutput = LifeSupportScenario.Instance.settings.GetSettings().WasteAmount;

            var supRatio = suppliesConsumption * crewCount * recyclerMultiplier;
            var mulchRatio = wasteOutput * crewCount * recyclerMultiplier;

            var recipe = new ConversionRecipe();
            recipe.Inputs.Add(new ResourceRatio
            {
                FlowMode = ResourceFlowMode.ALL_VESSEL,
                Ratio = supRatio,
                ResourceName = "Supplies",
                DumpExcess = true
            });
            recipe.Outputs.Add(new ResourceRatio
            {
                FlowMode = ResourceFlowMode.ALL_VESSEL,
                Ratio = mulchRatio,
                ResourceName = "Mulch",
                DumpExcess = true
            });

            return recipe;
        }

        private ConversionRecipe GenerateECRecipe()
        {
            var crewCount = _currentCrewCount;
            var electricityConsumption = LifeSupportScenario.Instance.settings.GetSettings().ECAmount;

            var recipe = new ConversionRecipe();
            recipe.Inputs.Add(new ResourceRatio
            {
                FlowMode = ResourceFlowMode.ALL_VESSEL,
                Ratio = electricityConsumption * crewCount,
                ResourceName = "ElectricCharge",
                DumpExcess = true
            });

            return recipe;
        }

        public override void OnLoadVessel()
        {
            GameEvents.onVesselPartCountChanged.Add(SetVesselDirty);
            GameEvents.onVesselCrewWasModified.Add(SetVesselDirty);
            GameEvents.onVesselChange.Add(SetVesselDirty);
        }

        private void SetVesselDirty(Vessel v)
        {
            _isDirty = true;
        }

        public void OnDestroy()
        {
            GameEvents.onVesselPartCountChanged.Remove(SetVesselDirty);
            GameEvents.onVesselCrewWasModified.Remove(SetVesselDirty);
            GameEvents.onVesselChange.Remove(SetVesselDirty);
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null || !vessel.loaded)
                return;

            if (vessel.isEVA)
            {
                CheckEVA(vessel);
                return;
            }

            if (_partCount != vessel.parts.Count)
            {
                if (_partCount > 0)
                {
                    _isStatusRefreshRequired = true;
                }
                _partCount = vessel.parts.Count;
            }

            if (_isDirty)
            {
                _isDirty = false;
                UpdateVesselInfo();
                UpdateStatus();
            }

            var now = Planetarium.GetUniversalTime();
            if (_currentCrewCount == 0)
            {
                VesselStatus.VesselName = vessel.vesselName;
                VesselStatus.NumCrew = vessel.GetCrewCount();
                VesselStatus.CrewCap = vessel.GetCrewCapacity();
                VesselStatus.LastECCheck = now;
                VesselStatus.LastFeeding = now;
                VesselStatus.LastUpdate = now;

                LifeSupportManager.Instance.TrackVessel(VesselStatus);
                LastUpdateTime = now;

                return;
            }

            try
            {
                bool isLongLoop = false;
                var offKerbin = !LifeSupportManager.IsOnKerbin(vessel);
                CheckVesselId();

                // Check our time
                double deltaTime = GetDeltaTime();
                bool isCatchup = deltaTime / 2 > TimeWarp.fixedDeltaTime;

                if (deltaTime < ResourceUtilities.FLOAT_TOLERANCE * 10)
                    return;

                if (now >= _lastProcessingTime + _checkInterval)
                {
                    isLongLoop = true;
                    _lastProcessingTime = now;
                }

                VesselStatus.LastUpdate = now;
                VesselStatus.VesselName = vessel.vesselName;
                VesselStatus.NumCrew = vessel.GetCrewCount();
                VesselStatus.CrewCap = vessel.GetCrewCapacity();

                if (isLongLoop)
                {
                    CheckForDeadKerbals();
                }

                if (_currentCrewCount > 0)
                {
                    //Guard clause
                    if (_crewPart == null)
                        UpdateVesselInfo();

                    //we will add a bit of a fudge factor for supplies
                    var tolerance = deltaTime / 2f;

                    //nom nom nom!
                    ConverterResults resultSupply = Converter.ProcessRecipe(deltaTime, SupplyRecipe, _crewPart, null, 1f);
                    ConverterResults resultEC = Converter.ProcessRecipe(deltaTime, ECRecipe, _crewPart, null, 1f);

                    #region Long Loop - Crew
                    if (isLongLoop)
                    {
                        //Ensure status is current
                        UpdateStatus();

                        var habTime = LifeSupportManager.GetTotalHabTime(VesselStatus, vessel);

                        if (_oldHabChecksum < ResourceUtilities.FLOAT_TOLERANCE)
                            _oldHabChecksum = LifeSupportManager.GetHabChecksum(VesselStatus, vessel);

                        var newHabChecksum = LifeSupportManager.GetHabChecksum(VesselStatus, vessel);
                        if (Math.Abs(_oldHabChecksum - newHabChecksum) > ResourceUtilities.FLOAT_TOLERANCE)
                        {
                            Debug.Log("[USI-LS] Vessel situation changed, refreshing life support");
                            _isStatusRefreshRequired = true;
                            _oldHabChecksum = newHabChecksum;
                        }

                        var crewRoster = vessel.GetVesselCrew();
                        var count = crewRoster.Count;
                        for (int i = 0; i < count; ++i)
                        {
                            var crewMember = crewRoster[i];
                            bool isGrouchyHab = false;
                            bool isGrouchySupplies = false;
                            bool isGrouchyEC = false;
                            bool isScout = crewMember.HasEffect("ExplorerSkill") && habTime >= LifeSupportScenario.Instance.settings.GetSettings().ScoutHabTime;
                            bool isPermaHab = habTime >= LifeSupportScenario.Instance.settings.GetSettings().PermaHabTime;
                            bool isHomeWorld = CheckIfHomeWorld() && habTime >= LifeSupportScenario.Instance.settings.GetSettings().ScoutHabTime && vessel.LandedOrSplashed;

                            // Get the crew member's life support stats
                            var trackedKerbal = LifeSupportManager.Instance.FetchKerbal(crewMember);

                            // Update life support stats
                            if (_isStatusRefreshRequired)
                            {
                                LifeSupportManager.Instance.TrackKerbal(trackedKerbal);
                            }

                            // Update Hab effects

                            // I guess if you've colonized a planet, Kerbals starting there should get cabin time refills?
                            if (!offKerbin || isHomeWorld)
                            {
                                trackedKerbal.RemainingCabinTime = LifeSupportScenario.Instance.settings.GetSettings().BaseHabTime
                                    * LifeSupportUtilities.SecondsPerMonth();
                            }
                            if (!offKerbin || isScout || isHomeWorld || isPermaHab)
                            {
                                trackedKerbal.LastAtHome = now;
                            }
                            else
                            {
                                if (vessel.id.ToString() != trackedKerbal.CurrentVesselId)
                                {
                                    trackedKerbal.PreviousVesselId = trackedKerbal.CurrentVesselId;
                                    trackedKerbal.CurrentVesselId = vessel.id.ToString();
                                    LifeSupportManager.Instance.TrackKerbal(trackedKerbal);
                                }

                                // Handle cabin time consumption or regeneration...

                                var timeSinceUpdate = now - trackedKerbal.LastUpdate;
                                var timeSpentUnderHab = Math.Min(timeSinceUpdate,
                                    Math.Max(0, habTime - (trackedKerbal.LastUpdate - trackedKerbal.LastAtHome)));

                                // First regenerate based on the amount of hab time we had left.
                                if (timeSpentUnderHab > 0)
                                {
                                    // Regenerate
                                    trackedKerbal.RemainingCabinTime += (timeSpentUnderHab) * 
                                        LifeSupportScenario.Instance.settings.GetSettings().RecoverySpeed;
                                    // Now cap it before trying to remove any
                                    if (trackedKerbal.RemainingCabinTime > LifeSupportScenario.Instance.settings.GetSettings().BaseHabTime
                                        * LifeSupportUtilities.SecondsPerMonth())
                                    {
                                        trackedKerbal.RemainingCabinTime = LifeSupportScenario.Instance.settings.GetSettings().BaseHabTime
                                        * LifeSupportUtilities.SecondsPerMonth();
                                    }
                                }

                                // If we ran out of hab time, any remaining time in the update consumes cabin time.
                                if (timeSpentUnderHab < timeSinceUpdate)
                                {
                                    // Consume the remaining amount in cabin time, scaling by seat fraction filled and multiplier.
                                    trackedKerbal.RemainingCabinTime -= (timeSinceUpdate - timeSpentUnderHab)
                                        * ((double)VesselStatus.NumCrew / (double)VesselStatus.CrewCap)
                                        * (1.0 / (1 + VesselStatus.VesselHabMultiplier));
                                    // Now cap it so we don't have cabin time going crazy negative when loading a vessel after a while
                                    if (trackedKerbal.RemainingCabinTime < -2)
                                    {
                                        trackedKerbal.RemainingCabinTime = -2;
                                    }
                                }

                                isGrouchyHab = CheckHabSideEffects(trackedKerbal);
                            }

                            // Update Supplies effects
                            if (offKerbin && (deltaTime - resultSupply.TimeFactor > tolerance))
                            {
                                isGrouchySupplies = CheckSupplySideEffects(trackedKerbal);
                            }
                            else if (deltaTime >= ResourceUtilities.FLOAT_TOLERANCE)
                            {
                                //All is well
                                trackedKerbal.LastMeal = LastUpdateTime;
                                VesselStatus.LastFeeding = LastUpdateTime;
                            }

                            // Update ElectricCharge effects
                            if (offKerbin && (deltaTime - resultEC.TimeFactor > tolerance))
                            {
                                isGrouchyEC = CheckECSideEffects(trackedKerbal);
                            }
                            else if (deltaTime >= ResourceUtilities.FLOAT_TOLERANCE)
                            {
                                //All is well
                                trackedKerbal.LastEC = LastUpdateTime;
                                VesselStatus.LastECCheck = LastUpdateTime;
                            }

                            trackedKerbal.LastUpdate = now;

                            var isAnyGrouch = isGrouchyEC || isGrouchyHab || isGrouchySupplies;
                            if (isGrouchyEC && !isCatchup)
                            {
                                ApplyEffect(
                                    trackedKerbal,
                                    crewMember,
                                    LifeSupportManager.GetNoECEffect(trackedKerbal.KerbalName),
                                    "power loss");
                            }
                            else if (isGrouchySupplies && !isCatchup)
                            {
                                ApplyEffect(
                                    trackedKerbal,
                                    crewMember,
                                    LifeSupportManager.GetNoSupplyEffect(trackedKerbal.KerbalName),
                                    "lack of supplies");
                            }
                            else if (isGrouchyHab && !isCatchup)
                            {
                                ApplyEffect(
                                    trackedKerbal,
                                    crewMember,
                                    LifeSupportManager.GetNoHomeEffect(trackedKerbal.KerbalName),
                                    "homesickness");
                            }
                            else if (crewMember.experienceTrait.Config.Name != trackedKerbal.OldTrait && !isAnyGrouch)
                            {
                                RemoveGrouchiness(crewMember, trackedKerbal);
                            }

                            LifeSupportManager.Instance.TrackKerbal(trackedKerbal);
                        }
                        if (_isStatusRefreshRequired)
                        {
                            _isStatusRefreshRequired = false;
                        }
                    }
                    #endregion - Crew

                    var remainingSupplies = ResourceBroker.AmountAvailable(
                        _crewPart,
                        "Supplies",
                        deltaTime,
                        ResourceFlowMode.ALL_VESSEL);
                    var remainingBattery = ResourceBroker.AmountAvailable(
                        _crewPart,
                        "ElectricCharge",
                        deltaTime,
                        ResourceFlowMode.ALL_VESSEL);
                    var suppliesConsumption = LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount;
                    var electricityConsumption = LifeSupportScenario.Instance.settings.GetSettings().ECAmount;

                    VesselStatus.SuppliesLeft = remainingSupplies / suppliesConsumption / _currentCrewCount / VesselStatus.RecyclerMultiplier;
                    VesselStatus.ECLeft = remainingBattery / electricityConsumption / _currentCrewCount;
                }
                else
                {
                    VesselStatus.LastECCheck = now;
                    VesselStatus.LastFeeding = now;
                    VesselStatus.LastUpdate = now;
                }

                LifeSupportManager.Instance.TrackVessel(VesselStatus);
            }
            catch (Exception ex)
            {
                print(string.Format("ERROR {0} IN ModuleLifeSupport", ex.Message));
            }
        }

        private void CheckEVA(Vessel evaKerbal)
        {
            if (IsAtHomeForEva(evaKerbal))
                return;

            var kerbal = evaKerbal.GetVesselCrew()[0];
            //Check their status.
            var kerbalStatus = LifeSupportManager.Instance.FetchKerbal(kerbal);

            //Update cabin time while EVA - this won't cause any effects until they board something, which is fine for now
            var now = Planetarium.GetUniversalTime();
            kerbalStatus.RemainingCabinTime -= now - kerbalStatus.LastUpdate;
            if (kerbalStatus.RemainingCabinTime < -2)
            {
                kerbalStatus.RemainingCabinTime = -2;
            }
            kerbalStatus.LastUpdate = now;

            if (evaKerbal.missionTime > LifeSupportScenario.Instance.settings.GetSettings().EVATime)
            {
                var effect = LifeSupportManager.GetEVAExcessEffect(kerbalStatus.KerbalName);
                ApplyEVAEffect(kerbalStatus, kerbal, evaKerbal, effect, "excessive EVA time");
            }
            else if (kerbalStatus.RemainingCabinTime < 0)
            {
                var effect = LifeSupportManager.GetNoHomeEffect(kerbalStatus.KerbalName);
                ApplyEVAEffect(kerbalStatus, kerbal, evaKerbal, effect, "homesickness");
            }
        }

        private bool IsAtHomeForEva(Vessel evaKerbal)
        {
            return (evaKerbal.mainBody == FlightGlobals.GetHomeBody()) &&
                    (evaKerbal.altitude < LifeSupportScenario.Instance.settings.GetSettings().HomeWorldAltitude);
        }

        private void ApplyEVAEffect(LifeSupportStatus trackedKerbal, ProtoCrewMember crewMember, Vessel vessel, int effectId, string reason)
        {
            if (crewMember.type == ProtoCrewMember.KerbalType.Tourist || crewMember.experienceTrait.Config.Name == "Tourist")
                return;

            /* SIDE EFFECTS:
             *   0 = No Effect (The feature is effectively turned off)
             *   1 = Grouchy (they become a Tourist until rescued)
             *   2 = Mutinous (A tourist, but also destroys a part of a nearby vessel...)
             *   3 = Instantly 'wander' back to the KSC - don't ask us how!
             *   4 = M.I.A. (will eventually respawn)
             *   5 = K.I.A. 
             */

            var screenMessage = "";
            switch (effectId)
            {
                case 0: // No effect
                    return; // No need to print
                case 1: //Grouchy
                    if (crewMember.type != ProtoCrewMember.KerbalType.Tourist)
                    {
                        screenMessage = string.Format("{0} refuses to work due to {1}", crewMember.name, reason);
                        trackedKerbal.OldTrait = crewMember.experienceTrait.Config.Name;
                        crewMember.type = ProtoCrewMember.KerbalType.Tourist;
                        KerbalRoster.SetExperienceTrait(crewMember, "Tourist");
                        trackedKerbal.IsGrouchy = true;
                        LifeSupportManager.Instance.TrackKerbal(trackedKerbal);
                    }
                    break;
                case 2:  //Mutinous
                    {
                        screenMessage = string.Format("{0} has become mutinous due to {1}", crewMember.name, reason);
                        trackedKerbal.OldTrait = crewMember.experienceTrait.Config.Name;
                        crewMember.type = ProtoCrewMember.KerbalType.Tourist;
                        KerbalRoster.SetExperienceTrait(crewMember, "Tourist");
                        trackedKerbal.IsGrouchy = true;
                        LifeSupportManager.Instance.TrackKerbal(trackedKerbal);
                    }
                    break;
                case 3: //Return to KSC
                    screenMessage = string.Format("{0} gets fed up and wanders back to the KSC due to {1}", crewMember.name, reason);
                    LifeSupportManager.Instance.UntrackKerbal(crewMember.name);
                    crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                    vessel.Die();
                    break;
                case 4: //Despawn
                    screenMessage = string.Format("{0} has gone missing due to {1}", crewMember.name, reason);
                    LifeSupportManager.Instance.UntrackKerbal(crewMember.name);
                    crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Missing;
                    vessel.Die();
                    break;
                case 5: //Kill
                    screenMessage = string.Format("{0} has died due to {1}", crewMember.name, reason);
                    LifeSupportManager.Instance.UntrackKerbal(crewMember.name);
                    crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                    vessel.Die();
                    break;
            }

            ScreenMessages.PostScreenMessage(screenMessage, 5f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void DestroyRandomPart(Vessel vessel)
        {
            System.Random rng = new System.Random();
            var vessels = GetNearbyVessels(150, false, vessel, false);
            var count = vessels.Count;
            for (int i = 0; i < count; ++i)
            {
                var v = vessels[i];
                var idx = rng.Next(1, v.parts.Count - 1);
                var part = v.parts[idx];
                if (part.parent != null)
                    part.decouple();
            }
        }

        public static List<Vessel> GetNearbyVessels(int range, bool includeSelf, Vessel focusedVessel, bool landedOnly = true)
        {
            try
            {
                var nearbyVessels = new List<Vessel>();
                var vesselCount = FlightGlobals.Vessels.Count;
                for (int i = 0; i < vesselCount; ++i)
                {
                    var vessel = FlightGlobals.Vessels[i];
                    if (vessel.mainBody == focusedVessel.mainBody
                        && (!landedOnly || vessel.Landed || vessel == focusedVessel))
                    {
                        if (!includeSelf && vessel == focusedVessel)
                            continue;

                        var focusedPosition = focusedVessel.GetWorldPos3D();
                        var neighborPosition = vessel.GetWorldPos3D();
                        var distance = Vector3d.Distance(focusedPosition, neighborPosition);
                        if (distance < range)
                        {
                            nearbyVessels.Add(vessel);
                        }
                    }
                }

                return nearbyVessels;
            }
            catch (Exception ex)
            {
                Debug.LogError(string.Format("[USI-LS] - ERROR in GetNearbyVessels - {0}", ex.Message));
                return new List<Vessel>();
            }
        }

        private void DestroyVessel(Vessel vessel)
        {
            var demoParts = new List<Part>();
            var count = vessel.parts.Count;
            for (int i = 0; i < count; ++i)
            {
                demoParts.Add(vessel.parts[i]);
            }

            for (int i = 0; i < count; ++i)
            {
                var p = demoParts[i];
                p.decouple();
                p.explode();
            }
        }

        private void UpdateVesselInfo()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null)
                return;

            CheckForDeadKerbals();
            _currentCrewCount = vessel.GetCrewCount();
            if (vessel.GetCrewCapacity() > 0)
            {
                var partCount = vessel.parts.Count;
                for (int i = 0; i < partCount; ++i)
                {
                    var part = vessel.parts[i];
                    if (part.CrewCapacity > 0)
                    {
                        _crewPart = part;
                        return;
                    }
                }
            }
        }

        private void CheckForDeadKerbals()
        {
            try
            {
                var crewRoster = vessel.GetVesselCrew();
                var crewNames = new List<string>();
                var crewCount = crewRoster.Count;
                for (int x = 0; x < crewCount; ++x)
                {
                    crewNames.Add(crewRoster[x].name);
                }
                var count = LifeSupportManager.Instance.LifeSupportInfo.Count;
                for (int i = count; i-- > 0;)
                {
                    var trackedKerbal = LifeSupportManager.Instance.LifeSupportInfo[i];
                    if (trackedKerbal.CurrentVesselId != vessel.id.ToString())
                        continue;

                    if (!crewNames.Contains(trackedKerbal.KerbalName) && IsKerbalMissing(trackedKerbal.KerbalName))
                        LifeSupportManager.Instance.UntrackKerbal(trackedKerbal.KerbalName);
                }
            }
            catch (Exception ex)
            {
                print(string.Format("ERROR {0} IN CheckForDeadKerbals", ex.Message));
            }
        }

        private bool IsKerbalMissing(string name)
        {
            var vesselCount = FlightGlobals.Vessels.Count;
            var crewCount = 0;
            for (int i = 0; i < vesselCount; ++i)
            {
                var vessel = FlightGlobals.Vessels[i];
                var crewRoster = vessel.GetVesselCrew();
                crewCount = crewRoster.Count;
                for (int x = 0; x < crewCount; ++x)
                {
                    var crewMember = crewRoster[x];
                    if (crewMember.name == name)
                        return false;
                }
            }
            return true;
        }

        private void UpdateStatus()
        {
            UpdateStatus(VesselStatus);
        }

        private void UpdateStatus(VesselSupplyStatus supplyStatus)
        {
            var now = Planetarium.GetUniversalTime();
            if (_lastUpdate < ResourceUtilities.FLOAT_TOLERANCE)
                _lastUpdate = now;

            // Give converters time to catch up before we start using calculated values
            bool fullRefresh = false;
            if (now > _lastUpdate + 5d)
            {
                fullRefresh = true;
                _lastUpdate = now;
            }

            var calcRecyclerMultiplier = (float)LifeSupportManager.GetRecyclerMultiplier(vessel);
            var calcHabTime = (float)LifeSupportManager.CalculateVesselHabExtraTime(vessel);
            var calcHabMultiplier = (float)LifeSupportManager.CalculateVesselHabMultiplier(vessel, _currentCrewCount);

            // If we're the active vessel, and we're past easing, use calculated values.  
            //  Otherwise, use the cache.
            var useCalculated = fullRefresh && vessel.id == FlightGlobals.ActiveVessel.id;

            //Start with intelligent defaults.
            if (supplyStatus.RecyclerMultiplier < ResourceUtilities.FLOAT_TOLERANCE)
                supplyStatus.RecyclerMultiplier = 1f;
            if (calcRecyclerMultiplier < ResourceUtilities.FLOAT_TOLERANCE)
                calcRecyclerMultiplier = 1f;

            //And take the lowest (non-zero)
            if (useCalculated || calcRecyclerMultiplier < supplyStatus.RecyclerMultiplier)
                supplyStatus.RecyclerMultiplier = calcRecyclerMultiplier;

            //Hab we want the best ones. 
            if (useCalculated || calcHabTime > supplyStatus.ExtraHabSpace)
                supplyStatus.ExtraHabSpace = calcHabTime;

            if (useCalculated || calcHabMultiplier > supplyStatus.VesselHabMultiplier)
                supplyStatus.VesselHabMultiplier = calcHabMultiplier;

            LifeSupportManager.Instance.TrackVessel(supplyStatus);
        }

        private void CheckVesselId()
        {
            if (string.IsNullOrEmpty(VesselStatus.VesselId))
                return;

            // Update values if the vessel id has changed (usually due to docking/undocking)
            if (VesselStatus.VesselId != vessel.id.ToString())
            {
                // We're basically just cloning the old values here
                var oldTrackedVessel = LifeSupportManager.Instance.FetchVessel(VesselStatus.VesselId);
                var newTrackedVessel = LifeSupportManager.Instance.FetchVessel(vessel.id.ToString());

                newTrackedVessel.VesselId = vessel.id.ToString();
                newTrackedVessel.VesselName = vessel.vesselName;
                newTrackedVessel.LastFeeding = oldTrackedVessel.LastFeeding;
                newTrackedVessel.LastECCheck = oldTrackedVessel.LastECCheck;
                newTrackedVessel.LastUpdate = oldTrackedVessel.LastUpdate;
                newTrackedVessel.NumCrew = oldTrackedVessel.NumCrew;
                newTrackedVessel.RecyclerMultiplier = oldTrackedVessel.RecyclerMultiplier;
                newTrackedVessel.CrewCap = oldTrackedVessel.CrewCap;
                newTrackedVessel.VesselHabMultiplier = oldTrackedVessel.VesselHabMultiplier;
                newTrackedVessel.CachedHabTime = oldTrackedVessel.CachedHabTime;
                newTrackedVessel.ExtraHabSpace = oldTrackedVessel.ExtraHabSpace;
                newTrackedVessel.SuppliesLeft = oldTrackedVessel.SuppliesLeft;
                newTrackedVessel.ECLeft = oldTrackedVessel.ECLeft;

                LifeSupportManager.Instance.TrackVessel(newTrackedVessel);
                VesselStatus = newTrackedVessel;
            }
        }

        protected double GetDeltaTime()
        {
            if (Time.timeSinceLevelLoad < 1.0f || !FlightGlobals.ready)
                return -1;

            var now = Planetarium.GetUniversalTime();
            if (Math.Abs(LastUpdateTime) < ResourceUtilities.FLOAT_TOLERANCE)
            {
                // Just started running
                LastUpdateTime = now;
                return -1;
            }

            double maxDeltaTime = ResourceUtilities.GetMaxDeltaTime();
            double deltaTime = Math.Min(now - LastUpdateTime, maxDeltaTime);

            LastUpdateTime += deltaTime;

            return deltaTime;
        }

        private bool CheckIfHomeWorld()
        {
            if (USI_GlobalBonuses.Instance.GetHabBonus(vessel.mainBody.flightGlobalsIndex) < 5)  //TODO - make this a parm
                return false;

            //Check for hab time.
            var habTime = LifeSupportManager.Instance.FetchVessel(vessel.id.ToString()).CachedHabTime;

            //We want one year, either Kerbal or earth. Note: 60 seconds X 60 minutes = 3600 seconds in an hour
            double secondsPerDay = GameSettings.KERBIN_TIME ? 3600d * 6d : 3600d * 24d;
            double secsPerYear = GameSettings.KERBIN_TIME ? secondsPerDay * 425d : secondsPerDay * 365d;

            double habYears = Math.Floor(habTime / secsPerYear);

            return habYears >= 1d;
        }

        private bool CheckHabSideEffects(LifeSupportStatus trackedKerbal)
        {
            LifeSupportManager.Instance.TrackKerbal(trackedKerbal);

            return (trackedKerbal.RemainingCabinTime < 0);
        }

        private bool CheckSupplySideEffects(LifeSupportStatus trackedKerbal)
        {
            var now = Planetarium.GetUniversalTime();
            var snackMax = LifeSupportScenario.Instance.settings.GetSettings().SupplyTime;
            var snackTime = Math.Max(now - trackedKerbal.LastMeal, ResourceUtilities.FLOAT_TOLERANCE);

            if (snackTime > ResourceUtilities.FLOAT_TOLERANCE)
                UnlockTins();

            return snackTime > snackMax;
        }

        private void UnlockTins()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                //Unlock the biscuit tins...
                bool foundSupplies = false;
                var partCount = vessel.parts.Count;
                for (int i = 0; i < partCount; ++i)
                {
                    var part = vessel.parts[i];
                    if (part.Resources.Contains("Supplies"))
                    {
                        var resource = part.Resources["Supplies"];
                        if (resource.flowState == false)
                        {
                            resource.flowState = true;
                            foundSupplies = true;
                        }
                    }
                }

                if (foundSupplies)
                    ScreenMessages.PostScreenMessage("Supply containers unlocked...", 5f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private bool CheckECSideEffects(LifeSupportStatus trackedKerbal)
        {
            var now = Planetarium.GetUniversalTime();
            var ecMax = LifeSupportScenario.Instance.settings.GetSettings().ECTime;
            var ecTime = Math.Max(now - trackedKerbal.LastEC, ResourceUtilities.FLOAT_TOLERANCE);

            return ecTime > ecMax;
        }

        private void ApplyEffect(LifeSupportStatus trackedKerbal, ProtoCrewMember crewMember, int effectId, string reason)
        {
            //Tourists are immune to effects
            if (crewMember.type == ProtoCrewMember.KerbalType.Tourist || crewMember.experienceTrait.Config.Name == "Tourist")
                return;

            /* SIDE EFFECTS:
             *   0 = No Effect (The feature is effectively turned off)
             *   1 = Grouchy (they become a Tourist until rescued)
             *   2 = Mutinous (Also a tourist, but a random part of the ship is decoupled as they search for snacks)
             *   3 = Instantly 'wander' back to the KSC - don't ask us how!
             *   4 = M.I.A. (will eventually respawn)
             *   5 = K.I.A. 
             */

            var msg = "";
            switch (effectId)
            {
                case 0: // No effect
                    return; // No need to print
                case 1: //Grouchy
                    msg = string.Format("{0} refuses to work due to {1}", crewMember.name, reason);
                    trackedKerbal.OldTrait = crewMember.experienceTrait.Config.Name;
                    crewMember.type = ProtoCrewMember.KerbalType.Tourist;
                    KerbalRoster.SetExperienceTrait(crewMember, "Tourist");
                    trackedKerbal.IsGrouchy = true;
                    LifeSupportManager.Instance.TrackKerbal(trackedKerbal);
                    break;
                case 2:  //Mutinous
                    msg = string.Format("{0} has become mutinous due to {1}", crewMember.name, reason);
                    trackedKerbal.OldTrait = crewMember.experienceTrait.Config.Name;
                    crewMember.type = ProtoCrewMember.KerbalType.Tourist;
                    KerbalRoster.SetExperienceTrait(crewMember, "Tourist");
                    trackedKerbal.IsGrouchy = true;
                    LifeSupportManager.Instance.TrackKerbal(trackedKerbal);
                    ClipRandomPart();
                    SpawnExtraSupplies(100f);
                    break;
                case 3: //Return to KSC
                    msg = string.Format("{0} gets fed up and wanders back to the KSC due to {1}", crewMember.name, reason);
                    LifeSupportManager.Instance.UntrackKerbal(crewMember.name);
                    crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                    vessel.CrewListSetDirty();
                    LifeSupportUtilities.RemoveCrewFromPart(crewMember,vessel);
                    break;
                case 4: //Despawn
                    msg = string.Format("{0} has gone missing due to {1}", crewMember.name, reason);
                    LifeSupportManager.Instance.UntrackKerbal(crewMember.name);
                    vessel.CrewListSetDirty();
                    LifeSupportUtilities.RemoveCrewFromPart(crewMember, vessel);
                    crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Missing;
                    break;
                case 5: //Kill
                    msg = string.Format("{0} has died due to {1}", crewMember.name, reason);
                    LifeSupportManager.Instance.UntrackKerbal(crewMember.name);
                    vessel.CrewListSetDirty();
                    LifeSupportUtilities.RemoveCrewFromPart(crewMember, vessel);
                    crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                    break;
            }

            ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void SpawnExtraSupplies(float sup)
        {
            ResourceBroker.StoreResource(vessel.rootPart, "Supplies", sup, TimeWarp.deltaTime, ResourceFlowMode.ALL_VESSEL);
        }


        private void ClipRandomPart()
        {
            System.Random rng = new System.Random();
            var idx = rng.Next(1, vessel.parts.Count - 1);
            var part = vessel.parts[idx];
            if (part.parent != null)
                part.decouple();
        }

        private void RemoveGrouchiness(ProtoCrewMember crewMember, LifeSupportStatus trackedKerbal)
        {
            if (trackedKerbal.IsGrouchy)
            {
                crewMember.type = ProtoCrewMember.KerbalType.Crew;
                KerbalRoster.SetExperienceTrait(crewMember, trackedKerbal.OldTrait);

                trackedKerbal.IsGrouchy = false;
                LifeSupportManager.Instance.TrackKerbal(trackedKerbal);

                string msg = string.Format("{0} has returned to duty", crewMember.name);
                ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        //public static double CalculateVesselHabExtraTime(Vessel vessel)
        //{
        //    var habTime = 0d;
        //    var habitats = vessel.FindConverterAddonsImplementing<USILS_HabitationConverterAddon>();
        //    for(int i = 0; i < habitats.Count; ++i)
        //    {
        //        var habitat = habitats[i];    
        //        habTime += habitat.KerbalMonths;
        //    }
        //    return habTime;
        //}

        //public static double CalculateVesselHabMultiplier(Vessel v, int numCrew)
        //{
        //    var habMulti = 0d;
        //    var habList = v.FindPartModulesImplementing<ModuleHabitation>();
        //    var count = habList.Count;
        //    for (int i = 0; i < count; ++i)
        //    {
        //        var hab = habList[i];
        //        habMulti += (hab.HabMultiplier * Math.Min(1, hab.CrewCapacity / numCrew));
        //    }
        //    return habMulti;
        //}
    }
}
