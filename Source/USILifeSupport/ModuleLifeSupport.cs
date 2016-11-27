using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace LifeSupport
{
    public class ModuleLifeSupport : PartModule
    {
        [KSPField(guiActive = true, guiName = "Wear")]
        public string wearPercent;

        [KSPField(isPersistant = true)]
        public string vesselId;


        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsFlight)
            {
                var v = new VesselSupplyStatus();
                v.VesselId = part.vessel.id.ToString();
                LifeSupportManager.Instance.TrackVessel(v);
                if (LifeSupportScenario.Instance.settings.GetSettings().ReplacementPartAmount < ResourceUtilities.FLOAT_TOLERANCE)
                {
                    Fields["wearPercent"].guiActive = false;
                }
            }
        }

        private void CheckVesselId()
        {
            //Something changed... 
            if (vesselId != vessel.id.ToString() && !String.IsNullOrEmpty(vesselId))
            {
                var oldV = LifeSupportManager.Instance.FetchVessel(vesselId);
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
            }
            vesselId = vessel.id.ToString();
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


        protected IResourceBroker _resBroker;
        protected ResourceConverter _resConverter;
        protected double lastUpdateTime;
        public bool _firstPass;

        public ResourceConverter ResConverter
        {
            get { return _resConverter ?? (_resConverter = new ResourceConverter(ResBroker)); }
        }

        public IResourceBroker ResBroker
        {
            get { return _resBroker ?? (_resBroker = new ResourceBroker()); }
        }

        protected double GetDeltaTime()
        {
            if (Time.timeSinceLevelLoad < 1.0f || !FlightGlobals.ready)
            {
                return -1;
            }

            if (Math.Abs(lastUpdateTime) < ResourceUtilities.FLOAT_TOLERANCE)
            {
                // Just started running
                lastUpdateTime = Planetarium.GetUniversalTime();
                return -1;
            }

            double maxDeltaTime = ResourceUtilities.GetMaxDeltaTime();
            double deltaTime = Math.Min(Planetarium.GetUniversalTime() - lastUpdateTime, maxDeltaTime);

            lastUpdateTime += deltaTime;
            return deltaTime;
        }


        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            lastUpdateTime = ResourceUtilities.GetValue(node, "lastUpdateTime", lastUpdateTime);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            node.AddValue("lastUpdateTime", lastUpdateTime);
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            try
            {
                bool isLongLoop = false;
                var offKerbin = !LifeSupportManager.IsOnKerbin(part.vessel);
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



                var v = LifeSupportManager.Instance.FetchVessel(part.vessel.id.ToString());
                v.LastUpdate = Planetarium.GetUniversalTime();
                v.VesselName = part.vessel.vesselName;
                v.NumCrew = part.vessel.GetCrewCount();
                v.CrewCap = part.vessel.GetCrewCapacity();
                if (isLongLoop)
                {
                    v.RecyclerMultiplier = (float)LifeSupportManager.GetRecyclerMultiplier(part.vessel);
                    CheckForDeadKerbals();
                }

                if (part.protoModuleCrew.Count > 0)
                {
                    #region Long loop - Vessel
                    //Only check effects periodically, this is for performance reasons.
                    if (isLongLoop)
                    {
                        //Update Hab info

                        var habMulti = CalculateVesselHabMultiplier(part.vessel, v.NumCrew);
                        var habTime = CalculateVesselHabExtraTime(part.vessel);
                        var totParts = 0d;
                        var maxParts = 0d;


                        v.ExtraHabSpace = habTime;
                        v.VesselHabMultiplier = habMulti;
                        //We also have to temper this with whether or not these parts are worn out.
                        if (part.Resources.Contains("ReplacementParts"))
                        {
                            var res = part.Resources["ReplacementParts"];
                            totParts = res.amount;
                            maxParts = res.maxAmount;
                        }

                        //Worn out parts have a corresponding negative effect.
                        if (maxParts > 0)
                        {
                            v.VesselHabMultiplier *= (totParts / maxParts);
                            v.ExtraHabSpace *= (totParts / maxParts);
                            if (totParts < 1)
                                wearPercent = "Broken!";
                            else
                                wearPercent = String.Format("{0:0.00}%", (1d - (totParts / maxParts)) * 100);

                        }
                        else
                        {
                            wearPercent = "Like New";
                        }
                    }
                    #endregion
                    //we will add a bit of a fudge factor for supplies
                    var tolerance = deltaTime / 2f;
                    //nom nom nom!
                    ConverterResults resultSupply = ResConverter.ProcessRecipe(deltaTime, SupplyRecipe, part, this, 1f);
                    ConverterResults resultEC = ResConverter.ProcessRecipe(deltaTime, ECRecipe, part, this, 1f);

                    foreach (var c in part.protoModuleCrew)
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
                            //First - Hab effects.
                            if (LifeSupportManager.IsOnKerbin(part.vessel))
                            {
                                k.LastAtHome = Planetarium.GetUniversalTime();
                                k.MaxOffKerbinTime = 648000;
                                k.TimeEnteredVessel = Planetarium.GetUniversalTime();
                            }
                            else
                            {
                                if (part.vessel.id.ToString() != k.CurrentVesselId)
                                {
                                    if (part.vessel.id.ToString() != k.PreviousVesselId)
                                        k.TimeEnteredVessel = Planetarium.GetUniversalTime();

                                    k.PreviousVesselId = k.CurrentVesselId;
                                    k.CurrentVesselId = part.vessel.id.ToString();
                                }
                            }
                            isGrouchyHab = CheckHabSideEffects(k, v);

                            //Second - Supply
                            if (offKerbin && (deltaTime - resultSupply.TimeFactor > tolerance))
                            {
                                isGrouchySupplies = CheckSupplySideEffects(k);
                            }
                            else if (deltaTime >= ResourceUtilities.FLOAT_TOLERANCE)
                            {
                                //All is well
                                k.LastMeal = lastUpdateTime;
                                v.LastFeeding = lastUpdateTime;
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
                                k.LastEC = lastUpdateTime;
                                v.LastECCheck = lastUpdateTime;
                            }


                            k.LastUpdate = Planetarium.GetUniversalTime();

                            if (!_firstPass)
                            {
                                _firstPass = true;
                            }
                            else
                            {
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
                            }
                            LifeSupportManager.Instance.TrackKerbal(k);
                        }
                        #endregion - Crew
                        var supAmount = _resBroker.AmountAvailable(part, "Supplies", deltaTime, ResourceFlowMode.ALL_VESSEL);
                        var ecAmount = _resBroker.AmountAvailable(part, "ElectricCharge", deltaTime, ResourceFlowMode.ALL_VESSEL);
                        v.SuppliesLeft = supAmount / LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount /
                                         part.vessel.GetCrewCount() /
                                         v.RecyclerMultiplier;
                        v.ECLeft = ecAmount/LifeSupportScenario.Instance.settings.GetSettings().ECAmount/
                                   part.vessel.GetCrewCount();
                    }
                }
                LifeSupportManager.Instance.TrackVessel(v);
            }
            catch (Exception ex)
            {
                print(String.Format("ERROR {0} IN ModuleLifeSupport", ex.Message));
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

        private ConversionRecipe GenerateSupplyRecipe()
        {
            //Two recipes are executed.  One for EC, one for Supplies.
            var v = LifeSupportManager.Instance.FetchVessel(part.vessel.id.ToString());
            var recipe = new ConversionRecipe();
            var numCrew = part.protoModuleCrew.Count;
            var recPercent = v.RecyclerMultiplier;
            var supAmount = LifeSupportScenario.Instance.settings.GetSettings().SupplyAmount;
            var scrapAmount = LifeSupportScenario.Instance.settings.GetSettings().WasteAmount;
            var repAmount = LifeSupportScenario.Instance.settings.GetSettings().ReplacementPartAmount;
            if (part.Resources.Contains("ReplacementParts"))
            {
                recipe.Inputs.Add(new ResourceRatio { FlowMode = ResourceFlowMode.ALL_VESSEL, Ratio = repAmount * numCrew, ResourceName = "ReplacementParts", DumpExcess = false });
            }

            var supRatio = supAmount * numCrew * recPercent;
            var mulchRatio = scrapAmount * numCrew * recPercent;

            recipe.Inputs.Add(new ResourceRatio { FlowMode = ResourceFlowMode.ALL_VESSEL, Ratio = supRatio, ResourceName = "Supplies", DumpExcess = true });
            recipe.Outputs.Add(new ResourceRatio { FlowMode = ResourceFlowMode.ALL_VESSEL, Ratio = mulchRatio, ResourceName = "Mulch", DumpExcess = true });
            return recipe;
        }

        private ConversionRecipe GenerateECRecipe()
        {
            //Two recipes are executed.  One for EC, one for Supplies.
            var v = LifeSupportManager.Instance.FetchVessel(part.vessel.id.ToString());
            var recipe = new ConversionRecipe();
            var numCrew = part.protoModuleCrew.Count;
            var ecAmount = LifeSupportScenario.Instance.settings.GetSettings().ECAmount;
            recipe.Inputs.Add(new ResourceRatio { FlowMode = ResourceFlowMode.ALL_VESSEL, Ratio = ecAmount * numCrew, ResourceName = "ElectricCharge", DumpExcess = true });
            return recipe;
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

        private double _checkInterval = 1d;
        private double _lastProcessingTime;

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



        private bool CheckHabSideEffects(LifeSupportStatus kStat, VesselSupplyStatus vsl)
        {
            var habTime = LifeSupportManager.GetTotalHabTime(vsl);
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
                    part.RemoveCrewmember(crew);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                    break;
                case 4: //Despawn
                    msg = string.Format("{0} has gone missing", crew.name);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    part.RemoveCrewmember(crew);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Missing;
                    break;
                case 5: //Kill
                    msg = string.Format("{0} has died", crew.name);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    part.RemoveCrewmember(crew);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                    break;
            }

            ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void ClipRandomPart()
        {
            Random r = new Random();
            var idx = r.Next(1, vessel.parts.Count - 1);
            var p = vessel.parts[idx];
            if (p.parent != null)
                p.decouple();
        }
    }
}

