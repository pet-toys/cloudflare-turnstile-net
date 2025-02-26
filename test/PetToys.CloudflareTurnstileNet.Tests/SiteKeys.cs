namespace PetToys.CloudflareTurnstileNet.Tests;

internal static class SiteKeys
{
    public const string AlwaysPassesVisible = "1x00000000000000000000AA";
    public const string AlwaysBlocksVisible = "2x00000000000000000000AB";
    public const string AlwaysPassesInvisible = "1x00000000000000000000BB";
    public const string AlwaysBlocksInvisible = "2x00000000000000000000BB";
    public const string ForcesInteractiveVisible = "3x00000000000000000000FF";
}
