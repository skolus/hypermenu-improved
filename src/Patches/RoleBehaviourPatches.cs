using HarmonyLib;
using System.Linq;
using Sentry.Internal.Extensions;

namespace MalumMenu;

[HarmonyPatch(typeof(EngineerRole), nameof(EngineerRole.FixedUpdate))]
public static class EngineerRole_FixedUpdate
{
    public static void Postfix(EngineerRole __instance)
    {
        if(__instance.Player.AmOwner)
        {
            MalumCheats.HandleEngineerCheats(__instance);
        }
    }
}

[HarmonyPatch(typeof(ShapeshifterRole), nameof(ShapeshifterRole.FixedUpdate))]
public static class ShapeshifterRole_FixedUpdate
{
    public static void Postfix(ShapeshifterRole __instance)
    {
        try
        {
            if(__instance.Player.AmOwner)
            {
                MalumCheats.HandleShapeshifterCheats(__instance);
            }
        } catch { }
    }
}

[HarmonyPatch(typeof(ScientistRole), nameof(ScientistRole.Update))]
public static class ScientistRole_Update
{
    public static void Postfix(ScientistRole __instance)
    {
        if(__instance.Player.AmOwner)
        {
            MalumCheats.HandleScientistCheats(__instance);
        }
    }
}

[HarmonyPatch(typeof(TrackerRole), nameof(TrackerRole.FixedUpdate))]
public static class TrackerRole_FixedUpdate
{
    public static void Postfix(TrackerRole __instance)
    {
        if(__instance.Player.AmOwner)
        {
            MalumCheats.HandleTrackerCheats(__instance);
        }
    }
}

[HarmonyPatch(typeof(PhantomRole), nameof(PhantomRole.IsValidTarget))]
public static class PhantomRole_IsValidTarget
{
    // Postfix patch of PhantomRole.IsValidTarget to allow killing while invisible
    public static void Postfix(NetworkedPlayerInfo target, ref bool __result)
    {
        if (CheatToggles.killVanished)
        {
            __result = Utils.IsValidTarget(target);
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckVanish))]
public static class PlayerControl_CmdCheckVanish
{
    public static bool Prefix(PlayerControl __instance, float maxDuration)
    {
        try
        {
            if (PhantomInvisHandler.TryInterceptVanish(__instance, maxDuration))
                return false;
        }
        catch (System.Exception ex)
        {
            MalumMenu.Log.LogWarning($"[PhantomInvis] CmdCheckVanish prefix: {ex.Message}");
        }

        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckAppear))]
public static class PlayerControl_CmdCheckAppear
{
    public static bool Prefix(PlayerControl __instance, bool shouldAnimate)
    {
        try
        {
            if (PhantomInvisHandler.TryInterceptAppear(__instance, shouldAnimate))
                return false;
        }
        catch (System.Exception ex)
        {
            MalumMenu.Log.LogWarning($"[PhantomInvis] CmdCheckAppear prefix: {ex.Message}");
        }

        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckAppear))]
public static class PlayerControl_CheckAppear
{
    public static bool Prefix(PlayerControl __instance, bool shouldAnimate)
    {
        try
        {
            if (PhantomInvisHandler.AllowAppearPassthrough) return true;
            if (PhantomInvisHandler.TryInterceptAppear(__instance, shouldAnimate))
                return false;
        }
        catch (System.Exception ex)
        {
            MalumMenu.Log.LogWarning($"[PhantomInvis] CheckAppear prefix: {ex.Message}");
        }

        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleServerAppear))]
public static class PlayerControl_HandleServerAppear
{
    public static bool Prefix(PlayerControl __instance, bool shouldAnimate)
    {
        try
        {
            if (PhantomInvisHandler.AllowAppearPassthrough) return true;
            if (__instance != null && __instance.AmOwner
                && PhantomInvisHandler.TryInterceptAppear(__instance, shouldAnimate))
                return false;
        }
        catch (System.Exception ex)
        {
            MalumMenu.Log.LogWarning($"[PhantomInvis] HandleServerAppear prefix: {ex.Message}");
        }

        return true;
    }
}

[HarmonyPatch(typeof(PhantomRole), nameof(PhantomRole.FixedUpdate))]
public static class PhantomRole_FixedUpdate
{
    public static void Postfix(PhantomRole __instance)
    {
        try
        {
            if (__instance == null || __instance.Player == null || !__instance.Player.AmOwner) return;
            PhantomInvisHandler.Tick(__instance);
        }
        catch { /* ignore */ }
    }
}

[HarmonyPatch(typeof(ImpostorRole), nameof(ImpostorRole.IsValidTarget))]
public static class ImpostorRole_IsValidTarget
{
    // Postfix patch of ImpostorRole.IsValidTarget to allow forbidden kill targets for killAnyone cheat
    // Allows killing ghosts (with seeGhosts), impostors, players in vents, etc...
    public static void Postfix(NetworkedPlayerInfo target, ref bool __result)
    {
        if (CheatToggles.killAnyone)
        {
           __result = Utils.IsValidTarget(target);
        }
    }
}

[HarmonyPatch(typeof(ImpostorRole), nameof(ImpostorRole.FindClosestTarget))]
public static class ImpostorRole_FindClosestTarget
{
    // Prefix patch of ImpostorRole.FindClosestTarget to allow for infinite kill reach
    public static bool Prefix(ImpostorRole __instance, ref PlayerControl __result)
    {
        if (!CheatToggles.killReach) return true;

        var playerList = Utils.GetPlayersSortedByDistance().Where(player => !player.IsNull() && __instance.IsValidTarget(player.Data) && player.Collider.enabled).ToList();

        __result = playerList[0];

        return false;
    }
}

[HarmonyPatch(typeof(DetectiveRole), nameof(DetectiveRole.FindClosestTarget))]
public static class DetectiveRole_FindClosestTarget
{
    // Prefix patch of DetectiveRole.FindClosestTarget to allow for infinite interrogate reach
    public static bool Prefix(DetectiveRole __instance, ref PlayerControl __result)
    {
        if (!CheatToggles.interrogateReach) return true;

        var playerList = Utils.GetPlayersSortedByDistance().Where(player => !player.IsNull() && __instance.IsValidTarget(player.Data) && player.Collider.enabled).ToList();

        __result = playerList[0];

        return false;
    }
}

[HarmonyPatch(typeof(TrackerRole), nameof(TrackerRole.FindClosestTarget))]
public static class TrackerRole_FindClosestTarget
{
    // Prefix patch of TrackerRole.FindClosestTarget to allow for infinite track reach
    public static bool Prefix(TrackerRole __instance, ref PlayerControl __result)
    {
        if (!CheatToggles.trackReach) return true;

        var playerList = Utils.GetPlayersSortedByDistance().Where(player => !player.IsNull() && __instance.IsValidTarget(player.Data) && player.Collider.enabled).ToList();

        __result = playerList[0];

        return false;
    }
}
