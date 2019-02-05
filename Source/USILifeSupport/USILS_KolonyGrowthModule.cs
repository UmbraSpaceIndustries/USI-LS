namespace LifeSupport
{
    public class USILS_KolonyGrowthModule : PartModule
    {
        [KSPField(guiName = "Kolony Growth", guiActive = true, guiActiveEditor = true, isPersistant = true), UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool KolonyGrowthEnabled = false;

        [KSPField(isPersistant = false, guiName = "Next Birthday")]
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

            var msg = string.Format("{0}, a new {1}, has joined your crew!", newKerb.name, newKerb.experienceTrait.TypeName);
            ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}
