using System;
using System.Collections;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using UnityEngine;

namespace MalumMenu;

public static class TpKillHandler
{
	public static readonly Vector2 EscapePosition = new(16.11f, 7.95f);
	private const float TeleportDuration = 0.5f;
	private const float TeleportInterval = 0.1f;
	private const float LogInterval = 1.0f;

	private static Coroutine _teleportCoroutine;
	private static float _nextLogTime;
	private static string _lastDiag = "";
	private static GUIStyle _btnStyle;
	private static GUIStyle _cdStyle;
	private static Rect _btnRect;

	public static bool isPickingTarget;

	public static string LastDiagnostic => _lastDiag;

	public static bool IsPointerOverButton()
	{
		if (!CheatToggles.showTpKill) return false;
		if (_btnRect.width <= 0f || _btnRect.height <= 0f) return false;
		if (!CanUseFeature(out _)) return false;

		var guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
		return _btnRect.Contains(guiMouse);
	}

	public static void Update()
	{
		try
		{
			if (!CheatToggles.showTpKill)
			{
				if (isPickingTarget && PlayerPickMenu.playerpickMenu == null)
					isPickingTarget = false;
				_lastDiag = "showTpKill=OFF";
				return;
			}

			BuildDiagnostic();
			MaybeLogDiagnostic();

			if (isPickingTarget && PlayerPickMenu.playerpickMenu == null)
				isPickingTarget = false;
		}
		catch (Exception ex)
		{
			_lastDiag = $"Update EXCEPTION: {ex.Message}";
			MalumMenu.Log.LogWarning($"[TP Kill] Update: {ex}");
		}
	}

	public static void OnGUI()
	{
		Color prevColor = GUI.color;
		bool prevEnabled = GUI.enabled;
		int prevDepth = GUI.depth;
		Matrix4x4 prevMatrix = GUI.matrix;

		try
		{
			if (!CheatToggles.showTpKill) return;
			if (!CanUseFeature(out _)) return;

			GUI.depth = 10;
			GUI.matrix = Matrix4x4.identity;
			GUI.enabled = true;

			EnsureStyles();

			float size = Mathf.Clamp(Screen.height * 0.09f, 64f, 100f);
			float fromRight = 100f;  // slightly more right than 112
			float fromBottom = 400f; // up more than 316
			_btnRect = new Rect(
				Screen.width - size - fromRight,
				Screen.height - size - fromBottom,
				size,
				size);

			float timer = PlayerControl.LocalPlayer != null ? Mathf.Max(0f, PlayerControl.LocalPlayer.killTimer) : 0f;
			bool onCd = timer > 0.01f;

			GUI.color = onCd ? new Color(0.45f, 0.45f, 0.45f, 0.95f) : new Color(0.85f, 0.15f, 0.15f, 0.95f);

			Event e = Event.current;
			bool mouseOver = e != null && _btnRect.Contains(e.mousePosition);

			if (mouseOver && e.type == EventType.MouseDown && e.button == 1)
			{
				e.Use();
				OpenTargetPicker();
				return;
			}

			string label = onCd ? $"TP\n{timer:F1}s" : "TP\nKILL";
			if (GUI.Button(_btnRect, label, _btnStyle))
				OnLeftClick();
		}
		catch (Exception ex)
		{
			MalumMenu.Log.LogWarning($"[TP Kill] OnGUI: {ex.Message}");
		}
		finally
		{
			GUI.color = prevColor;
			GUI.enabled = prevEnabled;
			GUI.depth = prevDepth;
			GUI.matrix = prevMatrix;
		}
	}

	private static void EnsureStyles()
	{
		if (_btnStyle != null && _cdStyle != null) return;

		_btnStyle = new GUIStyle(GUI.skin != null ? GUI.skin.button : new GUIStyle())
		{
			fontSize = 16,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			wordWrap = true,
			richText = false
		};
		_btnStyle.normal.textColor = Color.white;
		_btnStyle.hover.textColor = Color.white;
		_btnStyle.active.textColor = Color.white;

		_cdStyle = new GUIStyle(GUI.skin != null ? GUI.skin.label : new GUIStyle())
		{
			fontSize = 14,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			richText = false
		};
		_cdStyle.normal.textColor = Color.white;
	}

	private static void BuildDiagnostic()
	{
		bool hasClient = AmongUsClient.Instance != null;
		var state = hasClient ? AmongUsClient.Instance.GameState.ToString() : "null";
		bool hasPlayer = PlayerControl.LocalPlayer != null;
		bool hasData = hasPlayer && PlayerControl.LocalPlayer.Data != null;
		string roleType = "?";
		string roleName = "?";
		bool isImp = false;
		bool canKillBtn = false;
		bool isDead = true;
		bool roleDead = true;

		if (hasData)
		{
			var data = PlayerControl.LocalPlayer.Data;
			isDead = data.IsDead;
			roleType = data.RoleType.ToString();
			if (data.Role != null)
			{
				roleName = data.Role.NiceName ?? data.Role.GetIl2CppType().Name;
				isImp = data.Role.IsImpostor;
				canKillBtn = data.Role.CanUseKillButton;
				roleDead = data.Role.IsDead;
			}
		}

		bool killBtnExists = HudManager.Instance != null && HudManager.Instance.KillButton != null;
		bool killBtnActive = killBtnExists && HudManager.Instance.KillButton.gameObject.activeInHierarchy;
		bool eligible = CanUseFeature(out string reason);
		bool freeplay = Utils.isFreePlay;

		_lastDiag =
			$"eligible={eligible} reason={reason} freeplay={freeplay} GameState={state} " +
			$"RoleType={roleType} IsImp={isImp} CanKill={canKillBtn} dead={isDead} " +
			$"KillBtn={killBtnActive} timer={(hasPlayer ? PlayerControl.LocalPlayer.killTimer : -1f):F1}";
	}

	public static string ShortStatus
	{
		get
		{
			if (!CheatToggles.showTpKill) return "off";
			bool ok = CanUseFeature(out string reason);
			return ok ? "ready (button bottom-right)" : $"waiting ({reason})";
		}
	}

	private static void MaybeLogDiagnostic()
	{
		if (Time.unscaledTime < _nextLogTime) return;
		_nextLogTime = Time.unscaledTime + LogInterval;
		MalumMenu.Log.LogInfo($"[TP Kill] {_lastDiag}");
	}

	public static bool CanUseFeature(out string reason)
	{
		if (!CheatToggles.showTpKill)
		{
			reason = "toggle_off";
			return false;
		}

		if (AmongUsClient.Instance == null)
		{
			reason = "no_client";
			return false;
		}

		if (Utils.isMeeting)
		{
			reason = "in_meeting";
			return false;
		}

		if (Utils.isLobby && !Utils.isFreePlay)
		{
			reason = "in_lobby";
			return false;
		}

		bool freeplayGameplay = Utils.isFreePlay
		                        && PlayerControl.LocalPlayer != null
		                        && (ShipStatus.Instance != null
		                            || (HudManager.Instance != null && HudManager.Instance.KillButton != null
		                                && HudManager.Instance.KillButton.gameObject.activeInHierarchy));

		bool onlineGameplay = Utils.isInGame
		                      || AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started;

		if (!freeplayGameplay && !onlineGameplay)
		{
			reason = $"bad_state_{AmongUsClient.Instance.GameState}_fp={Utils.isFreePlay}";
			return false;
		}

		if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null)
		{
			reason = "no_local_player";
			return false;
		}

		var data = PlayerControl.LocalPlayer.Data;
		if (data.IsDead)
		{
			reason = "player_dead";
			return false;
		}

		if (!IsEligibleImpostor(data, out string roleReason))
		{
			reason = roleReason;
			return false;
		}

		reason = freeplayGameplay ? "ok_freeplay" : "ok";
		return true;
	}

	private static bool IsEligibleImpostor(NetworkedPlayerInfo data, out string reason)
	{
		if (data.Role != null)
		{
			if (data.Role.IsDead)
			{
				reason = "role_is_dead";
				return false;
			}

			if (data.Role.IsImpostor || data.Role.CanUseKillButton)
			{
				reason = $"ok_roleflags(IsImp={data.Role.IsImpostor},CanKill={data.Role.CanUseKillButton},Role={data.RoleType})";
				return true;
			}
		}

		var rt = data.RoleType;
		if (rt is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom or RoleTypes.Viper)
		{
			reason = $"ok_roletype({rt})";
			return true;
		}

		try
		{
			if (RoleManager.IsImpostorRole(rt) && rt is not RoleTypes.ImpostorGhost)
			{
				reason = $"ok_IsImpostorRole({rt})";
				return true;
			}
		}
		catch (Exception ex)
		{
			reason = $"RoleManager_throw:{ex.Message}";
			return false;
		}

		reason = $"not_imp(RoleType={rt}, RoleNull={data.Role == null})";
		return false;
	}

	private static float GetKillCooldown()
	{
		try
		{
			if (GameManager.Instance?.LogicOptions != null)
			{
				float cd = GameManager.Instance.LogicOptions.GetKillCooldown();
				if (cd > 0f) return cd;
			}
		}
		catch { /* default */ }

		return 30f;
	}

	private static PlayerControl GetNearestKillable()
	{
		var me = PlayerControl.LocalPlayer;
		if (me == null) return null;

		PlayerControl best = null;
		float bestDist = float.MaxValue;
		Vector2 myPos = me.GetTruePosition();

		foreach (var player in PlayerControl.AllPlayerControls)
		{
			if (!IsValidKillTarget(player)) continue;

			float d = Vector2.Distance(myPos, player.GetTruePosition());
			if (d < bestDist)
			{
				bestDist = d;
				best = player;
			}
		}

		return best;
	}

	private static bool IsValidKillTarget(PlayerControl player)
	{
		if (player == null || player.Data == null) return false;
		if (player == PlayerControl.LocalPlayer) return false;
		if (player.Data.IsDead || player.Data.Disconnected) return false;

		if (player.Data.Role != null && player.Data.Role.IsImpostor && !CheatToggles.killAnyone)
			return false;

		if (player.protectedByGuardianId != -1 && !CheatToggles.killAnyone)
			return false;

		return true;
	}

	private static void OnLeftClick()
	{
		if (!CanUseFeature(out string reason))
		{
			MalumMenu.Log.LogInfo($"[TP Kill] Left-click ignored: {reason}");
			return;
		}

		if (PlayerControl.LocalPlayer.killTimer > 0.01f)
		{
			MalumMenu.Log.LogInfo($"[TP Kill] Left-click on cooldown ({PlayerControl.LocalPlayer.killTimer:F2}s)");
			return;
		}

		var target = GetNearestKillable();
		if (target == null)
		{
			MalumMenu.Log.LogInfo("[TP Kill] Left-click: no valid nearest target");
			HudManager.Instance?.Notifier?.AddDisconnectMessage("TP Kill: no valid target nearby");
			return;
		}

		PerformTpKill(target);
	}

	private static void OpenTargetPicker()
	{
		if (!CanUseFeature(out string reason))
		{
			MalumMenu.Log.LogInfo($"[TP Kill] Right-click ignored: {reason}");
			return;
		}

		if (PlayerPickMenu.playerpickMenu != null)
			PlayerPickMenu.playerpickMenu.Close();

		CheatToggles.DisablePPMCheats("");
		isPickingTarget = true;
		MalumMenu.Log.LogInfo("[TP Kill] Opening player pick menu");

		PlayerPickMenu.OpenPlayerPickMenu(Utils.GetAllPlayerData(), (Action)(() =>
		{
			try
			{
				var target = PlayerPickMenu.targetPlayerData?.Object;
				isPickingTarget = false;

				if (target == null) return;
				if (PlayerControl.LocalPlayer == null) return;
				if (PlayerControl.LocalPlayer.killTimer > 0.01f) return;
				if (!IsValidKillTarget(target)) return;

				PerformTpKill(target);
			}
			catch (Exception ex)
			{
				isPickingTarget = false;
				MalumMenu.Log.LogWarning($"[TP Kill] PPM action: {ex.Message}");
			}
		}));
	}

	private static void PerformTpKill(PlayerControl target)
	{
		if (target == null || PlayerControl.LocalPlayer == null) return;

		MalumMenu.Log.LogInfo($"[TP Kill] Killing {target.Data?.PlayerName} + escape TP to {EscapePosition}");

		Utils.MurderPlayer(target, MurderResultFlags.Succeeded);

		if (!CheatToggles.noKillCd)
			PlayerControl.LocalPlayer.SetKillTimer(GetKillCooldown());

		StartEscapeTeleport();
	}

	private static void StartEscapeTeleport()
	{
		if (PlayerControl.LocalPlayer == null) return;

		SnapToEscape();

		if (MalumMenu.routines == null) return;

		if (_teleportCoroutine != null)
		{
			MalumMenu.routines.StopCoroutine(_teleportCoroutine);
			_teleportCoroutine = null;
		}

		_teleportCoroutine = MalumMenu.routines.StartCoroutine(EscapeTeleportLoop().WrapToIl2Cpp());
	}

	private static IEnumerator EscapeTeleportLoop()
	{
		float elapsed = 0f;
		while (elapsed < TeleportDuration)
		{
			yield return new WaitForSeconds(TeleportInterval);
			elapsed += TeleportInterval;
			SnapToEscape();
		}

		_teleportCoroutine = null;
	}

	private static void SnapToEscape()
	{
		try
		{
			if (PlayerControl.LocalPlayer?.NetTransform == null) return;
			PlayerControl.LocalPlayer.NetTransform.RpcSnapTo(EscapePosition);
		}
		catch (Exception ex)
		{
			MalumMenu.Log.LogWarning($"[TP Kill] SnapTo: {ex.Message}");
		}
	}
}
