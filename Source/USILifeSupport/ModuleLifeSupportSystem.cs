using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeSupport
{
    public class ModuleLifeSupportSystem : VesselModule
    {
        [KSPField(isPersistant = true)]
        public double LastUpdateTime;

        private bool isDirty = true;

        public override void OnLoadVessel()
        {
            GameEvents.onVesselPartCountChanged.Add(SetVesselDirty);
            GameEvents.onVesselCrewWasModified.Add(SetVesselDirty);
            GameEvents.onVesselChange.Add(SetVesselDirty);
        }

        private void SetVesselDirty(Vessel v)
        {
            isDirty = true;
        }

        private VesselSupplyStatus _vStat;
        private double _checkInterval = 1d;
        private double _lastProcessingTime;
        private int _currentCrew;
        private Part _crewPart;
        protected IResourceBroker _resBroker;
        protected ResourceConverter _resConverter;
        private int _partCount;

        public ResourceConverter ResConverter
        {
            get { return _resConverter ?? (_resConverter = new ResourceConverter(ResBroker)); }
        }

        public IResourceBroker ResBroker
        {
            get { return _resBroker ?? (_resBroker = new ResourceBroker()); }
        }

        public VesselSupplyStatus VesselStatus
        {
            get { return _vStat ?? (_vStat = SetupVesselStatus()); }
            set { _vStat = value;  }
        }

        public void OnDestroy()
        {
            GameEvents.onVesselPartCountChanged.Remove(SetVesselDirty);
            GameEvents.onVesselCrewWasModified.Remove(SetVesselDirty);
            GameEvents.onVesselChange.Remove(SetVesselDirty);
        }

        bool refreshVesselTime = false;

        private bool CheckIfHomeWorld()
        {
            if (USI_GlobalBonuses.Instance.GetHabBonus(vessel.mainBody.flightGlobalsIndex) < 5)  //TODO - make this a parm
                return false;

            //Check for hab time.
            var habTime = LifeSupportManager.Instance.FetchVessel(vessel.id.ToString()).CachedHabTime;
            //We want one year, either Kerbal or earth.
            const double secsPerMinute = 60d;
            const double secsPerHour = secsPerMinute * 60d;
            double secsPerDay = GameSettings.KERBIN_TIME ? secsPerHour * 6d : secsPerHour * 24d;
            double secsPerYear = GameSettings.KERBIN_TIME ? secsPerDay * 425d : secsPerDay * 365d;
            double y = Math.Floor(habTime / secsPerYear);

            return y >= 1;
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null)
                return;
            if (!vessel.loaded)
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
                    refreshVesselTime = true;
                }
                _partCount = vessel.parts.Count;
            }


            if (isDirty)
            {
                isDirty = false;
                UpdateVesselInfo();
                UpdateStatus();
            }

            if (_currentCrew == 0)
            {
                VesselStatus.VesselName = vessel.vesselName;
                VesselStatus.NumCrew = vessel.GetCrewCount();
                VesselStatus.CrewCap = vessel.GetCrewCapacity();
                VesselStatus.LastECCheck = Planetarium.GetUniversalTime();
                VesselStatus.LastFeeding = Planetarium.GetUniversalTime();
                VesselStatus.LastUpdate = Planetarium.GetUniversalTime();
                LifeSupportManager.Instance.TrackVessel(VesselStatus);
                LastUpdateTime = Planetarium.GetUniversalTime();
                return;
            }

            try
            {
                bool isLongLoop = false;
                var offKerbin = !LifeSupportManager.IsOnKerbin(vessel);
                CheckVesselId();

                //Check our time
                double deltaTime = GetDeltaTime();
                bool isCatchup = deltaTime / 2 > TimeWarp.fixedDeltaTime;

                if (deltaTime < ResourceUtilities.FLOAT_TOLERANCE * 10)
                    return;

                if (Planetarium.GetUniversalTime() >= _lastProcessingTime + _checkInterval)
                {
                    isLongLoop = true;
                    _lastProcessingTime = Planetarium.GetUniversalTime();
                }

                VesselStatus.LastUpdate = Planetarium.GetUniversalTime();
                VesselStatus.VesselName = vessel.vesselName;
                VesselStatus.NumCrew = vessel.GetCrewCount();
                VesselStatus.CrewCap = vessel.GetCrewCapacity();
                if (isLongLoop)
                {
                    CheckForDeadKerbals();
                }

                if (_currentCrew > 0)
                {
                    //Guard clause
                    if (_crewPart == null)
                        UpdateVesselInfo();

                    //we will add a bit of a fudge factor for supplies
                    var tolerance = deltaTime/2f;
                    //nom nom nom!
                    ConverterResults resultSupply = ResConverter.ProcessRecipe(deltaTime, SupplyRecipe, _crewPart, null,
                        1f);
                    ConverterResults resultEC = ResConverter.ProcessRecipe(deltaTime, ECRecipe, _crewPart, null, 1f);

                    #region Long Loop - Crew
                    if (isLongLoop)
                    {
                        //Ensure status is current
                        UpdateStatus();
                        var vCrew = vessel.GetVesselCrew();
                        var count = vCrew.Count;
                        var habTime = LifeSupportManager.GetTotalHabTime(VesselStatus, vessel);
                        for (int i = 0; i < count; ++i)
                        {
                            var c = vCrew[i];
                            bool isGrouchyHab = false;
                            bool isGrouchySupplies = false;
                            bool isGrouchyEC = false;
                            bool isScout = c.HasEffect("ExplorerSkill") && habTime >= LifeSupportScenario.Instance.settings.GetSettings().ScoutHabTime;
                            bool isPermaHab = habTime >= LifeSupportScenario.Instance.settings.GetSettings().PermaHabTime;
                            bool isHomeWorld = CheckIfHomeWorld() && habTime >= LifeSupportScenario.Instance.settings.GetSettings().ScoutHabTime && vessel.LandedOrSplashed; 
                            //Fetch them from the queue
                            var k = LifeSupportManager.Instance.FetchKerbal(c);
                            //Update our stuff
                            if (refreshVesselTime)
                            {
                                k.TimeEnteredVessel = Planetarium.GetUniversalTime();
                                refreshVesselTime = false;
                                LifeSupportManager.Instance.TrackKerbal(k);
                            }
                            //First - Hab effects.                        
                            if (!offKerbin || isScout || isHomeWorld || isPermaHab)
                            {
                                k.LastAtHome = Planetarium.GetUniversalTime();
                                k.MaxOffKerbinTime = habTime + k.LastAtHome;
                                k.TimeEnteredVessel = Planetarium.GetUniversalTime();
                            }
                            else
                            {
                                if (vessel.id.ToString() != k.CurrentVesselId)
                                {
                                    if (vessel.id.ToString() != k.PreviousVesselId)
                                        k.TimeEnteredVessel = Planetarium.GetUniversalTime();

                                    k.PreviousVesselId = k.CurrentVesselId;
                                    k.CurrentVesselId = vessel.id.ToString();
                                    LifeSupportManager.Instance.TrackKerbal(k);
                                }
                                isGrouchyHab = CheckHabSideEffects(k);
                            }


                            //Second - Supply
                            if (offKerbin && (deltaTime - resultSupply.TimeFactor > tolerance))
                            {
                                isGrouchySupplies = CheckSupplySideEffects(k);
                            }
                            else if (deltaTime >= ResourceUtilities.FLOAT_TOLERANCE)
                            {
                                //All is well
                                k.LastMeal = LastUpdateTime;
                                VesselStatus.LastFeeding = LastUpdateTime;
                            }

                            //Third - EC
                            if (offKerbin && (deltaTime - resultEC.TimeFactor > tolerance))
                            {
                                isGrouchyEC = CheckECSideEffects(k);
                            }
                            else if (deltaTime >= ResourceUtilities.FLOAT_TOLERANCE)
                            {
                                //All is well
                                k.LastEC = LastUpdateTime;
                                VesselStatus.LastECCheck = LastUpdateTime;
                            }


                            k.LastUpdate = Planetarium.GetUniversalTime();
                            var isAnyGrouch = isGrouchyEC || isGrouchyHab || isGrouchySupplies;

                            if (isGrouchyEC && !isCatchup)
                            {
                                ApplyEffect(k, c,
                                    LifeSupportManager.GetNoECEffect(k.KerbalName),
                                    "power loss");
                            }
                            else if (isGrouchySupplies && !isCatchup)
                            {
                                ApplyEffect(k, c,
                                    LifeSupportManager.GetNoSupplyEffect(k.KerbalName),
                                    "lack of supplies");
                            }
                            else if (isGrouchyHab && !isCatchup)
                            {
                                ApplyEffect(k, c,
                                    LifeSupportManager.GetNoHomeEffect(k.KerbalName),
                                    "homesickness");
                            }
                            else if (c.experienceTrait.Title != k.OldTrait && !isAnyGrouch)
                            {
                                RemoveGrouchiness(c, k);
                            }
                            LifeSupportManager.Instance.TrackKerbal(k);
                        }
                    }
                    #endregion - Crew

                    var supAmount = _resBroker.AmountAvailable(_crewPart, "Supplies", deltaTime,
                    ResourceFlowMode.ALL_VESSEL);
                    var ecAmount = _resBroker.AmountAvailable(_crewPart, "ElectricCharge", deltaTime,
                        ResourceFlowMode.ALL_VESSEL);
                    VesselStatus.SuppliesLeft = supAmount /
                                                LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount /
                                                _currentCrew /
                                                VesselStatus.RecyclerMultiplier;
                    VesselStatus.ECLeft = ecAmount / LifeSupportScenario.Instance.settings.GetSettings().ECAmount /
                                          _currentCrew;
                }
                else
                {
                    VesselStatus.LastECCheck = Planetarium.GetUniversalTime();
                    VesselStatus.LastFeeding = Planetarium.GetUniversalTime();
                    VesselStatus.LastUpdate = Planetarium.GetUniversalTime();
                }
                LifeSupportManager.Instance.TrackVessel(VesselStatus);
            }
            catch (Exception ex)
            {
                print(String.Format("ERROR {0} IN ModuleLifeSupport", ex.Message));
            }
        }

        private void UpdateVesselInfo()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null)
                return;

            CheckForDeadKerbals();
            _currentCrew = vessel.GetCrewCount();
            if (vessel.GetCrewCapacity() > 0)
            {
                var count = vessel.parts.Count;
                for (int i = 0; i < count; ++i)
                {
                    var p = vessel.parts[i];
                    if (p.CrewCapacity > 0)
                    {
                        _crewPart = p;
                        return;
                    }
                }
            }
        }

        private void UpdateStatus()
        {
            UpdateStatus(VesselStatus);
        }

        private double _lastUpdate;

        private void UpdateStatus(VesselSupplyStatus v)
        {
            if (_lastUpdate < ResourceUtilities.FLOAT_TOLERANCE)
                _lastUpdate = Planetarium.GetUniversalTime();

            bool fullRefresh = false;

            if (Planetarium.GetUniversalTime() > _lastUpdate + 5d) //A reasonable time for easing in everything
            {
                fullRefresh = true;
                _lastUpdate = Planetarium.GetUniversalTime();
            }

            var newRecMult = (float)LifeSupportManager.GetRecyclerMultiplier(vessel);
            var newSpace = (float)LifeSupportManager.CalculateVesselHabExtraTime(vessel);
            var newHabMult = (float)LifeSupportManager.CalculateVesselHabMultiplier(vessel, _currentCrew);
            //If we're the active vessel, and we're past easing, we always take calc values.  
            //Otherwise, let's use the cache.
            var useCur = fullRefresh && vessel.id == FlightGlobals.ActiveVessel.id;

            //Start with intelligent defaults.
            if (v.RecyclerMultiplier < ResourceUtilities.FLOAT_TOLERANCE)
                v.RecyclerMultiplier = 1f;
            if (newRecMult < ResourceUtilities.FLOAT_TOLERANCE)
                newRecMult = 1f;
            //And take the lowest (non-zero)
            if (useCur || newRecMult < v.RecyclerMultiplier)
                v.RecyclerMultiplier = newRecMult;

            //Hab we want the best ones. 
            if (useCur || newSpace > v.ExtraHabSpace)
                v.ExtraHabSpace = newSpace;

            if (useCur || newHabMult > v.VesselHabMultiplier)
                v.VesselHabMultiplier = newHabMult;

            LifeSupportManager.Instance.TrackVessel(v);
        }


        private VesselSupplyStatus SetupVesselStatus()
        {
            var id = vessel.id.ToString();
            var v = LifeSupportManager.Instance.FetchVessel(id);
            UpdateVesselInfo();
            LifeSupportManager.Instance.TrackVessel(v);
            return v;
        }

        private void UnlockTins()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                //Unlock the biscuit tins...
                bool foundSupplies = false;
                var count = vessel.parts.Count;
                for (int i = 0; i < count; ++i)
                {
                    var p = vessel.parts[i];
                    if (p.Resources.Contains("Supplies"))
                    {
                        var r = p.Resources["Supplies"];
                        if (r.flowState == false)
                        {
                            r.flowState = true;
                            foundSupplies = true;
                        }
                    }
                }
                if(foundSupplies)
                    ScreenMessages.PostScreenMessage("Supply containers unlocked...", 5f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        protected double GetDeltaTime()
        {
            if (Time.timeSinceLevelLoad < 1.0f || !FlightGlobals.ready)
            {
                return -1;
            }

            if (Math.Abs(LastUpdateTime) < ResourceUtilities.FLOAT_TOLERANCE)
            {
                // Just started running
                LastUpdateTime = Planetarium.GetUniversalTime();
                return -1;
            }

            double maxDeltaTime = ResourceUtilities.GetMaxDeltaTime();
            double deltaTime = Math.Min(Planetarium.GetUniversalTime() - LastUpdateTime, maxDeltaTime);

            LastUpdateTime += deltaTime;
            return deltaTime;
        }

        private void CheckForDeadKerbals()
        {
            try
            {
                var crewNames = new List<string>();
                var vCrew = vessel.GetVesselCrew();
                var cCount = vCrew.Count;
                for (int x = 0; x < cCount; ++x)
                {
                    crewNames.Add(vCrew[x].name);
                }
                var count = LifeSupportManager.Instance.LifeSupportInfo.Count;
                for(int i = count; i --> 0;)
                {
                    var thisCrew = LifeSupportManager.Instance.LifeSupportInfo[i];
                    if (thisCrew.CurrentVesselId != vessel.id.ToString())
                        continue;

                    if(!crewNames.Contains(thisCrew.KerbalName) && KerbalIsMissing(thisCrew.KerbalName))
                        LifeSupportManager.Instance.UntrackKerbal(thisCrew.KerbalName);
                }
            }
            catch (Exception ex)
            {
                print(String.Format("ERROR {0} IN CheckForDeadKerbals", ex.Message));
            }
        }

        private bool KerbalIsMissing(string name)
        {
            var vCount = FlightGlobals.Vessels.Count;
            var cCount = 0;
            for (int i = 0; i < vCount; ++i)
            {
                var v = FlightGlobals.Vessels[i];
                var crew = v.GetVesselCrew();
                cCount = crew.Count;
                for (int x = 0; x < cCount; ++x)
                {
                    var c = crew[x];
                    if (c.name == name)
                        return false;
                }
            }
            return true;
        }

        private void CheckVesselId()
        {
            if(String.IsNullOrEmpty(VesselStatus.VesselId))
                return;

            //Something changed... 
            if (VesselStatus.VesselId != vessel.id.ToString())
            {
                var oldV = LifeSupportManager.Instance.FetchVessel(VesselStatus.VesselId);
                var newV = LifeSupportManager.Instance.FetchVessel(vessel.id.ToString());
                newV.LastFeeding = oldV.LastFeeding;
                newV.LastECCheck = oldV.LastECCheck;
                newV.LastUpdate = oldV.LastUpdate;
                newV.NumCrew = oldV.NumCrew;
                newV.RecyclerMultiplier = oldV.RecyclerMultiplier;
                newV.CrewCap = oldV.CrewCap;
                newV.VesselHabMultiplier = oldV.VesselHabMultiplier;
                newV.CachedHabTime = oldV.CachedHabTime;
                newV.ExtraHabSpace = oldV.ExtraHabSpace;
                newV.SuppliesLeft = oldV.SuppliesLeft;
                newV.ECLeft = oldV.ECLeft;
                newV.VesselId = vessel.id.ToString();
                newV.VesselName = vessel.vesselName;
                LifeSupportManager.Instance.TrackVessel(newV);
                VesselStatus = newV;
            }
        }

        public static double CalculateVesselHabExtraTime(Vessel v)
        {
            var habTime = 0d;
            var habList = v.FindPartModulesImplementing<ModuleHabitation>();
            var count = habList.Count;
            for(int i = 0; i < count; ++i)
            {
                var hab = habList[i];    
                habTime += hab.KerbalMonths;
            }
            return habTime;
        }

        public static double CalculateVesselHabMultiplier(Vessel v, int numCrew)
        {
            var habMulti = 0d;
            var habList = v.FindPartModulesImplementing<ModuleHabitation>();
            var count = habList.Count;
            for (int i = 0; i < count; ++i)
            {
                var hab = habList[i];
                habMulti += (hab.HabMultiplier * Math.Min(1, hab.CrewCapacity / numCrew));
            }
            return habMulti;
        }

        private ConversionRecipe SupplyRecipe
        {
            get
            {
                return GenerateSupplyRecipe();
            }
        }

        private ConversionRecipe ECRecipe
        {
            get
            {
                return GenerateECRecipe();
            }
        }

        private ConversionRecipe GenerateSupplyRecipe()
        {
            //Two recipes are executed.  One for EC, one for Supplies.
            var recipe = new ConversionRecipe();
            var numCrew = _currentCrew;
            var recMult = VesselStatus.RecyclerMultiplier;   
            var supAmount = LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount;
            var scrapAmount = LifeSupportScenario.Instance.settings.GetSettings().WasteAmount;
            var supRatio = supAmount * numCrew * recMult;
            var mulchRatio = scrapAmount * numCrew * recMult;
            recipe.Inputs.Add(new ResourceRatio { FlowMode = ResourceFlowMode.ALL_VESSEL, Ratio = supRatio, ResourceName = "Supplies", DumpExcess = true });
            recipe.Outputs.Add(new ResourceRatio { FlowMode = ResourceFlowMode.ALL_VESSEL, Ratio = mulchRatio, ResourceName = "Mulch", DumpExcess = true });
            return recipe;
        }

        private ConversionRecipe GenerateECRecipe()
        {
            //Two recipes are executed.  One for EC, one for Supplies.
            var recipe = new ConversionRecipe();
            var numCrew = _currentCrew;
            var ecAmount = LifeSupportScenario.Instance.settings.GetSettings().ECAmount;
            recipe.Inputs.Add(new ResourceRatio { FlowMode = ResourceFlowMode.ALL_VESSEL, Ratio = ecAmount * numCrew, ResourceName = "ElectricCharge", DumpExcess = true });
            return recipe;
        }

        private bool CheckSupplySideEffects(LifeSupportStatus kStat)
        {
            var curTime = Planetarium.GetUniversalTime();
            var SnackMax = LifeSupportScenario.Instance.settings.GetSettings().SupplyTime;

            var SnackTime = Math.Max(curTime - kStat.LastMeal, ResourceUtilities.FLOAT_TOLERANCE);
            
            if(SnackTime > ResourceUtilities.FLOAT_TOLERANCE)
                UnlockTins();

            if (SnackTime > SnackMax)
            {
                return true;
            }
            return false;
        }

        private bool CheckECSideEffects(LifeSupportStatus kStat)
        {
            var curTime = Planetarium.GetUniversalTime();
            var ecMax = LifeSupportScenario.Instance.settings.GetSettings().ECTime;
            var ecTime = Math.Max(curTime - kStat.LastEC, ResourceUtilities.FLOAT_TOLERANCE);
            if (ecTime > ecMax)
            {
                return true;
            }
            return false;
        }

        private bool CheckHabSideEffects(LifeSupportStatus kStat)
        {
            var habTime = LifeSupportManager.GetTotalHabTime(VesselStatus, vessel);
            if (kStat.LastAtHome < 1)
                kStat.LastAtHome = Planetarium.GetUniversalTime();
            if (habTime + kStat.LastAtHome > kStat.MaxOffKerbinTime)
                kStat.MaxOffKerbinTime = habTime + kStat.LastAtHome;

            LifeSupportManager.Instance.TrackKerbal(kStat);

            if (Planetarium.GetUniversalTime() > kStat.MaxOffKerbinTime || (Planetarium.GetUniversalTime() - kStat.TimeEnteredVessel) > habTime)
            {
                return true;
            }
            return false;
        }


        private void RemoveGrouchiness(ProtoCrewMember c, LifeSupportStatus k)
        {
            if (k.IsGrouchy)
            {
                string msg = string.Format("{0} has returned to duty", c.name);
                ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                c.type = ProtoCrewMember.KerbalType.Crew;
                KerbalRoster.SetExperienceTrait(c, k.OldTrait);
                k.IsGrouchy = false;
                LifeSupportManager.Instance.TrackKerbal(k);
            }
        }

        private void ApplyEffect(LifeSupportStatus kStat, ProtoCrewMember crew, int effectId, string reason)
        {
            //Tourists are immune to effects
            if (crew.type == ProtoCrewMember.KerbalType.Tourist || crew.experienceTrait.Title == "Tourist")
                return;

            /*
                *  SIDE EFFECTS:
                * 
                *  0 = No Effect (The feature is effectively turned off
                *  1 = Grouchy (they become a Tourist until rescued)
                *  2 = Mutinous (A tourist, but a random part of the ship is decoupled as they search for snacks
                *  3 = Instantly 'wander' back to the KSC - don't ask us how!
                *  4 = M.I.A. (will eventually respawn)
                *  5 = K.I.A. 
                * 
                */

            var msg = "";
            switch (effectId)
            {
                case 0: // No effect
                    return; // No need to print
                case 1: //Grouchy
                    msg = string.Format("{0} refuses to work {1}", crew.name, reason);
                    kStat.OldTrait = crew.experienceTrait.Title;
                    crew.type = ProtoCrewMember.KerbalType.Tourist;
                    KerbalRoster.SetExperienceTrait(crew, "Tourist");
                    kStat.IsGrouchy = true;
                    LifeSupportManager.Instance.TrackKerbal(kStat);
                    break;
                case 2:  //Mutinous
                    {
                        msg = string.Format("{0} has become mutinous due to {1}", crew.name, reason);
                        kStat.OldTrait = crew.experienceTrait.Title;
                        crew.type = ProtoCrewMember.KerbalType.Tourist;
                        KerbalRoster.SetExperienceTrait(crew, "Tourist");
                        kStat.IsGrouchy = true;
                        LifeSupportManager.Instance.TrackKerbal(kStat);
                        ClipRandomPart();
                        SpawnExtraSupplies(100f);
                    }
                    break;
                case 3: //Return to KSC
                    msg = string.Format("{0} gets fed up and wanders back to the KSC due to {1}", crew.name, reason);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                    vessel.CrewListSetDirty();
                    RemoveCrewFromPart(crew);
                    break;
                case 4: //Despawn
                    msg = string.Format("{0} has gone missing due to {1}", crew.name, reason);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    vessel.CrewListSetDirty();
                    RemoveCrewFromPart(crew);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Missing;
                    break;
                case 5: //Kill
                    msg = string.Format("{0} has died due to {1}", crew.name, reason);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    vessel.CrewListSetDirty();
                    RemoveCrewFromPart(crew);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                    break;
            }

            ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void SpawnExtraSupplies(float sup)
        {
            ResBroker.StoreResource(vessel.rootPart, "Supplies", sup, TimeWarp.deltaTime, ResourceFlowMode.ALL_VESSEL);
        }

        private void RemoveCrewFromPart(ProtoCrewMember crew)
        {
            var parts = vessel.parts;
            for (int i = 0; i < parts.Count; ++i)
            {
                var p = parts[i];
                if (p.CrewCapacity > 0)
                {
                    var crewList = p.protoModuleCrew;
                    var cCount = crewList.Count;
                    for (int x = 0; x < cCount; x++)
                    {
                        var c = crewList[x];
                        if (c.name == crew.name)
                        {
                            p.RemoveCrewmember(c);
                            return;
                        }
                    }
                }
            }
        }

        private void ClipRandomPart()
        {
            System.Random r = new System.Random();
            var idx = r.Next(1, vessel.parts.Count - 1);
            var p = vessel.parts[idx];
            if (p.parent != null)
                p.decouple();
        }

        private void CheckEVA(Vessel evaKerbal)
        {
            if (IsAtHomeForEva(evaKerbal))
            {
                return;
            }

            var kerbal = evaKerbal.GetVesselCrew()[0];
            //Check their status.
            var kerbalStatus = LifeSupportManager.Instance.FetchKerbal(kerbal);
            if (evaKerbal.missionTime > LifeSupportScenario.Instance.settings.GetSettings().EVATime)
            {
                var effect = LifeSupportManager.GetEVAExcessEffect(kerbalStatus.KerbalName);
                ApplyEVAEffect(kerbalStatus, kerbal, evaKerbal, effect);
            }
        }

        private bool IsAtHomeForEva(Vessel evaKerbal)
        {
            return (evaKerbal.mainBody == FlightGlobals.GetHomeBody()) &&
                    (evaKerbal.altitude < LifeSupportScenario.Instance.settings.GetSettings().HomeWorldAltitude);
        }

        private void ApplyEVAEffect(LifeSupportStatus kStat, ProtoCrewMember crew, Vessel v, int effectId)
        {
            if (crew.type == ProtoCrewMember.KerbalType.Tourist || crew.experienceTrait.Title == "Tourist")
                return;

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
                case 0: // No effect
                return; // No need to print
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

        private void DestroyRandomPart(Vessel thisVessel)
        {
            System.Random r = new System.Random();
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

    }
}
