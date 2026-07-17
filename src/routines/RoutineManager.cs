using UnityEngine;

namespace MalumMenu.routines
{
	public class RoutineManager : MonoBehaviour
	{
		public AutoTriggerSporesRoutine autoTriggerSpores = new AutoTriggerSporesRoutine();
		public DiscoHostRoutine discoHost = new DiscoHostRoutine();
		public DoorTrollerRoutine doorTroller = new DoorTrollerRoutine();
		public PlayerFollowerRoutine playerFollower = new PlayerFollowerRoutine();
		public ReportBodySpam reportBodySpam = new ReportBodySpam();
		public XpFarmRoutine xpFarm = new XpFarmRoutine();

		public void Update()
		{
			if(autoTriggerSpores.Enabled) autoTriggerSpores.Run();
			if(discoHost.Enabled) discoHost.Run();
			if(doorTroller.Enabled) doorTroller.Run();
			if(playerFollower._enabled) playerFollower.Run();
			if(reportBodySpam.Enabled) reportBodySpam.Run();

			xpFarm.Run();

			TpKillHandler.Update();

			PlayerViewHandler.Update();

			SkipInfoHandler.Update();
		}

		public void OnGUI()
		{
			SafeGui(TpKillHandler.OnGUI);
			SafeGui(TaskCompleteButton.OnGUI);
			SafeGui(PlayerViewHandler.OnGUI);
			SafeGui(ImpostorListOverlay.OnGUI);
		}

		private static void SafeGui(System.Action draw)
		{
			try { draw(); }
			catch (System.Exception ex)
			{
				MalumMenu.Log.LogWarning($"[OnGUI] {draw.Method.DeclaringType?.Name}: {ex.GetType().Name}: {ex.Message}");
			}
		}
	}
}