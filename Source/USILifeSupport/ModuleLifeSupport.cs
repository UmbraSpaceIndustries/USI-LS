using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace LifeSupport
{
    public class ModuleLifeSupport : PartModule
    {
        [KSPField(guiActive = true, guiName = "Wear")] public string wearPercent;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsFlight)
            {
                var v = new VesselSupplyStatus();
                v.VesselId = part.vessel.id.ToString();
                LifeSupportManager.Instance.TrackVessel(v);
                if (LifeSupportSetup.Instance.LSConfig.ReplacementPartAmount < ResourceUtilities.FLOAT_TOLERANCE)
                {
                    Fields["wearPercent"].guiActive = false;
                }
            }
        }


        private ConversionRecipe LifeSupportRecipe
        {
            get
            {
                return GenerateLSRecipe();
            }
        }

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

        protected void CalculateKerbalSupplySeconds(out double supplySeconds, out double mealTime, out double deltaTime)
        {
            double now = Planetarium.GetUniversalTime();
            double previousMeal = now;

            foreach (ProtoCrewMember m in part.protoModuleCrew)
            {
                LifeSupportStatus status = LifeSupportManager.Instance.FetchKerbal(m);
                previousMeal = Math.Min(previousMeal, status.LastMeal);
            }

            supplySeconds = 0;

            if (previousMeal >= now)
            {
                mealTime = now;
                deltaTime = 0;
                return;
            }

            mealTime = Math.Min(now, previousMeal + ResourceUtilities.GetMaxDeltaTime());
            deltaTime = mealTime - previousMeal;

            foreach (ProtoCrewMember m in part.protoModuleCrew)
            {
                LifeSupportStatus status = LifeSupportManager.Instance.FetchKerbal(m);

                if (status.LastMeal < mealTime)
                {
                    supplySeconds += mealTime - status.LastMeal;
                }
            }
        }

        private int firstFixedUpdate = 1;
        public void FixedUpdate()
        {
            // When switching scenes to a vessel FixedUpdate will get called before the scenario is restored from configuration.
            // In this case changes to the vessel will remain but changes to the scenario will get undone (causing over-consumption
            // of supplies). Delaying processing prevents that from happening.
            if (firstFixedUpdate > 0)
            {
                firstFixedUpdate = 0;
                return;
            }
            try
            {
                bool isLongLoop = false;
                bool offKerbin = true;
                if(HighLogic.LoadedSceneIsFlight)
                    offKerbin = !LifeSupportManager.IsOnKerbin(part.vessel);

                UnlockTins();
                //Check our time
                double supplySeconds;
                double mealTime;
                double deltaTime;
                CalculateKerbalSupplySeconds(out supplySeconds, out mealTime, out deltaTime);

                if (supplySeconds < ResourceUtilities.FLOAT_TOLERANCE)
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
                    v.RecyclerMultiplier = (float) LifeSupportManager.GetRecyclerMultiplier(part.vessel);
                    CheckForDeadKerbals();
                }

                if (part.protoModuleCrew.Count > 0)
                {
                    #region Long loop - Vessel
                    //Only check effects periodically, this is for performance reasons.
                    if (isLongLoop)
                    {
                        //Update Hab info
                        var habMulti = 0d;
                        var habTime = 0d;
                        var totParts = 0d;
                        var maxParts = 0d;
                        var habMods = part.vessel.FindPartModulesImplementing<ModuleHabitation>();

                        foreach (var hab in habMods)
                        {
                            //Next.  Certain modules, in addition to crew capacity, have living space.
                            habTime += hab.KerbalMonths;
                            //Lastly.  Some modules act more as 'multipliers', dramatically extending a hab's workable lifespan.
                            habMulti += (hab.HabMultiplier * Math.Min(1, hab.CrewCapacity / v.NumCrew));
                        }

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
                    var tolerance = supplySeconds / 2f;
                    //nom nom nom!
                    ConverterResults result = ResConverter.ProcessRecipe(supplySeconds, LifeSupportRecipe, part, this, 1f);

                    foreach (var c in part.protoModuleCrew)
                    {
                        bool isGrouchyHab = false;
                        bool isGrouchySupplies = false;
                        //Fetch them from the queue
                        var k = LifeSupportManager.Instance.FetchKerbal(c);
                        //Update our stuff

                        #region Long Loop - Crew
                        if (isLongLoop)
                        {
                            //First - Hab effects.
                            if (LifeSupportManager.IsOnKerbin(part.vessel))
                            {
                                k.LastOnKerbin = Planetarium.GetUniversalTime();
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
                            if ((LifeSupportManager.isVet(k.KerbalName)
                                            ? LifeSupportSetup.Instance.LSConfig.NoHomeEffectVets
                                            : LifeSupportSetup.Instance.LSConfig.NoHomeEffect) > 0)
                            {
                                isGrouchyHab = CheckHabSideEffects(k, v);
                            }
                        }
                        #endregion - Crew
                        //Second - Supply
  
                        if (offKerbin && (supplySeconds - result.TimeFactor > tolerance))
                        {
                            isGrouchySupplies = CheckSupplySideEffects(k);
                            k.LastUpdate = Math.Min(Planetarium.GetUniversalTime(), k.LastUpdate + deltaTime);
                        }
                        else
                        {
                            //All is well
                            k.LastUpdate = k.LastMeal = Math.Max(k.LastMeal, mealTime);
                            v.LastFeeding = Math.Max(v.LastFeeding, mealTime);
                        }

                        if (!isGrouchyHab && !isGrouchySupplies)
                            RemoveGrouchiness(c, k);

                        if (k.LastUpdate + _checkInterval * 2 > Planetarium.GetUniversalTime())
                        {
                            if (isGrouchyHab)
                            {
                                ApplyEffect(k, c,
                                    LifeSupportManager.isVet(k.KerbalName)
                                        ? LifeSupportSetup.Instance.LSConfig.NoHomeEffectVets
                                        : LifeSupportSetup.Instance.LSConfig.NoHomeEffect);
                            }
                            if (isGrouchySupplies)
                            {
                                ApplyEffect(k, c,
                                    LifeSupportManager.isVet(k.KerbalName)
                                        ? LifeSupportSetup.Instance.LSConfig.NoSupplyEffectVets
                                        : LifeSupportSetup.Instance.LSConfig.NoSupplyEffect);
                            }
                        }
                        LifeSupportManager.Instance.TrackKerbal(k);
                        var supAmount = _resBroker.AmountAvailable(part, "Supplies", supplySeconds, "ALL_VESSEL");
                        v.SuppliesLeft = supAmount/LifeSupportSetup.Instance.LSConfig.SupplyAmount/
                                         part.vessel.GetCrewCount()/
                                         v.RecyclerMultiplier;
                    }
                }
                LifeSupportManager.Instance.TrackVessel(v);
            }
            catch (Exception ex)
            {
                print(String.Format("ERROR {0} IN ModuleLifeSupport",ex.Message));
            }
        }

        private ConversionRecipe GenerateLSRecipe()
        {
            //This is where the rubber hits the road.  Let us see if we can
            //keep our Kerbals cozy and warm.
            var v = LifeSupportManager.Instance.FetchVessel(part.vessel.id.ToString());
            var recipe = new ConversionRecipe();
            var recPercent = v.RecyclerMultiplier;
            var ecAmount = LifeSupportSetup.Instance.LSConfig.ECAmount;
            var supAmount = LifeSupportSetup.Instance.LSConfig.SupplyAmount;
            var scrapAmount = LifeSupportSetup.Instance.LSConfig.WasteAmount;
            var repAmount = LifeSupportSetup.Instance.LSConfig.ReplacementPartAmount;
            if (part.Resources.Contains("ReplacementParts"))
            {
                recipe.Inputs.Add(new ResourceRatio { FlowMode = "ALL_VESSEL", Ratio = repAmount, ResourceName = "ReplacementParts", DumpExcess = false });
            }

            var supRatio = supAmount * recPercent;
            var mulchRatio = scrapAmount * recPercent;

            recipe.Inputs.Add(new ResourceRatio { FlowMode = "ALL_VESSEL", Ratio = ecAmount, ResourceName = "ElectricCharge", DumpExcess = true });
            recipe.Inputs.Add(new ResourceRatio { FlowMode = "ALL_VESSEL", Ratio = supRatio, ResourceName = "Supplies", DumpExcess = true });
            recipe.Outputs.Add(new ResourceRatio { FlowMode = "ALL_VESSEL", Ratio = mulchRatio, ResourceName = "Mulch", DumpExcess = true });
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
                    if(KerbalIsMissing(k))
                           LifeSupportManager.Instance.UntrackKerbal(k);
                }
            }
            catch (Exception ex )
            {
                print(String.Format("ERROR {0} IN CheckForDeadKerbals", ex.Message));
            }
        }

        private double _checkInterval = 1d;
        private double _lastProcessingTime;

        private bool CheckSupplySideEffects(LifeSupportStatus kStat)
        {
            var curTime = Planetarium.GetUniversalTime();
            var SnackMax = LifeSupportSetup.Instance.LSConfig.SupplyTime;

            var SnackTime = Math.Max(curTime - kStat.LastMeal, ResourceUtilities.FLOAT_TOLERANCE);

            if (SnackTime > SnackMax)
            {
                return true;
            }
            return false;
        }

        private bool CheckHabSideEffects(LifeSupportStatus kStat, VesselSupplyStatus vsl)
        {
            var habTime = LifeSupportManager.GetTotalHabTime(vsl);
            if (kStat.LastOnKerbin < 1)
                kStat.LastOnKerbin = Planetarium.GetUniversalTime();
            if (habTime + kStat.LastOnKerbin > kStat.MaxOffKerbinTime)
                kStat.MaxOffKerbinTime = habTime + kStat.LastOnKerbin;

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
            if(p.parent != null)
                p.decouple();
        }
    }
}


