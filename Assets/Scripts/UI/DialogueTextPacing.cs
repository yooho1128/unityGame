namespace ShadowTheater.UI
{
    /// <summary>Shared punctuation timing for story text and the settings preview.</summary>
    public static class DialogueTextPacing
    {
        public static float PauseAfter(char character, bool lastCharacter)
        {
            if (character == ',' || character == '，') return .07f;
            if (character == '…' || character == '—') return .19f;
            if (!lastCharacter && (character == '.' || character == '?' || character == '!')) return .12f;
            return 0f;
        }
    }
}
