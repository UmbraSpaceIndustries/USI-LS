using KSP.Localization;
namespace LifeSupport
{
    public class USILS_KolonyGrowthModule : PartModule
    {
        [KSPField(guiName = "#autoLoc_USI_LS_Growth_Growth", guiActive = true, guiActiveEditor = true, isPersistant = true), UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool KolonyGrowthEnabled = false;

        [KSPField(isPersistant = false, guiName = "#autoLoc_USI_LS_Growth_Birthday")]
        public string KerbabyCountdown;

        [KSPField(isPersistant = true)]
        public double GrowthTime = 0d;

        public const double GestationTime = 9720000d;
        private double _lastCheck = 0;

        public override void OnStart(StartState state)
        {
            _lastCheck = Planetarium.GetUniversalTime();
        }

        public override void OnUpdate()
        {
            Fields["KerbabyCountdown"].guiActive = KolonyGrowthEnabled;

            // We don't need to update kolony growth hyperactively, once per second is more than sufficient
            var now = Planetarium.GetUniversalTime();
            var elapsedTime = now - _lastCheck;

            if (elapsedTime >= 1d)
            {
                _lastCheck = now;

                if (KolonyGrowthEnabled && part.CrewCapacity > part.protoModuleCrew.Count)
                {
                    var hasMale = false;
                    var hasFemale = false;

                    var crew = vessel.GetVesselCrew();
                    var count = crew.Count;
                    for (int i = 0; i < count; ++i)
                    {
                        var c = crew[i];
                        if (c.gender == ProtoCrewMember.Gender.Male)
                            hasMale = true;
                        if (c.gender == ProtoCrewMember.Gender.Female)
                            hasFemale = true;
                    }

                    if (hasMale && hasFemale)
                    {
                        // Grow our Kolony!
                        GrowthTime += (elapsedTime * part.protoModuleCrew.Count);
                        if (GrowthTime >= GestationTime)
                        {
                            GrowthTime -= GestationTime;
                            SpawnKerbal();
                        }
                        KerbabyCountdown = LifeSupportUtilities.SmartDurationDisplay(GestationTime - GrowthTime);
                    }
                }
            }
        }

        private void SpawnKerbal()
        {
            ProtoCrewMember newKerb = HighLogic.CurrentGame.CrewRoster.GetNewKerbal();
            newKerb.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
            part.AddCrewmember(newKerb);

            var msg = Localizer.Format("#autoLoc_USI_LS_Growth_career", newKerb.name, newKerb.experienceTrait.TypeName);
            ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}
