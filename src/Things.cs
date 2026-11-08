using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MoreSlugcats;
using Noise;
using RWCustom;
using SlugBase.Features;
using Smoke;
using UnityEngine;
using static SlugBase.Features.FeatureTypes;
using OpCodes = Mono.Cecil.Cil.OpCodes;
using Random = UnityEngine.Random;

namespace Leaper;

public class LeaperData {
    public class GraphicsData {
        public int tailSpecklesIndex;
        public Color eyeColor;
    }
    public float storedCharge = 5f;
    public float usingCharge;
    public bool specWasHeld;
    public LeaperSmoke smoke;
    public int lastLeap;
    public float exhaustion;
    public bool exhausted;
    public ChunkSoundEmitter sound;
}
// i love copying an entire class and just changing it up a bit
public class LeaperSmoke(Room room, PlayerGraphics pg) : SmokeSystem(LeaperEnums.LeaperSmoke, room, 2, 0.0f) {
    // all of this is copied from CyanLizardSmoke lol
    public PlayerGraphics pGraphics = pg;

    public override SmokeSystemParticle CreateParticle()
    {
        return new LeaperParticle(pGraphics);
    }
    public void EmitSmoke(Vector2 pos, Vector2 vel, bool big, PlayerGraphics pGraphics, float maxlifeTime) {
        if (!(AddParticle(pos, vel, maxlifeTime * Mathf.Lerp(0.3f, 1f, Random.value)) is LeaperParticle particle))
            return;
        particle.pGraphics = pGraphics;
        particle.big = big;
    }
    public class LeaperParticle(PlayerGraphics pg) : SpriteSmoke {
        public Color fadeColor;
        public PlayerGraphics pGraphics = pg;

        public Color eyeColor => Hooks.TryGetGraphicsData(pGraphics, out var data) ? data.eyeColor : new Color(0.66f, 0.99f, 0.99f); /*{
            get {
                if ()
                    return data.eyeColor;
                return null;
            }
        }*/
        public float moveDir;
        public int counter;
        public bool big;
        public override void Reset(SmokeSystem newOwner, Vector2 pos, Vector2 vel, float newLifeTime) {
            base.Reset(newOwner, pos, vel, newLifeTime);
            rad = Mathf.Lerp(28f, 46f, Random.value);
            moveDir = Random.value * 360f;
            counter = 0;
        }
        public override void Update(bool eu)  {
            base.Update(eu);
            if (resting)
                return;
            vel *= (float) (0.699999988079071 + 0.30000001192092896 / Mathf.Pow(vel.magnitude, 0.5f));
            moveDir += Mathf.Lerp(-1f, 1f, Random.value) * 50f;
            if (room.PointSubmerged(pos))
                pos.y = room.FloatWaterLevel(pos);
            ++counter;
            if (!room.GetTile(pos).Solid || room.GetTile(lastPos).Solid)
                return;
            FloatRect floatRect = Custom.RectCollision(pos, lastPos, room.TileRect(SharedPhysics.RayTraceTilesForTerrainReturnFirstSolid(room, room.GetTilePosition(lastPos), room.GetTilePosition(pos)).Value).Grow(2f));
            pos = floatRect.GetCorner(FloatRect.CornerLabel.D);
            if (floatRect.GetCorner(FloatRect.CornerLabel.B).x < 0.0)
                vel.x = Mathf.Abs(vel.x);
            else if (floatRect.GetCorner(FloatRect.CornerLabel.B).x > 0.0)
                vel.x = -Mathf.Abs(vel.x);
            else if (floatRect.GetCorner(FloatRect.CornerLabel.B).y < 0.0)
                vel.y = Mathf.Abs(vel.y);
            else {
                if (floatRect.GetCorner(FloatRect.CornerLabel.B).y <= 0.0)
                    return;
                vel.y = -Mathf.Abs(vel.y);
            }
        }
        public override float Rad(int type, float useLife, float useStretched, float timeStacker) {
            float num = type switch {
                0 => Mathf.Lerp(4f, rad, Mathf.Pow(1f - useLife, 0.2f) + useStretched),
                1 => 1.5f * Mathf.Lerp(2f, rad, Mathf.Pow(1f - useLife, 0.2f)),
                _ => Mathf.Lerp(4f, rad, Mathf.Pow(1f - useLife, 0.2f))
            };
            return !big ? num * 0.2f : num * (1f + Mathf.InverseLerp(0.0f, 10f, counter + timeStacker));
        }
        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam) {
            base.InitiateSprites(sLeaser, rCam);
            for (int index = 0; index < 2; ++index)
                sLeaser.sprites[index].shader = this.room.game.rainWorld.Shaders["FireSmoke"];
        }
        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos) {
            base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
            if (resting)
                return;
            float f = Mathf.Lerp(lastLife, life, timeStacker);
            Color color = !big ? Color.Lerp(eyeColor, fadeColor, Mathf.InverseLerp(1f, 0.25f, f) * 0.5f) : Color.Lerp(eyeColor, fadeColor, Mathf.InverseLerp(1f, 0.25f, f));
            sLeaser.sprites[0].color = color;
            sLeaser.sprites[1].color = color;
            sLeaser.sprites[0].alpha = (float) (Mathf.Pow(f, 0.25f) * (1.0 - stretched) * (big ? 1.0 - 0.20000000298023224 * Mathf.InverseLerp(0.0f, 10f, counter + timeStacker) : 1.0));
            sLeaser.sprites[1].alpha = (float) ((0.30000001192092896 + Mathf.Pow(Mathf.Sin(f * 3.1415927f), 0.7f) * 0.6499999761581421 * (1.0 - stretched)) * (big ? 1.0 - 0.20000000298023224 * Mathf.InverseLerp(0.0f, 10f, counter + timeStacker) : 1.0));
        }
        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette) {
            fadeColor = Color.Lerp(palette.blackColor, palette.fogColor, 0.6f);
        }
    }
}
// ditto
public class LeapLight : CosmeticSprite {
    public float life;
    public float lastLife;
    public float lifeTime;
    public float intensity;
    public PlayerGraphics pGraphics;
    public LeapLight(Vector2 p, PlayerGraphics pg, float intensity) {
        life = 1f;
        lastLife = 1f;
        pos = p;
        lastPos = p;
        pGraphics = pg;
        lifeTime = Mathf.Lerp(4f, 22f, Mathf.Pow(intensity, 2f));
    }
    public override void Update(bool eu)
    {
        lastLife = life;
        life -= 1f / lifeTime;
        if (lastLife <= 0.0)
            Destroy();
        base.Update(eu);
    }
    public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam) {
        sLeaser.sprites = new FSprite[3];
        for (int index = 0; index < sLeaser.sprites.Length; ++index)
            sLeaser.sprites[index] = new FSprite("Futile_White");
        sLeaser.sprites[0].shader = rCam.game.rainWorld.Shaders["LightSource"];
        sLeaser.sprites[1].shader = rCam.game.rainWorld.Shaders["FlatLight"];
        sLeaser.sprites[2].shader = rCam.game.rainWorld.Shaders["FlatLight"];
        AddToContainer(sLeaser, rCam, null);
    }
    public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos) {
        Vector2 vector2 = Vector2.Lerp(lastPos, pos, timeStacker);
        float num1 = Mathf.Lerp(lastLife, life, timeStacker);
        foreach (var t in sLeaser.sprites) {
            t.x = vector2.x - camPos.x;
            t.y = vector2.y - camPos.y;
        }
        var data = Hooks.pGraphicsCWT.GetOrCreateValue(pGraphics);
        float num2 = Mathf.Lerp((float) (20.0 + 10.0 * intensity), (float) (40.0 + 30.0 * intensity), num1) + Mathf.Lerp(50f, 90f, intensity) * Mathf.Sin(Mathf.Pow(num1, 2f) * 3.1415927f);
        sLeaser.sprites[0].color = data.eyeColor;
        sLeaser.sprites[0].scale = (float) (num2 * 2.0 / 8.0);
        sLeaser.sprites[0].alpha = Mathf.Pow(Mathf.InverseLerp(0.0f, 0.5f, num1), 0.5f);
        sLeaser.sprites[1].color = data.eyeColor;
        sLeaser.sprites[1].scale = num2 / 8f;
        sLeaser.sprites[1].alpha = Mathf.Pow(num1, 2f) * (float) (0.4000000059604645 + 0.4000000059604645 * intensity);
        sLeaser.sprites[2].scale = (float) (num2 * (double) Mathf.Lerp(0.4f, 0.8f, Random.value) / 8.0);
        sLeaser.sprites[2].alpha = Mathf.Pow(Mathf.InverseLerp(0.25f, 1f, num1), 3f) * intensity;
        base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
    }
    public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette) { }

    public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner) {
        newContatiner ??= rCam.ReturnFContainer("Bloom");
        foreach (FSprite sprite in sLeaser.sprites) {
            sprite.RemoveFromContainer();
            newContatiner.AddChild(sprite);
        }
    }
}
public class LeaperEnums {
    public static SlugcatStats.Name Leaper = new("leaper");
    public static SmokeSystem.SmokeType LeaperSmoke = new(nameof(LeaperSmoke), true);
    public static SoundID ChargeLoop;
}

[SuppressMessage("ReSharper", "ObjectCreationAsStatement")]
public class Hooks {
    public static float chargeDepleteRate = 0.2f;
    private static readonly PlayerFeature<float> LeapCharge = PlayerFloat("leap_charge");
    public static readonly ConditionalWeakTable<PlayerGraphics, LeaperData.GraphicsData> pGraphicsCWT = new();
    public static readonly ConditionalWeakTable<Player, LeaperData> playerCWT = new();
    public static bool TryGetGraphicsData(PlayerGraphics key, out LeaperData.GraphicsData data) {
        data = key != null ? pGraphicsCWT.GetOrCreateValue(key) : null;
        return data != null;
    }
    public static bool TryGetData(Player key, out LeaperData data) {
        data = key != null ? playerCWT.GetOrCreateValue(key) : null;
        return data != null;
    }

    public static void Unapply() {
        On.PlayerGraphics.ctor -= On_PlayerGraphics_ctor;
        On.PlayerGraphics.InitiateSprites -= On_PlayerGraphics_InitiateSprices;
        On.PlayerGraphics.DrawSprites -= On_PlayerGraphics_DrawSprites;
        On.PlayerGraphics.AddToContainer -= On_PlayerGraphics_AddToContainer;
        On.PlayerGraphics.TailSpeckles.DrawSprites -= On_TailSpeckles_DrawSprites;
        On.SaveState.ctor -= On_SaveState_ctor;
        On.SLOrcacleState.ForceResetState -= On_SLOrcacleState_ForceResetState;
        On.Player.ctor -= On_Player_ctor;
        On.Player.Update -= On_Player_Update;
        On.HUD.HUD.InitSinglePlayerHud -= On_HUD_InitSinglePlayerHud;
        IL.MoreSlugcats.BreathMeter.Update -= IL_BreathMeter_Update;
        LeaperEnums.Leaper.Unregister();
        LeaperEnums.LeaperSmoke.Unregister();
    }

    public static void Apply() {
        try {
            On.PlayerGraphics.ctor += On_PlayerGraphics_ctor;
            On.PlayerGraphics.InitiateSprites += On_PlayerGraphics_InitiateSprices;
            On.PlayerGraphics.DrawSprites += On_PlayerGraphics_DrawSprites;
            On.PlayerGraphics.AddToContainer += On_PlayerGraphics_AddToContainer;
            On.PlayerGraphics.TailSpeckles.DrawSprites += On_TailSpeckles_DrawSprites;
            On.SaveState.ctor += On_SaveState_ctor;
            On.SLOrcacleState.ForceResetState += On_SLOrcacleState_ForceResetState;
            On.Player.ctor += On_Player_ctor;
            On.Player.Update += On_Player_Update;
            On.HUD.HUD.InitSinglePlayerHud += On_HUD_InitSinglePlayerHud;
            IL.MoreSlugcats.BreathMeter.Update += IL_BreathMeter_Update;
            On.DebugMouse.Update += On_DebugMouse_Updae;
            Plugin.Logger.LogDebug("trying to hook Breathmeter.Show.get");
            foreach (var propertyInfo in typeof(BreathMeter).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic)) {
                Plugin.Logger.LogDebug(propertyInfo.Name);
            }
            new Hook(typeof(BreathMeter).GetProperty(nameof(BreathMeter.Show), BindingFlags.Instance | BindingFlags.NonPublic)!.GetGetMethod(true), MH_BreathMeter_Show);
        }
        catch (Exception e) {
            Plugin.Logger.LogError("FAILED TO HOOK SHIT");
            Plugin.Logger.LogError(e);
        }
    }

    public static bool MH_BreathMeter_Show(Func<BreathMeter, bool> orig, BreathMeter self) {
        if (TryGetData(self.hudPlayer, out var data) && LeapCharge.TryGet(self.hudPlayer, out float charge) && charge > 0) return orig(self) || data.storedCharge < charge;
        return orig(self);
    }

    private static void On_DebugMouse_Updae(On.DebugMouse.orig_Update orig, DebugMouse self, bool eu) {
        orig(self, eu);
        if (!TryGetData(self.room.game.FirstRealizedPlayer, out var data) || !LeapCharge.TryGet(self.room.game.FirstRealizedPlayer, out float charge) || charge <= 0) return;
        string txt = self.label.text;
        txt += $"\nStored charge: {data.storedCharge}\nUsed charge: {data.usingCharge}\nJump exhaustion: {data.exhaustion}";
        self.label.text = txt;
        self.label2.text = txt;
    }

    private static void IL_BreathMeter_Update(ILContext il) {
        try {
            ILCursor c = new ILCursor(il);
            c.GotoNext(MoveType.After, x => x.MatchStloc(2));
            c.GotoNext(MoveType.After, x => x.MatchStloc(2));
            c.MoveAfterLabels();
            c.Emit(OpCodes.Ldloc, 2);
            c.Emit(OpCodes.Ldarg, 0);
            c.Emit(OpCodes.Call, typeof(BreathMeter).GetMethod("get_hudPlayer"));
            c.EmitDelegate(GetLeaperStoredCharge);
            c.Emit(OpCodes.Stloc, 2);
            Plugin.Logger.LogInfo("Successfully IL hooked BreathMeter.Update");
        }
        catch (Exception e) {
            Plugin.Logger.LogError("FAILED TO IL HOOK BreathMeter.Update");
            Plugin.Logger.LogError(e);
        }
    }

    static float GetLeaperStoredCharge(float prev, Player player) {
        return player != null
               && LeapCharge.TryGet(player, out float charge)
               && charge > 0
               && TryGetData(player, out var data)
               && !player.submerged
            ? data.storedCharge/charge : prev;
    }

    private static void On_HUD_InitSinglePlayerHud(On.HUD.HUD.orig_InitSinglePlayerHud orig, HUD.HUD self, RoomCamera cam) {
        orig(self, cam);
        if ((!ModManager.MMF || !MMF.cfgBreathTimeVisualIndicator.Value) && !ModManager.CoopAvailable)
            self.AddPart(new BreathMeter(self, self.fContainers[1]));
    }

    private static void On_Player_Update(On.Player.orig_Update orig, Player self, bool eu) {
        orig(self, eu);
        if (!TryGetData(self, out var data) || !LeapCharge.TryGet(self, out float charge) || charge <= 0) return;
        if (self.input[0].spec) {
            if (data.storedCharge >= chargeDepleteRate) {
                data.storedCharge -= chargeDepleteRate;
                data.usingCharge += chargeDepleteRate;
            }
            if (data.sound != null) {
                data.sound.alive = true;
                data.sound.volume = data.usingCharge / charge;
            } else {
                data.sound = self.room.PlaySound(LeaperEnums.ChargeLoop, self.mainBodyChunk, true, 0f, 1f);
                data.sound.requireActiveUpkeep = true;
            }
        }
        else if (data.sound != null) {
            data.sound.alive = false;
            data.sound = null;
        }
        if (!self.input[0].spec) {
            if (data.specWasHeld && self.canJump <= 0 && self.bodyMode != Player.BodyModeIndex.CorridorClimb) {
                if (data.usingCharge < 1f && data.storedCharge >= 1-data.usingCharge) {
                    //Plugin.Logger.LogDebug(1f - data.usingCharge);
                    data.storedCharge -= 1f - data.usingCharge;
                    data.usingCharge = 1f;
                }
                if (data.smoke != null && data.smoke.room != self.room) {
                    data.smoke.Destroy();
                    data.smoke = null;
                }

                if (data.smoke == null) {
                    data.smoke = new LeaperSmoke(self.room, (PlayerGraphics)self.graphicsModule);
                    self.room.AddObject(data.smoke);
                }
                if (data.exhausted)
                    data.usingCharge *= Mathf.Lerp(1, 0.5f, Mathf.Min(0.2f - data.exhaustion/1.5f, 1));
                if (data.usingCharge >= 1f) {
                    switch (data.usingCharge) {
                        case <= 5 / 3f:
                            self.room.PlaySound(SoundID.Cyan_Lizard_Small_Jump, self.mainBodyChunk);
                            break;
                        case <= 10 / 3f:
                            self.room.PlaySound(SoundID.Cyan_Lizard_Medium_Jump, self.mainBodyChunk);
                            break;
                        default:
                            self.room.PlaySound(SoundID.Cyan_Lizard_Powerful_Jump, self.mainBodyChunk);
                            break;
                    }
                    
                    if (self.graphicsModule is PlayerGraphics pg) {
                        self.room.InGameNoise(new InGameNoise(pg.tail[2].pos, 3000f * data.usingCharge / charge, self,
                            1f));
                        for (int i = 0; i < 8 * data.usingCharge / charge; ++i)
                            data.smoke.EmitSmoke(pg.tail[2].pos,
                                Custom.RNV() * Mathf.Lerp(6f, 23f, data.usingCharge / charge), true, pg,
                                Mathf.Lerp(30f, 160f, data.usingCharge / charge));
                        self.room.AddObject(new ShockWave(pg.tail[2].pos,
                            Mathf.Lerp(40f, 120f, data.usingCharge / charge), 0.07f,
                            6 + (int)((double)data.usingCharge / charge * 4.0)));
                        self.room.AddObject(new LeapLight(pg.tail[2].pos, pg, data.usingCharge/charge));
                    }

                    float x = self.input[0].x;
                    float y = self.input[0].y;
                    if (x == 0 && y == 0) y = 1f;
                    else if (y == 0) y = 0.2f;
                    self.bodyChunks[0].vel.x = Mathf.Lerp(5f, 20f, data.usingCharge / charge) * x;
                    self.bodyChunks[0].vel.y = Mathf.Lerp(9f, 30f, data.usingCharge / charge) * y;
                    self.bodyChunks[1].vel.x = Mathf.Lerp(4f, 19f, data.usingCharge / charge) * x;
                    self.bodyChunks[1].vel.y = Mathf.Lerp(8f, 29f, data.usingCharge / charge) * y;
                    data.lastLeap = self.room.game.timeInRegionThisCycle;
                    self.animation = Player.AnimationIndex.Flip;
                    self.bodyMode = Player.BodyModeIndex.Default;
                    data.exhaustion += data.usingCharge / 8f;
                    if (data.exhaustion >= 2f) {
                        self.Stun(40 + (int)data.usingCharge*20);
                    }
                    data.usingCharge = 0f;
                }
                else if (self.graphicsModule is PlayerGraphics pg)
                    data.smoke.EmitSmoke(pg.tail[2].pos, Custom.RNV() * Mathf.Lerp(6f, 23f, data.usingCharge / charge), true, pg, Mathf.Lerp(30f, 160f, data.usingCharge / charge));
            } else if (data.usingCharge >= chargeDepleteRate) {
                data.storedCharge += chargeDepleteRate;
                data.usingCharge -= chargeDepleteRate;
            } else if (data.storedCharge <= charge - chargeDepleteRate / 12 && self.room.game.timeInRegionThisCycle - data.lastLeap > 60) {
                data.storedCharge += chargeDepleteRate / (self.Malnourished ? 12 : 6);
                if (data.storedCharge > charge - chargeDepleteRate / (self.Malnourished ? 12 : 6))
                    data.storedCharge = charge;
            }
        }
        if (data.exhaustion > 0) data.exhaustion -= 0.001f * (data.exhaustion >= 2f ? 1.3f : Mathf.Lerp(0.6f, 1, Math.Min(data.exhaustion, 1))) * (self.bodyMode == Player.BodyModeIndex.Crawl ? 2 : 1);
        if (data.exhaustion >= 1f) {
            data.exhausted = true;
        } else if (data.exhausted && data.exhaustion < 0.2f && !self.Malnourished)
            data.exhausted = false;
        if (data.exhausted) {
            self.exhausted = true;
            self.lungsExhausted = true;
        }
        if (data.exhaustion > 3f)
            self.Stun(20);
        data.specWasHeld = self.input[0].spec;
    }

    private static void On_Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world) {
        orig(self, abstractCreature, world);
        if (!TryGetData(self, out var data) || !LeapCharge.TryGet(self, out float charge) || charge <= 0) return;
        data.storedCharge = charge;
    }

    private static void On_SLOrcacleState_ForceResetState(On.SLOrcacleState.orig_ForceResetState orig, SLOrcacleState self, SlugcatStats.Name saveStateNumber) {
        orig(self, saveStateNumber);
        if (saveStateNumber != LeaperEnums.Leaper) return;
        self.neuronsLeft = 7;
    }

    private static void On_SaveState_ctor(On.SaveState.orig_ctor orig, SaveState self, SlugcatStats.Name saveStateNumber, PlayerProgression progression) {
        orig(self, saveStateNumber, progression);
        if (saveStateNumber != LeaperEnums.Leaper) return;
        self.miscWorldSaveData.moonGivenRobe = true;
        self.miscWorldSaveData.moonHeartRestored = true;
        self.miscWorldSaveData.pebblesEnergyTaken = true;
        self.miscWorldSaveData.pebblesRivuletPostgame = true;
    }

    private static void On_TailSpeckles_DrawSprites(On.PlayerGraphics.TailSpeckles.orig_DrawSprites orig, PlayerGraphics.TailSpeckles self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos) {
        orig(self, sLeaser, rCam, timeStacker, camPos);
        if (!LeapCharge.TryGet(self.pGraphics.player, out float charge) || charge <= 0 ||
            !TryGetData(self.pGraphics.player, out var data)) return;
        for (int i = 0; i < self.rows; i++) {
            for (int j = 0; j < self.lines; j++) {
                sLeaser.sprites[self.startSprite + i * self.lines + j].scaleX = 1 + 0.5f * (data.usingCharge / charge);
                sLeaser.sprites[self.startSprite + i * self.lines + j].scaleY = 1 + 0.5f * (data.usingCharge / charge);
            }
        }
    }

    private static void On_PlayerGraphics_AddToContainer(On.PlayerGraphics.orig_AddToContainer orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner) {
        orig(self, sLeaser, rCam, newContatiner);
        if (!TryGetGraphicsData(self, out var data) || data.tailSpecklesIndex == 0 || sLeaser.sprites.Length <= data.tailSpecklesIndex || !LeapCharge.TryGet(self.player, out float charge) || charge <= 0) return;
        newContatiner ??= rCam.ReturnFContainer("Midground");
        self.tailSpecks.AddToContainer(sLeaser, rCam, newContatiner);
    }

    private static void On_PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos) {
        orig(self, sLeaser, rCam, timeStacker, camPos);
        if (self.player.room == null || !LeapCharge.TryGet(self.player, out float charge) || charge <= 0) return;
        self.tailSpecks.DrawSprites(sLeaser, rCam, timeStacker, camPos);
        if (!TryGetGraphicsData(self, out var data)) return;
        data.eyeColor = sLeaser.sprites[9].color;
    }

    private static void On_PlayerGraphics_InitiateSprices(On.PlayerGraphics.orig_InitiateSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam) {
        orig(self, sLeaser, rCam);
        if (!TryGetGraphicsData(self, out var data) || !LeapCharge.TryGet(self.player, out float charge) || charge <= 0) return;
        data.tailSpecklesIndex = sLeaser.sprites.Length;
        self.tailSpecks = new PlayerGraphics.TailSpeckles(self, data.tailSpecklesIndex);
        Array.Resize(ref sLeaser.sprites, sLeaser.sprites.Length + self.tailSpecks.numberOfSprites);
        self.tailSpecks.InitiateSprites(sLeaser, rCam);
        for (int i = 0; i < self.tailSpecks.numberOfSprites; i++) {
            sLeaser.sprites[data.tailSpecklesIndex + i].MoveInFrontOfOtherNode(sLeaser.sprites[2]);
        }
        self.AddToContainer(sLeaser, rCam, null);
    }

    private static void On_PlayerGraphics_ctor(On.PlayerGraphics.orig_ctor orig, PlayerGraphics self, PhysicalObject ow) {
        orig(self, ow);
        if (!LeapCharge.TryGet(self.player, out float charge) || charge <= 0) return;
        if (self.player.playerState.isPup) {
            self.tail[0] = new TailSegment(self, 8f, 2f, null, 0.85f, 1f, 1f, true);
            self.tail[1] = new TailSegment(self, 6f, 3.5f, self.tail[0], 0.85f, 1f, 0.5f, true);
            self.tail[2] = new TailSegment(self, 4f, 3.5f, self.tail[1], 0.85f, 1f, 0.5f, true);
            self.tail[3] = new TailSegment(self, 2f, 3.5f, self.tail[2], 0.85f, 1f, 0.5f, true);
        } else {
            self.tail[0] = new TailSegment(self, 8f, 4f, null, 0.85f, 1f, 1f, true);
            self.tail[1] = new TailSegment(self, 6f, 7f, self.tail[0], 0.85f, 1f, 0.5f, true);
            self.tail[2] = new TailSegment(self, 4f, 7f, self.tail[1], 0.85f, 1f, 0.5f, true);
            self.tail[3] = new TailSegment(self, 2f, 7f, self.tail[2], 0.85f, 1f, 0.5f, true);
        }
    }
}