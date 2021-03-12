using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LifeSupport.Cryo
{

    public class ModuleCryoFreezer : PartModule
    {
        [KSPField]
        public double UnfreezeMinLifespan = 18407040d; //Two Kerbin Years

        [KSPField]
        public double UnfreezeMaxLifespan = 92035200d; //Ten Kerbin Years

        [KSPField]
        public float UnfreezeMaxFailPercent = 0.5f; //50% at ten years

        [KSPField]
        public bool CanRecharge = true; //Allows a freeze to be initialted multiple times

        [KSPField]
        public bool FreezeRequiresScientist = true; //Anywhere on the vessel

        [KSPField]
        public bool UnfreezeRequiresScientist = true; //Anywhere on the vessel


        [KSPField(isPersistant = true)]
        public string KerbalName;   //The frozen Kerbal


        [KSPField(isPersistant = true)]
        public double FreezeTime;   //When we froze them

        [KSPField(isPersistant = true)]
        public bool FirstFreeze = false;    //Was this our first time use?

        [KSPField(guiName = "CryoTank Active?", guiActive = true, isPersistant = true)]
        public bool FreezerEngaged = false; //Is the freezer on?

        [KSPField(guiName = "CryoTank Timer:", guiActive = true)]
        public string CryoTime;

        [KSPField(guiName = "CryoTank Stability:", guiActive = true)]
        public string CryoMargin;

        [KSPEvent(guiActive = true, guiName = "Enter CryoFreeze", active = true)]
        public void CryoFreeze()
        {
            ToggleFreezer(!FreezerEngaged);
        }

        private double CalculateMargin()
        {
            var totTime = Planetarium.GetUniversalTime() - FreezeTime;
            if (totTime <= UnfreezeMinLifespan)
                return 0d;

            if (totTime >= UnfreezeMaxLifespan)
                return UnfreezeMaxFailPercent;

            var maxOverTime = UnfreezeMaxLifespan - UnfreezeMinLifespan;
            var overTime = totTime - UnfreezeMinLifespan;

            var result = overTime / maxOverTime * UnfreezeMaxFailPercent;
            return result;
        }

        public override void OnUpdate()
        {
            if(FreezerEngaged)
            {
                CryoTime = LifeSupportUtilities.SmartDurationDisplay(Planetarium.GetUniversalTime() - FreezeTime);
                CryoMargin = String.Format("{0:0.00}%",(1 - CalculateMargin())*100);
            }
            else
            {
                CryoTime = "Not Active";
                CryoMargin = String.Format("{0:0.00}%", 100f);
            }
            base.OnUpdate();
        }

        private void ToggleFreezer(bool state)
        {
            FreezerEngaged = state;
            if(FreezerEngaged)
            {
                FreezeKerbals();
            }
            else
            {
                UnfreezeKerbals();

            }
            SetMenu();
        }

        public override void OnStart(StartState state)
        {
            SetMenu();
            base.OnStart(state);
        }

        private void SetMenu()
        {
            if (FreezerEngaged)
                Events["CryoFreeze"].guiName = "Unfreeze Kerbals";
            else
                Events["CryoFreeze"].guiName = "Freeze Kerbals";

            MonoUtilities.RefreshContextWindows(part); 
        }


        private void FreezeKerbals()
        {
            if(!ReadyToFreeze())  //)
            {
                FreezerEngaged = false;
                return;
            }
            else
            {
                FirstFreeze = true;
                FreezeTime = Planetarium.GetUniversalTime();
                KerbalName = "";

                var count = part.protoModuleCrew.Count();
                for (int i = count; i-- > 0;)
                {
                    var c = part.protoModuleCrew[i];
                    KerbalName += c.name + "|";
                    var msg = string.Format("{0} has been frozen", c.name);
                    vessel.CrewListSetDirty();
                    LifeSupportUtilities.RemoveCrewFromPart(c, vessel);
                    c.rosterStatus = ProtoCrewMember.RosterStatus.Missing;
                    c.SetTimeForRespawn(double.MaxValue);
                    part.RemoveCrewmember(c);
                    LifeSupportManager.Instance.UntrackKerbal(KerbalName);
                    ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                }
                KerbalName = KerbalName.TrimEnd('|');
            }
            SetMenu();
        }

        public bool DoesVesselHaveScientist()
        {
            foreach (var p in part.vessel.Parts)
            {
                if (p != part)
                {
                    var cCount = p.protoModuleCrew.Count;
                    for (int i = 0; i < cCount; ++i)
                    {
                        if (p.protoModuleCrew[i].experienceTrait.TypeName == "Scientist")
                            return true;
                    }
                }
            }
            return false;
        }

        private bool ReadyToFreeze()
        {
            if(part.protoModuleCrew.Count() == 0)
            {
                var msg = string.Format("No Kerbals present in CryoTank");
                ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }
            //Also need to do our check for re-starting a cryotank
            if(!CanRecharge && !FirstFreeze)
            {
                var msg = string.Format("This CryoTank cannot be restarted!");
                ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }
            //And do we require a scientist?
            if(FreezeRequiresScientist && !DoesVesselHaveScientist())
            {
                var msg = string.Format("No Scientist on board to operate CryoTank");
                ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }

            //Last Check = Make sure we have the resources we need.  
            //These need to be in the part.
            if(!PartHasCryoResources())
            {
                var msg = string.Format("Insufficient resources to activate CryoTank");
                ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }
            //If we made it this far, we're done!  Eat our resources and call it a day
            ConsumeCryoResources();
            return true;
        }

        public bool PartHasCryoResources()
        {
            var resModules = part.FindModulesImplementing<ModuleCryoResource>();
            if (resModules.Count() > 0)
            {
                foreach (var r in resModules)
                {
                    bool hasThisResource = false;
                    if (part.Resources.Contains(r.ResourceName))
                    {
                        var thisRes = part.Resources[r.ResourceName];
                        if (thisRes.amount >= r.ResourceQty)
                            hasThisResource = true;
                    }
                    if (!hasThisResource)
                        return false;
                }
            }
            return true;
        }

        public void ConsumeCryoResources()
        {
            var resModules = part.FindModulesImplementing<ModuleCryoResource>();
            if (resModules.Count() > 0)
            {
                foreach (var r in resModules)
                {
                    if (part.Resources.Contains(r.ResourceName))
                    {
                        var thisRes = part.Resources[r.ResourceName];
                        thisRes.amount -= r.ResourceQty;
                        if (thisRes.amount < ResourceUtilities.FLOAT_TOLERANCE)
                            thisRes.amount = 0f;
                    }
                }
            }
        }


        private void UnfreezeKerbals()
        {
            bool canThaw = true;
            if (String.IsNullOrEmpty(KerbalName))
            {
                var msg = string.Format("No frozen Kerbal in this part");
                ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                canThaw = false;
            }
            else
            {
                if (UnfreezeRequiresScientist && !DoesVesselHaveScientist())
                {
                    var msg = string.Format("CryoTank requires a Scientist to thaw a Kerbal");
                    ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                    canThaw = false;
                }
            }
            if(canThaw)
            {
                var kerbList = KerbalName.Split('|');
                foreach (var kerb in kerbList)
                {
                    ProtoCrewMember crewMember = HighLogic.CurrentGame.CrewRoster.Kerbals().ToList().Where(k => k.name == kerb).FirstOrDefault();
                    if (crewMember == null)
                    {
                        var msg = string.Format("Unable to thaw {0}", kerb);
                        ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                    }
                    else
                    {
                        if (PassDethawTest())
                        {
                            //All is well - time to thaw!
                            var msg = string.Format("{0} has been unfrozen", crewMember.name);
                            crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                            part.AddCrewmember(crewMember);
                            ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                        }
                        else
                        {
                            //Yikes!  Our Kerbal did not make it!
                            var msg = string.Format("{0} failed to survive thawing", crewMember.name);
                            crewMember.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                            ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
                        }
                    }
                }                
                part.vessel.CrewListSetDirty();
                part.vessel.MakeActive();
            }
            else
            {
                //Turn the freezer back on.
                FreezerEngaged = true;
            }
            KSP.UI.Screens.Flight.KerbalPortraitGallery.Instance.StartRefresh(vessel);
            SetMenu();
        }

        private bool PassDethawTest()
        {
            System.Random rng = new System.Random();
            var thaw = rng.Next(1, 100);
            if(thaw < ((1 - CalculateMargin()) * 100))
                return true;
            else
                return false;
        }

    }
}
