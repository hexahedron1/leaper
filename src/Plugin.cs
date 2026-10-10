using BepInEx;
using BepInEx.Logging;
using System.Security.Permissions;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace TestMod;

[BepInPlugin(GUID, Name, Version)]
sealed class Plugin : BaseUnityPlugin {
    public const string GUID = "hexahedron1.leaper";
    public const string Name = "The Leaper";
    public const string Version = "0.0.0"; // TODO: version
    public new static ManualLogSource Logger;
    private static bool IsInit;

    public void OnEnable()
    {
        Logger = base.Logger;
        On.RainWorld.OnModsInit += OnModsInit;
    }

    private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (IsInit) return;
        IsInit = true;

        // Initialize assets, your mod config, and anything that uses RainWorld here
        Logger.LogDebug("Hello world!");
    }
}
