using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace LifeSupport
{
    public class ModuleLifeSupport : BaseConverter
    {
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
            var numCrew = part.protoModuleCrew.Count();
            var ecAmount = LifeSupportSetup.Instance.LSConfig.ECAmount; 
            var supAmount = LifeSupportSetup.Instance.LSConfig.SupplyAmount; 
            var scrapAmount = LifeSupportSetup.Instance.LSConfig.WasteAmount; 
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

            //How close before we assume we're done?
            var tolerance = deltaTime/2f;


            foreach (var c in part.protoModuleCrew)
            {
                //Fetch them from the queue
                var k = LifeSupportManager.Instance.FetchKerbal(c);
                //Update our stuff
                var onKerbin = (part.vessel.mainBody.name == "Kerbin" && part.vessel.altitude < LifeSupportSetup.Instance.LSConfig.HomeWorldAltitude);

                if (!onKerbin && (deltaTime - result.TimeFactor > tolerance))
                {
                    CheckSupplySideEffects(k, c);
                }
                else
                {
                    //All is well
                    k.LastMeal = lastUpdateTime;
                    v.LastFeeding = lastUpdateTime;
                    if (c.type == ProtoCrewMember.KerbalType.Tourist && k.IsGrouchy)
                    {
                        string msg = string.Format("{0} has returned to duty", c.name);
                        ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                        c.type = ProtoCrewMember.KerbalType.Crew;
                        KerbalRoster.SetExperienceTrait(c,k.OldTrait);
                        k.IsGrouchy = false;
                    }
                }
                k.LastUpdate = Planetarium.GetUniversalTime();
                LifeSupportManager.Instance.TrackKerbal(k);

                var supAmpunt = _resBroker.AmountAvailable(part, "Supplies", deltaTime, "ALL_VESSEL");
                v.SuppliesLeft = supAmpunt/0.00005f/part.vessel.GetCrewCount();
                LifeSupportManager.Instance.TrackVessel(v);
            }
        }

        private void CheckSupplySideEffects(LifeSupportStatus kStat, ProtoCrewMember crew)
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


