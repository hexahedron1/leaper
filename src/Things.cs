using System;
using System.Linq;
using System.Runtime.CompilerServices;
using MonoMod.Cil;
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
    }
    public float storedCharge = 5f;
    public float usingCharge;
    public bool specWasHeld;
    public LeaperSmoke smoke;
}

public class LeaperSmoke(Room room, Color col) : SmokeSystem(LeaperEnums.LeaperSmoke, room, 2, 0.0f) {
    // all of this is copied from CyanLizardSmoke lol
    public Color color = col;
    public void EmitSmoke(Vector2 pos, Vector2 vel, bool big, PlayerGraphics pGraphics, float maxlifeTime) {
        if (!(AddParticle(pos, vel, maxlifeTime * Mathf.Lerp(0.3f, 1f, Random.value)) is LeaperParticle particle))
            return;
        particle.pGraphics = pGraphics;
        particle.big = big;
    }
    public class LeaperParticle(Color col) : SpriteSmoke {
        public Color fadeColor;
        public PlayerGraphics pGraphics;
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
            Color color = !big ? Color.Lerp(((LeaperSmoke)owner).color, fadeColor, Mathf.InverseLerp(1f, 0.25f, f) * 0.5f) : Color.Lerp(((LeaperSmoke)owner).color, fadeColor, Mathf.InverseLerp(1f, 0.25f, f));
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

public class LeaperEnums {
    public static SlugcatStats.Name Leaper = new("leaper");
    public static SmokeSystem.SmokeType LeaperSmoke = new("LeaperSmoke", true);
}

public class Hooks {
    public static float chargeDepleteRate = 0.1f;
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
               && Mathf.Approximately(player.airInLungs, 1f)
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
        if (self.input[0].spec && data.storedCharge >= chargeDepleteRate) {
            data.storedCharge -= chargeDepleteRate;
            data.usingCharge += chargeDepleteRate;
        }
        if (!self.input[0].spec) {
            if (data.specWasHeld && self.canJump <= 0 && self.bodyMode != Player.BodyModeIndex.CorridorClimb) {
                if (data.usingCharge < 1f && data.storedCharge >= 1-data.usingCharge) {
                    data.usingCharge = 1f;
                    data.storedCharge -= 1 - data.usingCharge;
                }
                Plugin.Logger.LogInfo("Leap! " + data.usingCharge);
                /*if (data.smoke != null && data.smoke.room != self.room) {
                    data.smoke.Destroy();
                    data.smoke = null;
                }
                if (data.smoke == null) {
                    data.smoke = new LeaperSmoke(self.room, new Color(0.66f, 0.99f, 0.99f));
                    self.room.AddObject(data.smoke);
                }*/
                switch (data.usingCharge) {
                    case <= 5/3f:
                        self.room.PlaySound(SoundID.Cyan_Lizard_Small_Jump, self.mainBodyChunk);
                        break;
                    case <= 10/3f:
                        self.room.PlaySound(SoundID.Cyan_Lizard_Medium_Jump, self.mainBodyChunk);
                        break;
                    default:
                        self.room.PlaySound(SoundID.Cyan_Lizard_Powerful_Jump, self.mainBodyChunk);
                        break;
                }
                if (self.graphicsModule is PlayerGraphics pg) {
                    self.room.InGameNoise(new InGameNoise(pg.tail[2].pos, 3000f*data.usingCharge/charge, self, 1f));
                    /*for (int i = 0; i < 2 * data.usingCharge/charge; ++i)
                        data.smoke.EmitSmoke(pg.tail[2].pos, Custom.RNV() * Mathf.Lerp(6f, 23f, data.usingCharge/charge), true, pg, Mathf.Lerp(30f, 140f, data.usingCharge/charge));
                    */
                    self.room.AddObject(new ShockWave(pg.tail[2].pos, Mathf.Lerp(40f, 120f, data.usingCharge/charge), 0.07f, 6 + (int) ((double) data.usingCharge/charge * 4.0)));
                }

                float x = self.input[0].x;
                float y = self.input[0].y;
                if (x == 0 && y == 0) y = 1f;
                else if (y == 0) y = 0.2f;
                self.bodyChunks[0].vel.x += Mathf.Lerp(9f, 30f, data.usingCharge/charge) * x;
                self.bodyChunks[0].vel.y += Mathf.Lerp(9f, 30f, data.usingCharge/charge) * y;
                self.bodyChunks[1].vel.x += Mathf.Lerp(8f, 29f, data.usingCharge/charge) * x;
                self.bodyChunks[1].vel.y += Mathf.Lerp(8f, 29f, data.usingCharge/charge) * y;
                self.animation = Player.AnimationIndex.Flip;
                self.bodyMode = Player.BodyModeIndex.Default;
                data.usingCharge = 0f;
                
            } else if (data.usingCharge >= chargeDepleteRate) {
                data.storedCharge += chargeDepleteRate;
                data.usingCharge -= chargeDepleteRate;
            }
        }

        if (data.storedCharge <= charge - chargeDepleteRate/2) data.storedCharge += chargeDepleteRate/2;
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