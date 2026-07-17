using UnityEngine;

namespace MalumMenu;

public class ShipTab : ITab
{
    public string name => "Ship";

    public void Draw()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        DrawGeneral();

        GUILayout.Space(15);

        DrawSabotage();

        GUILayout.EndVertical();

        GUILayout.BeginVertical();

        DrawVents();

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private void DrawGeneral()
    {
        // CheatToggles.completeAllTasks = GUILayout.Toggle(CheatToggles.completeAllTasks, " Allow All Tasks");
        
        CheatToggles.fakeTasks = GUILayout.Toggle(CheatToggles.fakeTasks, " Fake Tasks");

        CheatToggles.doAnyTask = GUILayout.Toggle(CheatToggles.doAnyTask, " Do Any Task");

        CheatToggles.xpFarm = GUILayout.Toggle(CheatToggles.xpFarm, " XP Farm");

        if (MalumMenu.routines != null)
        {
            var farm = MalumMenu.routines.xpFarm;
            if (CheatToggles.xpFarm)
            {
                GUILayout.Label($" Wait before end: {farm.waitBeforeEndSeconds:F0}s  60s is close to optimal; too lazy to test further");
                farm.waitBeforeEndSeconds = GUILayout.HorizontalSlider(farm.waitBeforeEndSeconds, 0f, 120f);
            }
            GUILayout.Label($" XP Farm: {farm.StatusText}");
        }

        CheatToggles.showTpKill = GUILayout.Toggle(CheatToggles.showTpKill, " Show TP Kill");
        if (CheatToggles.showTpKill)
        {
            GUILayout.Label($" TP Kill: {TpKillHandler.ShortStatus}");
        }

        CheatToggles.taskComplete = GUILayout.Toggle(CheatToggles.taskComplete, " Task Complete");

        CheatToggles.unfixableLights = GUILayout.Toggle(CheatToggles.unfixableLights, " Unfixable Lights");

        CheatToggles.callMeeting = GUILayout.Toggle(CheatToggles.callMeeting, " Call Meeting");

        CheatToggles.reportBody = GUILayout.Toggle(CheatToggles.reportBody, " Report Body");

        CheatToggles.closeMeeting = GUILayout.Toggle(CheatToggles.closeMeeting, " Close Meeting");

        CheatToggles.autoReportBodies = GUILayout.Toggle(CheatToggles.autoReportBodies, " Auto-Report Dead Bodies");

        CheatToggles.autoOpenDoorsOnUse = GUILayout.Toggle(CheatToggles.autoOpenDoorsOnUse, " Auto-Open Doors On Use");

        CheatToggles.kickOffensiveNames = GUILayout.Toggle(CheatToggles.kickOffensiveNames, " Kick Offensive Names");
    }

    private void DrawSabotage()
    {
        GUILayout.Label("Sabotage", GUIStylePreset.TabSubtitle);

        CheatToggles.reactorSab = GUILayout.Toggle(CheatToggles.reactorSab, " Reactor");

        CheatToggles.oxygenSab = GUILayout.Toggle(CheatToggles.oxygenSab, " Oxygen");

        CheatToggles.elecSab = GUILayout.Toggle(CheatToggles.elecSab, " Lights");

        CheatToggles.commsSab = GUILayout.Toggle(CheatToggles.commsSab, " Comms");

        CheatToggles.showDoorsMenu = GUILayout.Toggle(CheatToggles.showDoorsMenu, " Show Doors Menu");

        CheatToggles.mushSab = GUILayout.Toggle(CheatToggles.mushSab, " Mushroom Mixup");

        CheatToggles.mushSpore = GUILayout.Toggle(CheatToggles.mushSpore, " Trigger Spores");

        CheatToggles.sabotageMap = GUILayout.Toggle(CheatToggles.sabotageMap, " Open Sabotage Map");
    }

    private void DrawVents()
    {
        GUILayout.Label("Vents", GUIStylePreset.TabSubtitle);

        CheatToggles.unlockVents = GUILayout.Toggle(CheatToggles.unlockVents, " Unlock Vents");

        CheatToggles.kickVents = GUILayout.Toggle(CheatToggles.kickVents, " Kick All From Vents");

        CheatToggles.walkInVents = GUILayout.Toggle(CheatToggles.walkInVents, " Walk In Vents");
    }
}
