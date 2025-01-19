namespace LifeSupport
{
    public class LifeSupportConfig
    {
        public int NoECEffect { get; set; }
        public int NoECEffectVets { get; set; }
        public float ECTime { get; set; }
        public float SupplyTime { get; set; }
        public float EVATime { get; set; }
        public float ECAmount { get; set; }
        public float SupplyAmount { get; set; }
        public float WasteAmount { get; set; }
        public float ReplacementPartAmount { get; set; }
        public int NoSupplyEffect { get; set; }
        public int NoSupplyEffectVets { get; set; }
        public int EVAEffect { get; set; }
        public int EVAEffectVets { get; set; }
        public int NoHomeEffect { get; set; }
        public int NoHomeEffectVets { get; set; }
        public int HabMultiplier { get; set; }
        public string VetNames { get; set; }
        public int HomeWorldAltitude { get; set; }
        public double BaseHabTime { get; set; }
        public bool EnableRecyclers { get; set; }
        public double HabRange { get; set; }
        public float RecoverySpeed { get; set; }
        public double ScoutHabTime { get; set; }
        public double PermaHabTime { get; set; }
    }
}