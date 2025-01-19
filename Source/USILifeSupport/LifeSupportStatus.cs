namespace LifeSupport
{
    public class LifeSupportStatus
    {
        public string KerbalName { get; set; }
        public int HomeBodyId { get; set; }
        public int LastPlanet { get; set; }
        public double LastMeal { get; set; }
        public double LastEC { get; set; }
        public double LastUpdate { get; set; }
        public bool IsGrouchy { get; set; }
        public string OldTrait { get; set; }
        public double LastAtHome { get; set; }
        public double LastSOIChange { get; set; }
        public double MaxOffKerbinTime { get; set; }
        public double TimeEnteredVessel { get; set; }
        public string CurrentVesselId { get; set; }
        public string PreviousVesselId { get; set; }
        public double RemainingCabinTime { get; set; }
    }
}