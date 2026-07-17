using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace MalumMenu;

public static class ImpostorListOverlay
{
	private static readonly List<string> _cachedLines = new();
	private static float _lastGoodCacheTime;
	private static float _nextLogTime;

	public static void OnGUI()
	{
		try
		{
			if (!CheatToggles.showImpostors) return;
			if (PlayerControl.LocalPlayer == null || AmongUsClient.Instance == null) return;
			if (!ShouldShow()) return;

			RefreshLines();

			int prevDepth = GUI.depth;
			Color prevColor = GUI.color;
			GUI.depth = -2000;
			GUI.color = Color.white;

			try
			{
				float pad = 12f;
				float lineH = 22f;
				float titleH = 24f;
				float width = 360f;
				int count = Mathf.Max(_cachedLines.Count, 1);
				float height = titleH + 12f + count * lineH + 16f;
				var box = new Rect(pad, Screen.height - height - pad, width, height);

				GUI.Box(box, GUIContent.none);

				var titleStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 16,
					fontStyle = FontStyle.Bold,
					richText = false
				};
				titleStyle.normal.textColor = new Color(1f, 0.25f, 0.25f);

				var lineStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 14,
					fontStyle = FontStyle.Bold,
					richText = false
				};
				lineStyle.normal.textColor = Color.white;

				float y = box.y + 8f;
				GUI.Label(new Rect(box.x + 12f, y, width - 24f, titleH), "Impostors", titleStyle);
				y += titleH + 4f;

				if (_cachedLines.Count == 0)
				{
					GUI.Label(new Rect(box.x + 12f, y, width - 24f, lineH), "(none / scanning…)", lineStyle);
				}
				else
				{
					foreach (var line in _cachedLines)
					{
						GUI.Label(new Rect(box.x + 12f, y, width - 24f, lineH), line, lineStyle);
						y += lineH;
					}
				}
			}
			finally
			{
				GUI.depth = prevDepth;
				GUI.color = prevColor;
			}
		}
		catch (System.Exception ex)
		{
			if (Time.unscaledTime >= _nextLogTime)
			{
				_nextLogTime = Time.unscaledTime + 2f;
				MalumMenu.Log.LogWarning($"[ShowImpostors] {ex.GetType().Name}: {ex.Message}");
			}
		}
	}

	private static bool ShouldShow()
	{
		try
		{
			if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
				return true;
		}
		catch { /* ignore */ }

		if (Utils.isFreePlay) return true;
		if (ShipStatus.Instance != null) return true;
		if (Utils.isInGame) return true;
		try
		{
			if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
				return true;
		}
		catch { /* ignore */ }

		try
		{
			return PlayerControl.AllPlayerControls != null && PlayerControl.AllPlayerControls.Count > 0
			       && !Utils.isLobby;
		}
		catch
		{
			return false;
		}
	}

	private static void RefreshLines()
	{
		var fresh = new List<string>();
		try
		{
			var all = PlayerControl.AllPlayerControls;
			if (all == null) return;

			foreach (var player in all)
			{
				if (player == null || player.Data == null) continue;
				if (player.Data.Disconnected) continue;
				if (!IsImpostorSide(player)) continue;

				string role = GetRoleLabel(player);
				string name = player.Data.PlayerName;
				if (string.IsNullOrEmpty(name)) name = "?";
				string colorName = GetColorName(player);
				fresh.Add($"{role} - {name} ({colorName})");
			}
		}
		catch
		{
			return;
		}

		if (fresh.Count > 0)
		{
			_cachedLines.Clear();
			_cachedLines.AddRange(fresh);
			_lastGoodCacheTime = Time.unscaledTime;
		}
		else if (Time.unscaledTime - _lastGoodCacheTime > 2f)
		{
			_cachedLines.Clear();
		}
	}

	private static bool IsImpostorSide(PlayerControl player)
	{
		try
		{
			var data = player.Data;
			if (data.Role != null)
			{
				if (data.Role.IsImpostor) return true;
				if (data.Role.TeamType == RoleTeamTypes.Impostor) return true;
			}

			var rt = data.RoleType;
			if (rt is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom
			    or RoleTypes.Viper or RoleTypes.ImpostorGhost)
				return true;

			return RoleManager.IsImpostorRole(rt);
		}
		catch
		{
			return false;
		}
	}

	private static string GetRoleLabel(PlayerControl player)
	{
		try
		{
			if (player.Data.Role != null)
			{
				string r = Utils.GetRoleName(player.Data);
				if (!string.IsNullOrEmpty(r) && r != "STRMISS") return r;
			}
		}
		catch { /* ignore */ }

		try { return player.Data.RoleType.ToString(); }
		catch { return "Impostor"; }
	}

	private static string GetColorName(PlayerControl player)
	{
		try
		{
			int id = player.Data.DefaultOutfit.ColorId;
			try
			{
				if (player.CurrentOutfit != null)
					id = player.CurrentOutfit.ColorId;
			}
			catch { /* use default outfit */ }

			return id switch
			{
				0 => "Red", 1 => "Blue", 2 => "Green", 3 => "Pink",
				4 => "Orange", 5 => "Yellow", 6 => "Black", 7 => "White",
				8 => "Purple", 9 => "Brown", 10 => "Cyan", 11 => "Lime",
				12 => "Maroon", 13 => "Rose", 14 => "Banana", 15 => "Gray",
				16 => "Tan", 17 => "Coral",
				_ => $"Color{id}"
			};
		}
		catch
		{
			return "Unknown";
		}
	}
}
