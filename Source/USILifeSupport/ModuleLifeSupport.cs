using System;
using Random = System.Random;

namespace LifeSupport
{
    public class ModuleLifeSupport : BaseConverter
    {
        [KSPField(guiActive = true, guiName = "Wear")] 
        public string wearPercent;
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsFlight)
            {
                var v = new VesselSupplyStatus();
                v.VesselId = part.vessel.id.ToString();
                LifeSupportManager.Instance.TrackVessel(v);
                Fields["status"].guiActive = false;
                IsActivated = true;
            }
        }

        protected override ConversionRecipe PrepareRecipe(double deltatime)
        {
            //This is where the rubber hits the road.  Let us see if we can
            //keep our Kerbals cozy and warm.
            var recipe = new ConversionRecipe();
            var numCrew = part.protoModuleCrew.Count;
            var ecAmount = LifeSupportSetup.Instance.LSConfig.ECAmount; 
            var supAmount = LifeSupportSetup.Instance.LSConfig.SupplyAmount; 
            var scrapAmount = LifeSupportSetup.Instance.LSConfig.WasteAmount;
            var repAmount = LifeSupportSetup.Instance.LSConfig.ReplacementPartAmount;

            if (part.Resources.Contains("ReplacementParts"))
            {
                recipe.Inputs.Add(new ResourceRatio { FlowMode = "ALL_VESSEL", Ratio = repAmount * numCrew, ResourceName = "ReplacementParts", DumpExcess = false });
            }

            recipe.Inputs.Add(new ResourceRatio { FlowMode = "ALL_VESSEL", Ratio = ecAmount * numCrew, ResourceName = "ElectricCharge", DumpExcess = true });
            recipe.Inputs.Add(new ResourceRatio { FlowMode = "ALL_VESSEL", Ratio = supAmount * numCrew, ResourceName = "Supplies", DumpExcess = true });
            recipe.Outputs.Add(new ResourceRatio { FlowMode = "ALL_VESSEL", Ratio = scrapAmount * numCrew, ResourceName = "Mulch", DumpExcess = true });
            return recipe;
        }


        protected override void PreProcessing()
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

        public override bool IsSituationValid()
        {
            //Disable life support if we are in a pre-launch state.
            //return (part.vessel.situation != Vessel.Situations.PRELAUNCH);
            return true;
        }

        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            var v = LifeSupportManager.Instance.FetchVessel(part.vessel.id.ToString());
            v.LastUpdate = Planetarium.GetUniversalTime();
            v.VesselName = part.vessel.vesselName;
            v.NumCrew = part.vessel.GetCrewCount();
            v.CrewCap = part.vessel.GetCrewCapacity();

            //Update Hab info
            var habMulti = 1d;
            var habTime = 0d;
            var totParts = 0d;
            var maxParts = 0d;
            var habMods = part.vessel.FindPartModulesImplementing<ModuleHabitation>();
            foreach (var hab in habMods)
            {
                //Next.  Certain modules, in addition to crew capacity, have living space.
                habTime += hab.KerbalMonths;
                //Lastly.  Some modules act more as 'multipliers', dramatically extending a hab's workable lifespan.
                habMulti += hab.HabMultiplier;
            }

            v.HabSpace = habTime;
            v.HabMultiplier = habMulti;
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
                v.HabMultiplier *= (totParts/maxParts);
                if (totParts < 1)
                    wearPercent = "Broken!";
                else
                    wearPercent = String.Format("{0:0.00}%", (1d - (totParts/maxParts))*100);

            }
            else
            {
                wearPercent = "Like New";
            }

            //How close before we assume we're done?
            var tolerance = deltaTime/2f;

            foreach (var c in part.protoModuleCrew)
            {
                bool isGrouchyHab = false;
                bool isGrouchySupplies = false;

                //Fetch them from the queue
                var k = LifeSupportManager.Instance.FetchKerbal(c);
                //Update our stuff
                var onKerbin = (part.vessel.mainBody == FlightGlobals.GetHomeBody() && part.vessel.altitude < LifeSupportSetup.Instance.LSConfig.HomeWorldAltitude);

                //First - Hab effects.
                if (onKerbin)
                {
                    k.LastOnKerbin = Planetarium.GetUniversalTime();
                    k.MaxOffKerbinTime = Planetarium.GetUniversalTime() + 648000;
                    k.TimeInVessel = 0d;
                }
                else
                {
                    if (part.vessel.id.ToString() != k.LastVesselId)
                    {
                        k.LastVesselId = part.vessel.id.ToString();
                        k.TimeInVessel = 0d;
                    }
                }
                isGrouchyHab = CheckHabSideEffects(k, c, v);

                //Second - Supply
                if (!onKerbin && (deltaTime - result.TimeFactor > tolerance))
                {
                    isGrouchySupplies = CheckSupplySideEffects(k, c);
                }
                else
                {
                    //All is well
                    k.LastMeal = lastUpdateTime;
                    v.LastFeeding = lastUpdateTime;
                }

                k.LastUpdate = Planetarium.GetUniversalTime();
                if(!isGrouchyHab && !isGrouchySupplies)
                    RemoveGrouchiness(c,k);
                LifeSupportManager.Instance.TrackKerbal(k);
                var supAmpunt = _resBroker.AmountAvailable(part, "Supplies", deltaTime, "ALL_VESSEL");
                v.SuppliesLeft = supAmpunt/LifeSupportSetup.Instance.LSConfig.SupplyAmount/part.vessel.GetCrewCount()/LifeSupportManager.GetRecyclerMultiplier(vessel);
            }
            LifeSupportManager.Instance.TrackVessel(v);
        }

        private bool CheckSupplySideEffects(LifeSupportStatus kStat, ProtoCrewMember crew)
        {
            var curTime = Planetarium.GetUniversalTime();
            var SnackMax = LifeSupportSetup.Instance.LSConfig.SupplyTime;

            var SnackTime = Math.Max(curTime - kStat.LastMeal, ResourceUtilities.FLOAT_TOLERANCE);

            if (SnackTime > SnackMax)
            {
                ApplyEffect(kStat, crew,
                    LifeSupportManager.isVet(kStat.KerbalName)
                        ? LifeSupportSetup.Instance.LSConfig.NoSupplyEffectVets
                        : LifeSupportSetup.Instance.LSConfig.NoSupplyEffect);
                return true;
            }
            return false;
        }

        private bool CheckHabSideEffects(LifeSupportStatus kStat, ProtoCrewMember crew, VesselSupplyStatus vsl)
        {
            var habTime = LifeSupportManager.GetHabtime(vsl);
            if (kStat.LastOnKerbin < 1)
                kStat.LastOnKerbin = Planetarium.GetUniversalTime();
            if (habTime + kStat.LastOnKerbin > kStat.MaxOffKerbinTime)
                kStat.MaxOffKerbinTime = habTime + kStat.LastOnKerbin;

            if (Planetarium.GetUniversalTime() > kStat.MaxOffKerbinTime || kStat.TimeInVessel > habTime)
            {
                ApplyEffect(kStat, crew,
                    LifeSupportManager.isVet(kStat.KerbalName)
                        ? LifeSupportSetup.Instance.LSConfig.NoHomeEffectVets
                        : LifeSupportSetup.Instance.LSConfig.NoHomeEffect);
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
                        crew.type = ProtoCrewMember.KerbalType.Tourist;
                        kStat.OldTrait = crew.experienceTrait.Title;
                        KerbalRoster.SetExperienceTrait(crew, "Tourist");
                        kStat.IsGrouchy = true;
                        LifeSupportManager.Instance.TrackKerbal(kStat);
                    }
                    break;
                case 2:  //Mutinous
                    {
                        msg = string.Format("{0} has become mutinous", crew.name);
                        crew.type = ProtoCrewMember.KerbalType.Tourist;
                        kStat.OldTrait = crew.experienceTrait.Title;
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


