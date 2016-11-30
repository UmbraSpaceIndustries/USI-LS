using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LifeSupport
{
    public class ModuleLifeSupportSystem : VesselModule
    {
        [KSPField(isPersistant = true)]
        public double LastUpdateTime;

        private bool isDirty = true;

        public void Start()
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

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null)
                return;
            if (!vessel.loaded)
                return;

            if (isDirty)
            {
                isDirty = false;
                UpdateVesselInfo();
                UpdateStatus();
            }

            if (_currentCrew == 0)
                return;

            try
            {
                bool isLongLoop = false;
                var offKerbin = !LifeSupportManager.IsOnKerbin(vessel);
                UnlockTins();
                CheckVesselId();

                //Check our time
                double deltaTime = GetDeltaTime();

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
                    if(_crewPart == null)
                        UpdateVesselInfo();

                    //we will add a bit of a fudge factor for supplies
                    var tolerance = deltaTime / 2f;
                    //nom nom nom!
                    ConverterResults resultSupply = ResConverter.ProcessRecipe(deltaTime, SupplyRecipe, _crewPart, null, 1f);
                    ConverterResults resultEC = ResConverter.ProcessRecipe(deltaTime, ECRecipe, _crewPart, null, 1f);

                    foreach (var c in vessel.GetVesselCrew())
                    {
                        bool isGrouchyHab = false;
                        bool isGrouchySupplies = false;
                        bool isGrouchyEC = false;
                        //Fetch them from the queue
                        var k = LifeSupportManager.Instance.FetchKerbal(c);
                        //Update our stuff

                        #region Long Loop - Crew
                        if (isLongLoop)
                        {
                            //Ensure status is current
                            UpdateStatus();
                            //First - Hab effects.
                            if (LifeSupportManager.IsOnKerbin(vessel))
                            {
                                k.LastAtHome = Planetarium.GetUniversalTime();
                                k.MaxOffKerbinTime = 648000;
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
                                }
                            }
                            isGrouchyHab = CheckHabSideEffects(k);

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
                            //Second - Supply
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

                            if (isGrouchyEC)
                            {
                                ApplyEffect(k, c,
                                    LifeSupportManager.isVet(k.KerbalName)
                                        ? LifeSupportScenario.Instance.settings.GetSettings().NoSupplyEffectVets
                                        : LifeSupportScenario.Instance.settings.GetSettings().NoSupplyEffect);
                            }
                            else if (isGrouchySupplies)
                            {
                                ApplyEffect(k, c,
                                    LifeSupportManager.isVet(k.KerbalName)
                                        ? LifeSupportScenario.Instance.settings.GetSettings().NoSupplyEffectVets
                                        : LifeSupportScenario.Instance.settings.GetSettings().NoSupplyEffect);
                            }
                            else if (isGrouchyHab)
                            {
                                ApplyEffect(k, c,
                                    LifeSupportManager.isVet(k.KerbalName)
                                        ? LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffectVets
                                        : LifeSupportScenario.Instance.settings.GetSettings().NoHomeEffect);
                            }
                            else if (c.experienceTrait.Title != k.OldTrait)
                            {
                                RemoveGrouchiness(c, k);
                            }
                            LifeSupportManager.Instance.TrackKerbal(k);
                        }
                        #endregion - Crew
                        var supAmount = _resBroker.AmountAvailable(_crewPart, "Supplies", deltaTime, ResourceFlowMode.ALL_VESSEL);
                        var ecAmount = _resBroker.AmountAvailable(_crewPart, "ElectricCharge", deltaTime, ResourceFlowMode.ALL_VESSEL);
                        VesselStatus.SuppliesLeft = supAmount / LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount /
                                         _currentCrew /
                                         VesselStatus.RecyclerMultiplier;
                        VesselStatus.ECLeft = ecAmount/LifeSupportScenario.Instance.settings.GetSettings().ECAmount/
                                              _currentCrew;
                    }
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
            if(vessel.GetCrewCapacity() > 0)
                _crewPart = vessel.Parts.First(p => p.CrewCapacity > 0);
        }

        private void UpdateStatus()
        {
            UpdateStatus(VesselStatus);
        }

        private void UpdateStatus(VesselSupplyStatus v)
        {
            v.RecyclerMultiplier = (float)LifeSupportManager.GetRecyclerMultiplier(vessel);
            v.ExtraHabSpace = (float)LifeSupportManager.CalculateVesselHabExtraTime(vessel);
            v.VesselHabMultiplier = (float)LifeSupportManager.CalculateVesselHabMultiplier(vessel,_currentCrew);
            LifeSupportManager.Instance.TrackVessel(v);
        }


        private VesselSupplyStatus SetupVesselStatus()
        {
            VesselSupplyStatus v = new VesselSupplyStatus();
            v.VesselId = vessel.id.ToString();
            UpdateVesselInfo();
            LifeSupportManager.Instance.TrackVessel(v);
            UpdateStatus(v);
            return v;
        }

        private void UnlockTins()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                //Unlock the biscuit tins...
                foreach (var p in vessel.parts)
                {
                    if (p.Resources.Contains("Supplies"))
                    {
                        var r = p.Resources["Supplies"];
                        r.flowState = true;
                    }
                }
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
                var thisCrew = LifeSupportManager.Instance.LifeSupportInfo.Where(k => k.CurrentVesselId == vessel.id.ToString());
                var deadKerbals = new List<String>();
                foreach (var k in thisCrew)
                {
                    if (vessel.GetVesselCrew().All(c => c.name != k.KerbalName))
                    {
                        deadKerbals.Add(k.KerbalName);
                    }
                }
                foreach (var k in deadKerbals)
                {
                    if (KerbalIsMissing(k))
                        LifeSupportManager.Instance.UntrackKerbal(k);
                }
            }
            catch (Exception ex)
            {
                print(String.Format("ERROR {0} IN CheckForDeadKerbals", ex.Message));
            }
        }

        private bool KerbalIsMissing(string name)
        {
            foreach (var v in FlightGlobals.Vessels)
            {
                foreach (var c in v.GetVesselCrew())
                {
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
                newV.RecyclerMultiplier = oldV.NumCrew;
                newV.CrewCap = oldV.CrewCap;
                newV.VesselHabMultiplier = oldV.VesselHabMultiplier;
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
            foreach (var hab in v.FindPartModulesImplementing<ModuleHabitation>())
            {
                //Next.  Certain modules, in addition to crew capacity, have living space.
                habTime += hab.KerbalMonths;
            }
            return habTime;
        }

        public static double CalculateVesselHabMultiplier(Vessel v, int numCrew)
        {
            var habMulti = 0d;
            foreach (var hab in v.FindPartModulesImplementing<ModuleHabitation>())
            {
                //Lastly.  Some modules act more as 'multipliers', dramatically extending a hab's workable lifespan.
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
            var recPercent = VesselStatus.RecyclerMultiplier;
            var supAmount = LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount;
            var scrapAmount = LifeSupportScenario.Instance.settings.GetSettings().WasteAmount;
            var supRatio = supAmount * numCrew * recPercent;
            var mulchRatio = scrapAmount * numCrew * recPercent;
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
            var habTime = LifeSupportManager.GetTotalHabTime(VesselStatus);
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
            if (c.type == ProtoCrewMember.KerbalType.Tourist && k.IsGrouchy)
            {
                string msg = string.Format("{0} has returned to duty", c.name);
                ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                c.type = ProtoCrewMember.KerbalType.Crew;
                KerbalRoster.SetExperienceTrait(c, k.OldTrait);
                k.IsGrouchy = false;
                LifeSupportManager.Instance.TrackKerbal(k);
            }
        }

        private void ApplyEffect(LifeSupportStatus kStat, ProtoCrewMember crew, int effectId)
        {
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
                        ClipRandomPart();
                    }
                    break;
                case 3: //Return to KSC
                    msg = string.Format("{0} gets fed up and wanders back to the KSC", crew.name);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                    vessel.CrewListSetDirty();
                    RemoveCrewFromPart(crew);
                    break;
                case 4: //Despawn
                    msg = string.Format("{0} has gone missing", crew.name);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    vessel.CrewListSetDirty();
                    RemoveCrewFromPart(crew);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Missing;
                    break;
                case 5: //Kill
                    msg = string.Format("{0} has died", crew.name);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    vessel.CrewListSetDirty();
                    RemoveCrewFromPart(crew);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                    break;
            }

            ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void RemoveCrewFromPart(ProtoCrewMember crew)
        {
            foreach (var p in vessel.parts.Where(p => p.CrewCapacity > 0))
            {
                foreach (var c in p.protoModuleCrew)
                {
                    if (c.name == crew.name)
                    {
                        p.RemoveCrewmember(c);
                        return;
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
    }
}
