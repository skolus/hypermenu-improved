using UnityEngine;

namespace MalumMenu;

public static class SkipInfoHandler
{
	private static float _lastSkipTime = -999f;

	public static void Update()
	{
		if (!CheatToggles.skipInfo) return;

		bool hadIntro = false;

		try
		{
			if (IntroCutscene.Instance != null)
			{
				hadIntro = true;
				var go = IntroCutscene.Instance.gameObject;
				MalumMenu.Log.LogInfo("[SkipInfo] Finishing intro (destroy cutscene)");
				UnityEngine.Object.Destroy(go);
			}
		}
		catch (System.Exception ex)
		{
			MalumMenu.Log.LogWarning($"[SkipInfo] Destroy intro: {ex.Message}");
		}

		if (!IsGameplayContext()) return;

		RestoreGameplay(forceOverlay: hadIntro || Time.unscaledTime - _lastSkipTime < 2f);

		if (hadIntro)
			_lastSkipTime = Time.unscaledTime;
	}

	public static void FinishIntroNow(IntroCutscene intro)
	{
		if (!CheatToggles.skipInfo) return;

		try
		{
			if (intro != null && intro.gameObject != null)
				UnityEngine.Object.Destroy(intro.gameObject);
		}
		catch { /* ignore */ }

		_lastSkipTime = Time.unscaledTime;
		RestoreGameplay(forceOverlay: true);
	}

	private static bool IsGameplayContext()
	{
		if (AmongUsClient.Instance == null || PlayerControl.LocalPlayer == null)
			return false;
		if (Utils.isLobby && !Utils.isFreePlay)
			return false;
		return Utils.isInGame || Utils.isFreePlay
		       || AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started
		       || ShipStatus.Instance != null;
	}

	private static void RestoreGameplay(bool forceOverlay)
	{
		try
		{
			if (PlayerControl.LocalPlayer != null)
				PlayerControl.LocalPlayer.moveable = true;
		}
		catch { /* ignore */ }

		try
		{
			var cam = Camera.main;
			if (cam != null)
			{
				var follower = cam.GetComponent<FollowerCamera>();
				if (follower != null)
				{
					follower.Locked = false;
					if (!follower.enabled)
						follower.enabled = true;
					if (follower.Target == null && PlayerControl.LocalPlayer != null)
						follower.SetTarget(PlayerControl.LocalPlayer);
				}
			}
		}
		catch { /* ignore */ }

		try
		{
			var hud = HudManager.Instance;
			if (hud == null) return;

			hud.IsIntroDisplayed = false;

			hud.SetHudActive(true);

			if (hud.FullScreen != null)
			{
				hud.FullScreen.gameObject.SetActive(false);
				var c = hud.FullScreen.color;
				hud.FullScreen.color = new Color(c.r, c.g, c.b, 0f);
			}

		}
		catch (System.Exception ex)
		{
			MalumMenu.Log.LogWarning($"[SkipInfo] RestoreGameplay: {ex.Message}");
		}
	}
}
