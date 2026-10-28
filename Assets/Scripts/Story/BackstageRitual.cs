namespace ShadowTheater.Story
{
    /// <summary>Three readings, three successful rehearsal uses, then one final reading.</summary>
    public sealed class BackstageRitual
    {
        public const string ShrineId = "shrine_first_script";
        public const string ItemId = "rehearsal_script";
        public int Readings { get; private set; }
        public int Rehearsals { get; private set; }
        public bool Ready => Readings == 3 && Rehearsals == 3;
        public void Reset() { Readings = Rehearsals = 0; }
        public void Read()
        {
            if (Readings < 3) Readings++;
            else if (!Ready) { Reset(); Readings = 1; }
        }
        public void Use(string itemId)
        {
            if (Readings != 3 || itemId != ItemId || Ready) { Reset(); return; }
            Rehearsals++;
        }
    }
}
