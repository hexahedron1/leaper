using System;
using System.Linq;
using System.Runtime.CompilerServices;
using SlugBase;
using SlugBase.Features;
using UnityEngine;
using static SlugBase.Features.FeatureTypes;
namespace Leaper;

public class LeaperData {
    public class GraphicsData {
        public int tailSpecklesIndex;
    }
}

public class Hooks {
    internal static SlugcatStats.Name Leaper = new("leaper");
    private static readonly PlayerFeature<float> LeapCharge = PlayerFloat("leap_charge");
    public static readonly ConditionalWeakTable<PlayerGraphics, LeaperData.GraphicsData> pGraphicsCWT = new();
    public static bool TryGetGraphicsData(PlayerGraphics key, out LeaperData.GraphicsData data) {
        data = key != null ? pGraphicsCWT.GetOrCreateValue(key) : null;
        return data != null;
    }

    public static void Unapply() {
        On.PlayerGraphics.ctor -= On_PlayerGraphics_ctor;
        On.PlayerGraphics.InitiateSprites -= On_PlayerGraphics_InitiateSprices;
        On.PlayerGraphics.DrawSprites -= On_PlayerGraphics_DrawSprites;
        On.PlayerGraphics.AddToContainer -= On_PlayerGraphics_AddToContainer;
        On.PlayerGraphics.TailSpeckles.DrawSprites -= On_TailSpeckles_DrawSprites;
    }
    public static void Apply() {
        On.PlayerGraphics.ctor += On_PlayerGraphics_ctor;
        On.PlayerGraphics.InitiateSprites += On_PlayerGraphics_InitiateSprices;
        On.PlayerGraphics.DrawSprites += On_PlayerGraphics_DrawSprites;
        On.PlayerGraphics.AddToContainer += On_PlayerGraphics_AddToContainer;
        On.PlayerGraphics.TailSpeckles.DrawSprites += On_TailSpeckles_DrawSprites;
        On.SaveState.ctor += On_SaveState_ctor;
        On.SLOrcacleState.ForceResetState += On_SLOrcacleState_ForceResetState;
    }

    private static void On_SLOrcacleState_ForceResetState(On.SLOrcacleState.orig_ForceResetState orig, SLOrcacleState self, SlugcatStats.Name saveStateNumber) {
        orig(self, saveStateNumber);
        if (saveStateNumber != Leaper) return;
        self.neuronsLeft = 7;
    }

    private static void On_SaveState_ctor(On.SaveState.orig_ctor orig, SaveState self, SlugcatStats.Name saveStateNumber, PlayerProgression progression) {
        orig(self, saveStateNumber, progression);
        if (saveStateNumber != Leaper) return;
        self.miscWorldSaveData.moonGivenRobe = true;
        self.miscWorldSaveData.moonHeartRestored = true;
        self.miscWorldSaveData.pebblesEnergyTaken = true;
        self.miscWorldSaveData.pebblesRivuletPostgame = true;
    }

    private static void On_TailSpeckles_DrawSprites(On.PlayerGraphics.TailSpeckles.orig_DrawSprites orig, PlayerGraphics.TailSpeckles self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos) {
        orig(self, sLeaser, rCam, timeStacker, camPos);
        if (!PlayerFeatures.CustomColors.TryGet(self.pGraphics.player, out var colorSlot) || LeapCharge.TryGet(self.pGraphics.player, out float charge) || charge <= 0) return;
        var col = colorSlot.FirstOrDefault(x => x.Name == "Eyes")?.Default;
        if (!col.HasValue) return;
        for (int i = 0; i < self.rows; i++) {
            for (int j = 0; j < self.lines; j++) {
                sLeaser.sprites[self.startSprite + i * self.lines + j].color = col.Value;
            }
        }
    }

    private static void On_PlayerGraphics_AddToContainer(On.PlayerGraphics.orig_AddToContainer orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner) {
        orig(self, sLeaser, rCam, newContatiner);
        if (!TryGetGraphicsData(self, out var data) || data.tailSpecklesIndex == 0 || sLeaser.sprites.Length <= data.tailSpecklesIndex || !LeapCharge.TryGet(self.player, out float charge) || charge <= 0) return;
        Plugin.Logger.LogDebug("AddToContainer");
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
        Plugin.Logger.LogDebug("InitiateSprites");
        data.tailSpecklesIndex = sLeaser.sprites.Length;
        self.tailSpecks = new PlayerGraphics.TailSpeckles(self, data.tailSpecklesIndex);
        Plugin.Logger.LogDebug("tailSpecklesIndex " + data.tailSpecklesIndex);
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