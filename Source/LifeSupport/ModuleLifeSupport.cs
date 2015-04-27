using System;
using System.Linq;
using System.Text;
using UnityEngine;

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
            }
        }

        protected override float GetHeatMultiplier(ConverterResults result, double deltaTime)
        {
            //No need for heat generation
            return 0f;
        }

        protected override ConversionRecipe PrepareRecipe(double deltatime)
        {
            //This is where the rubber hits the road.  Let us see if we can
            //keep our Kerbals cozy and warm.
            var recipe = new ConversionRecipe();
            var numCrew = part.protoModuleCrew.Count();
            var ecAmount = 0.01f;
            var supAmount = 0.00005f;
            var scrapAmount = 0.00005f;
            recipe.Inputs.Add(new ResourceRatio { FlowMode = "ALL_VESSEL", Ratio = ecAmount * numCrew, ResourceName = "ElectricCharge", DumpExcess = true });
            recipe.Inputs.Add(new ResourceRatio { FlowMode = "ALL_VESSEL", Ratio = supAmount * numCrew, ResourceName = "Supplies", DumpExcess = true });
            recipe.Outputs.Add(new ResourceRatio { FlowMode = "ALL_VESSEL", Ratio = scrapAmount * numCrew, ResourceName = "Mulch", DumpExcess = true });
            return recipe;
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
                var onKerbin = (part.vessel.mainBody.name == "Kerbin" && part.vessel.altitude < 50000);

                if (!onKerbin && (deltaTime - result.TimeFactor > tolerance))
                {
                    //Sadness
                    CheckSideEffects(k, c);
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
                        KerbalRoster.SetExperienceTrait(c, "");
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

        private void CheckSideEffects(LifeSupportStatus kStat, ProtoCrewMember crew)
        {
            var curTime = Planetarium.GetUniversalTime();
            var SnackMax = LifeSupportSetup.Instance.LSConfig.SupplyTime;

            var SnackTime = Math.Max(curTime - kStat.LastMeal, ResourceUtilities.FLOAT_TOLERANCE);

            if (SnackTime > SnackMax)
            {
                //If we are past the final threshold, we will start by trying to unlock any biscuit tins left on the ship.
                
                foreach (var rp in vessel.parts.Where(p => p.Resources.Contains("Supplies")))
                {
                    var resAmt = rp.Resources["Supplies"].amount;
                    if (resAmt > 0)
                    {
                        rp.Resources["Supplies"].amount = 0;
                        string msg = string.Format("{0} raids the ship for snacks...  some accidentally fell out of the airlock.", crew.name);
                        kStat.LastMeal = lastUpdateTime;
                        ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                        LifeSupportManager.Instance.TrackKerbal(kStat);
                        //At this point, the Kerbal is saved!
                        return;
                    }
                }

                if (LifeSupportSetup.Instance.LSConfig.CausesDeath)
                {
                    string msg = string.Format("{0} has died of starvation", crew.name);
                    LifeSupportManager.Instance.UntrackKerbal(crew.name);
                    ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                    part.RemoveCrewmember(crew);
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                    return;
                }

                //Vacation time!  The lone exception are our badasses.
                if (crew.isBadass)
                    return;

                if (crew.type != ProtoCrewMember.KerbalType.Tourist)
                {
                    string msg = string.Format("{0} refuses to work.", crew.name);
                    ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                    crew.type = ProtoCrewMember.KerbalType.Tourist;
                    KerbalRoster.SetExperienceTrait(crew, "Tourist");
                    kStat.IsGrouchy = true;
                    LifeSupportManager.Instance.TrackKerbal(kStat);
                }
            }
        }
    }
}


