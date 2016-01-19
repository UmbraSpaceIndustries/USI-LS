namespace LifeSupport
{
    public class ModuleLifeSupportRecycler : ModuleResourceConverter
    {
        [KSPField] 
        public int CrewCapacity = 1;

        [KSPField] public float RecyclePercent = 0f;
    }
}