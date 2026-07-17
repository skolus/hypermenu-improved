using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;

namespace MalumMenu;

public static class PhantomInvisHandler
{
	private enum Mode
	{
		None,
		WaitingFullInvis,
		WaitingFullyVisible
	}

	private static Mode _mode = Mode.None;
	private static bool _sequenceRunning;

	public static bool AllowVanishPassthrough { get; private set; }

	public static bool AllowAppearPassthrough { get; private set; }

	private static Vector2 _savedPos;
	private static Vector3 _savedCamPos;
	private static bool _hadFollowerEnabled;
	private static float _startTime;
	private static float _pendingMaxDuration;
	private static bool _pendingAppearAnimate = true;
	private static Coroutine _sequenceCoroutine;

	public static bool IsActive => _mode != Mode.None || _sequenceRunning;

	public static bool TryInterceptVanish(PlayerControl player, float maxDuration)
	{
		if (!CheatToggles.phantomInvis) return false;
		if (player == null || !player.AmOwner) return false;
		if (AllowVanishPassthrough) return false;
		if (_sequenceRunning || _mode != Mode.None) return false;

		try
		{
			if (player.Data?.Role is PhantomRole pr && (pr.isInvisible || pr.IsInvisible))
				return false;
		}
		catch { /* continue */ }

		_pendingMaxDuration = maxDuration;
		StartSequence(isAppear: false);
		return true;
	}

	public static bool TryInterceptAppear(PlayerControl player, bool shouldAnimate)
	{
		if (!CheatToggles.phantomInvis) return false;
		if (player == null || !player.AmOwner) return false;
		if (AllowAppearPassthrough) return false;
		if (_sequenceRunning || _mode != Mode.None) return false;

		bool isInvis = false;
		try
		{
			if (player.Data?.Role is PhantomRole pr)
				isInvis = pr.isInvisible || pr.IsInvisible || pr.fading || pr.IsFading;
		}
		catch { /* continue */ }

		if (!isInvis) return false;

		_pendingAppearAnimate = shouldAnimate;
		StartSequence(isAppear: true);
		return true;
	}

	private static void StartSequence(bool isAppear)
	{
		var player = PlayerControl.LocalPlayer;
		if (player == null) return;

		_sequenceRunning = true;
		_mode = Mode.None; // set after ability fires
		_startTime = Time.time;

		try
		{
			var t = player.transform.position;
			_savedPos = new Vector2(t.x, t.y);
		}
		catch
		{
			_savedPos = player.GetTruePosition();
		}

		var cam = Camera.main;
		_savedCamPos = cam != null
			? cam.transform.position
			: new Vector3(_savedPos.x, _savedPos.y, -10f);

		player.moveable = false;
		try { player.MyPhysics?.SetNormalizedVelocity(Vector2.zero); } catch { /* ignore */ }

		LockCamera();
		SnapTo(TpKillHandler.EscapePosition);

		string kind = isAppear ? "appear" : "vanish";
		MalumMenu.Log.LogInfo($"[PhantomInvis] {kind} sequence — saved {_savedPos}, TP to {TpKillHandler.EscapePosition}");

		if (_sequenceCoroutine != null && MalumMenu.routines != null)
		{
			try { MalumMenu.routines.StopCoroutine(_sequenceCoroutine); } catch { /* ignore */ }
			_sequenceCoroutine = null;
		}

		if (MalumMenu.routines != null)
			_sequenceCoroutine = MalumMenu.routines.StartCoroutine(AbilityAfterTeleport(isAppear).WrapToIl2Cpp());
		else
			FireAbilityNow(isAppear);
	}

	private static IEnumerator AbilityAfterTeleport(bool isAppear)
	{
		yield return new WaitForSeconds(0.2f);

		SnapTo(TpKillHandler.EscapePosition);
		LockCamera();
		if (PlayerControl.LocalPlayer != null)
			PlayerControl.LocalPlayer.moveable = false;

		yield return new WaitForSeconds(0.05f);

		FireAbilityNow(isAppear);
		_sequenceCoroutine = null;
	}

	private static void FireAbilityNow(bool isAppear)
	{
		var player = PlayerControl.LocalPlayer;
		if (player == null)
		{
			ForceCleanup();
			return;
		}

		if (isAppear)
		{
			try
			{
				AllowAppearPassthrough = true;
				MalumMenu.Log.LogInfo($"[PhantomInvis] Sending CmdCheckAppear(animate={_pendingAppearAnimate})");
				player.CmdCheckAppear(_pendingAppearAnimate);
			}
			catch (System.Exception ex)
			{
				MalumMenu.Log.LogWarning($"[PhantomInvis] CmdCheckAppear failed: {ex.Message}");
				try
				{
					AllowAppearPassthrough = true;
					player.CheckAppear(_pendingAppearAnimate);
				}
				catch (System.Exception ex2)
				{
					MalumMenu.Log.LogWarning($"[PhantomInvis] CheckAppear fallback failed: {ex2.Message}");
				}
			}
			finally
			{
				AllowAppearPassthrough = false;
			}

			_mode = Mode.WaitingFullyVisible;
		}
		else
		{
			try
			{
				AllowVanishPassthrough = true;
				MalumMenu.Log.LogInfo($"[PhantomInvis] Sending CmdCheckVanish(duration={_pendingMaxDuration})");
				player.CmdCheckVanish(_pendingMaxDuration);
			}
			catch (System.Exception ex)
			{
				MalumMenu.Log.LogWarning($"[PhantomInvis] CmdCheckVanish failed: {ex.Message}");
				try
				{
					if (player.Data?.Role is PhantomRole role)
						role.UseAbility();
				}
				catch (System.Exception ex2)
				{
					MalumMenu.Log.LogWarning($"[PhantomInvis] UseAbility fallback failed: {ex2.Message}");
				}
			}
			finally
			{
				AllowVanishPassthrough = false;
			}

			_mode = Mode.WaitingFullInvis;
		}

		_sequenceRunning = false;
		_startTime = Time.time;
	}

	public static void Tick(PhantomRole role)
	{
		if (_mode == Mode.None && !_sequenceRunning) return;

		if (!CheatToggles.phantomInvis || role == null || role.Player == null || !role.Player.AmOwner)
		{
			ForceCleanup();
			return;
		}

		var player = role.Player;

		player.moveable = false;
		try { player.MyPhysics?.SetNormalizedVelocity(Vector2.zero); } catch { /* ignore */ }
		LockCameraPositionOnly();

		if (_mode != Mode.None || _sequenceRunning)
		{
			float d = Vector2.Distance(player.GetTruePosition(), TpKillHandler.EscapePosition);
			if (d > 0.5f)
				SnapTo(TpKillHandler.EscapePosition);
		}

		if (_mode == Mode.None) return;

		bool done = false;
		try
		{
			bool invis = role.isInvisible || role.IsInvisible;
			bool fading = role.fading || role.IsFading;

			if (_mode == Mode.WaitingFullInvis)
			{
				done = invis && !fading;
			}
			else if (_mode == Mode.WaitingFullyVisible)
			{
				done = !invis && !fading;
			}
		}
		catch
		{
			if (_mode == Mode.WaitingFullInvis)
				done = role.isInvisible && !role.fading;
			else
				done = !role.isInvisible && !role.fading;
		}

		if (!done && Time.time - _startTime > 4f)
		{
			MalumMenu.Log.LogWarning($"[PhantomInvis] Timeout in {_mode} — returning");
			done = true;
		}

		if (done)
			ReturnToSaved();
	}

	private static void ReturnToSaved()
	{
		var mode = _mode;
		_mode = Mode.None;
		_sequenceRunning = false;

		var player = PlayerControl.LocalPlayer;
		if (player != null)
		{
			SnapTo(_savedPos);
			MalumMenu.Log.LogInfo($"[PhantomInvis] {mode} done — returned to {_savedPos}");
		}

		RestoreCameraAndMovement();
	}

	private static void ForceCleanup()
	{
		if (_mode == Mode.None && !_sequenceRunning) return;
		MalumMenu.Log.LogInfo("[PhantomInvis] Force cleanup");

		if (MalumMenu.routines != null && _sequenceCoroutine != null)
		{
			try { MalumMenu.routines.StopCoroutine(_sequenceCoroutine); } catch { /* ignore */ }
			_sequenceCoroutine = null;
		}

		if (PlayerControl.LocalPlayer != null && (_mode != Mode.None || _sequenceRunning))
			SnapTo(_savedPos);

		RestoreCameraAndMovement();
		_mode = Mode.None;
		_sequenceRunning = false;
		AllowVanishPassthrough = false;
		AllowAppearPassthrough = false;
	}

	private static void LockCamera()
	{
		try
		{
			var cam = Camera.main;
			if (cam == null) return;

			var follower = cam.GetComponent<FollowerCamera>();
			if (follower != null)
			{
				_hadFollowerEnabled = follower.enabled;
				follower.enabled = false;
				follower.Target = null;
			}
			cam.transform.position = _savedCamPos;
		}
		catch (System.Exception ex)
		{
			MalumMenu.Log.LogWarning($"[PhantomInvis] Camera lock: {ex.Message}");
		}
	}

	private static void LockCameraPositionOnly()
	{
		try
		{
			if (Camera.main != null)
				Camera.main.transform.position = _savedCamPos;
		}
		catch { /* ignore */ }
	}

	private static void RestoreCameraAndMovement()
	{
		var player = PlayerControl.LocalPlayer;
		if (player != null)
			player.moveable = true;

		try
		{
			var cam = Camera.main;
			if (cam == null) return;

			var follower = cam.GetComponent<FollowerCamera>();
			if (follower != null)
			{
				follower.enabled = true;
				if (player != null)
					follower.SetTarget(player);
			}
		}
		catch (System.Exception ex)
		{
			MalumMenu.Log.LogWarning($"[PhantomInvis] Restore camera: {ex.Message}");
		}
	}

	private static void SnapTo(Vector2 pos)
	{
		try
		{
			var player = PlayerControl.LocalPlayer;
			if (player?.NetTransform == null) return;
			player.NetTransform.RpcSnapTo(pos);
		}
		catch (System.Exception ex)
		{
			MalumMenu.Log.LogWarning($"[PhantomInvis] SnapTo: {ex.Message}");
		}
	}
}
