using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MalumMenu;

public static class PlayerViewHandler
{
	private const int RayCount = 96;
	private const float DefaultOrthoSize = 3f;
	private const float ViewAspect = 1920f / 1080f;
	private const float BorderWidth = 0.055f;
	private const float FillAlpha = 0.16f;
	private const float BorderAlpha = 0.95f;
	private const float OriginYNudge = 0.18f;

	private const string RootName = "MalumPlayerViewRoot";

	private static Material _lineMaterial;
	private static Material _fillMaterial;
	private static int _lastFrame = -1;
	private static bool _localInOtherView;

	private static readonly Dictionary<byte, ViewObjects> _views = new();
	private static readonly Vector3[] _rayPoints = new Vector3[RayCount + 1];
	private static readonly Vector2[] _poly2 = new Vector2[RayCount];

	private class ViewObjects
	{
		public GameObject Root;
		public LineRenderer Border;
		public MeshFilter FillFilter;
		public MeshRenderer FillRenderer;
		public Mesh FillMesh;
	}

	public static void Update()
	{
		if (Time.frameCount != _lastFrame)
		{
			_lastFrame = Time.frameCount;
			_localInOtherView = false;
		}

		if (!CheatToggles.showPlayerView
		    || PlayerControl.LocalPlayer == null
		    || AmongUsClient.Instance == null
		    || Utils.isMeeting)
		{
			CleanupAll();
			return;
		}

		bool freeplayOk = Utils.isFreePlay;
		bool onlineOk = Utils.isInGame
		                || AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started;
		if (!freeplayOk && !onlineOk)
		{
			CleanupAll();
			return;
		}

		if (Utils.isLobby && !Utils.isFreePlay)
		{
			CleanupAll();
			return;
		}

		EnsureMaterials();

		var seen = new HashSet<byte>();
		Vector2 localPos = GetVisionOrigin(PlayerControl.LocalPlayer);

		GetCameraWorldBounds(out Vector2 camCenter, out float camHalfW, out float camHalfH);

		float halfH = DefaultOrthoSize;
		float halfW = DefaultOrthoSize * ViewAspect;
		float maxVisionExtent = Mathf.Sqrt(halfW * halfW + halfH * halfH);

		foreach (var player in PlayerControl.AllPlayerControls)
		{
			if (player == null || player.Data == null) continue;
			if (player.Data.Disconnected || player.Data.IsDead) continue;

			seen.Add(player.PlayerId);

			Vector2 origin = GetVisionOrigin(player);
			float viewDist = GetViewDistance(player);
			if (viewDist < 0.5f) viewDist = 0.5f;
			float extent = Mathf.Min(viewDist, maxVisionExtent);

			int mask = Constants.ShadowMask;
			if (mask == 0) mask = Constants.ShipOnlyMask;
			if (player != PlayerControl.LocalPlayer
			    && IsPointInVision(origin, viewDist, halfW, halfH, mask, localPos))
			{
				_localInOtherView = true;
			}

			if (!IsVisionNearCamera(origin, extent, camCenter, camHalfW, camHalfH))
			{
				HideView(player.PlayerId);
				continue;
			}

			UpdatePlayerView(player, origin, viewDist, halfW, halfH, mask);
		}

		var stale = new List<byte>();
		foreach (var kv in _views)
		{
			if (!seen.Contains(kv.Key))
				stale.Add(kv.Key);
		}
		foreach (var id in stale)
		{
			DestroyView(id);
			_views.Remove(id);
		}
	}

	private static bool IsVisionNearCamera(Vector2 origin, float visionRadius, Vector2 camCenter, float camHalfW, float camHalfH)
	{
		float minX = camCenter.x - camHalfW - visionRadius;
		float maxX = camCenter.x + camHalfW + visionRadius;
		float minY = camCenter.y - camHalfH - visionRadius;
		float maxY = camCenter.y + camHalfH + visionRadius;
		return origin.x >= minX && origin.x <= maxX && origin.y >= minY && origin.y <= maxY;
	}

	private static void GetCameraWorldBounds(out Vector2 center, out float halfW, out float halfH)
	{
		var cam = Camera.main;
		if (cam == null || !cam.orthographic)
		{
			center = PlayerControl.LocalPlayer != null
				? (Vector2)PlayerControl.LocalPlayer.transform.position
				: Vector2.zero;
			halfH = DefaultOrthoSize;
			halfW = halfH * ViewAspect;
			return;
		}

		center = cam.transform.position;
		halfH = cam.orthographicSize;
		halfW = halfH * cam.aspect;
	}

	private static void HideView(byte playerId)
	{
		if (!_views.TryGetValue(playerId, out var view) || view.Root == null) return;
		try
		{
			if (view.Border != null) view.Border.enabled = false;
			if (view.FillRenderer != null) view.FillRenderer.enabled = false;
		}
		catch { /* ignore */ }
	}

	public static void OnGUI()
	{
		try
		{
			if (!CheatToggles.showPlayerView || !_localInOtherView) return;

			const float size = 22f;
			const float pad = 16f;
			var rect = new Rect(Screen.width - size - pad, pad, size, size);

			Color prev = GUI.color;
			GUI.color = Color.red;
			GUI.Box(rect, "!");
			GUI.color = prev;
		}
		catch { /* ignore */ }
	}

	private static void UpdatePlayerView(PlayerControl player, Vector2 origin, float viewDist, float halfW, float halfH, int mask)
	{
		for (int i = 0; i < RayCount; i++)
		{
			float angle = (i / (float)RayCount) * Mathf.PI * 2f;
			Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

			float rectDist = DistanceToRectEdge(dir, halfW, halfH);
			float maxDist = Mathf.Min(viewDist, rectDist);

			Vector2 end = origin + dir * maxDist;
			var hit = Physics2D.Raycast(origin, dir, maxDist, mask);
			if (hit.collider != null)
			{
				end = hit.point - dir * 0.02f;
			}

			_rayPoints[i] = new Vector3(end.x, end.y, 0f);
			_poly2[i] = end;
		}
		_rayPoints[RayCount] = _rayPoints[0];

		Color playerColor = GetPlayerColor(player);
		var borderColor = new Color(playerColor.r, playerColor.g, playerColor.b, BorderAlpha);
		var fillColor = new Color(playerColor.r, playerColor.g, playerColor.b, FillAlpha);

		if (!_views.TryGetValue(player.PlayerId, out var view) || view.Root == null)
		{
			view = CreateViewObjects();
			_views[player.PlayerId] = view;
		}

		var lr = view.Border;
		if (lr != null)
		{
			lr.enabled = true;
			lr.positionCount = RayCount + 1;
			lr.startWidth = BorderWidth;
			lr.endWidth = BorderWidth;
			lr.startColor = borderColor;
			lr.endColor = borderColor;
			try { lr.SetColors(borderColor, borderColor); } catch { /* older API */ }
			for (int i = 0; i <= RayCount; i++)
				lr.SetPosition(i, _rayPoints[i]);
		}

		BuildFillMesh(view, origin, fillColor);
	}

	private static bool IsPointInVision(Vector2 origin, float viewDist, float halfW, float halfH, int mask, Vector2 point)
	{
		Vector2 delta = point - origin;
		float dist = delta.magnitude;
		if (dist < 0.01f) return true;
		if (dist > viewDist + 0.05f) return false;

		if (Mathf.Abs(delta.x) > halfW + 0.05f || Mathf.Abs(delta.y) > halfH + 0.05f)
			return false;

		Vector2 dir = delta / dist;
		var hit = Physics2D.Raycast(origin, dir, dist - 0.05f, mask);
		return hit.collider == null;
	}

	private static float DistanceToRectEdge(Vector2 dir, float halfW, float halfH)
	{
		float dx = dir.x == 0f ? float.PositiveInfinity : halfW / Mathf.Abs(dir.x);
		float dy = dir.y == 0f ? float.PositiveInfinity : halfH / Mathf.Abs(dir.y);
		return Mathf.Min(dx, dy);
	}

	private static Vector2 GetVisionOrigin(PlayerControl player)
	{
		if (player == null) return Vector2.zero;

		try
		{
			if (player.lightSource != null)
			{
				var t = player.lightSource.transform;
				Vector3 p = t.position;
				try
				{
					var off = player.lightSource.LightOffset;
					p += off;
				}
				catch { /* optional */ }

				return new Vector2(p.x, p.y + OriginYNudge * 0.25f);
			}
		}
		catch { /* fall through */ }

		Vector2 truePos = player.GetTruePosition();
		return new Vector2(truePos.x, truePos.y + OriginYNudge);
	}

	private static float GetViewDistance(PlayerControl player)
	{
		bool isImp = false;
		try
		{
			isImp = player.Data?.Role != null && player.Data.Role.IsImpostor;
		}
		catch { /* ignore */ }

		// ShipStatus knows lights sabotage + crew/imp light mods.
		// Other clients' LightSource often doesn't shrink under lights — this does.
		try
		{
			if (ShipStatus.Instance != null && player.Data != null)
			{
				float radius = ShipStatus.Instance.CalculateLightRadius(player.Data);
				if (radius > 0.1f) return radius;
			}
		}
		catch { /* fall through */ }

		// Crew under lights: match local crew vision if we're also crew
		try
		{
			if (!isImp && AreLightsOut())
			{
				var local = PlayerControl.LocalPlayer;
				bool localImp = local?.Data?.Role != null && local.Data.Role.IsImpostor;
				if (!localImp && local?.lightSource != null && local.lightSource.ViewDistance > 0.1f)
					return local.lightSource.ViewDistance;
			}
		}
		catch { /* fall through */ }

		try
		{
			if (player.lightSource != null && player.lightSource.ViewDistance > 0.1f)
				return player.lightSource.ViewDistance;
		}
		catch { /* fall through */ }

		return isImp ? 10f : 5f;
	}

	private static bool AreLightsOut()
	{
		try
		{
			return ShipStatus.Instance != null
			       && Sabotage.IsSabotageActive(SystemTypes.Electrical);
		}
		catch
		{
			return false;
		}
	}

	private static Color GetPlayerColor(PlayerControl player)
	{
		try
		{
			var c = player.Data.Color;
			if (c.a > 0.05f) return c;
		}
		catch { /* ignore */ }

		try
		{
			int id = player.Data.DefaultOutfit.ColorId;
			if (id >= 0 && id < Palette.PlayerColors.Count)
				return Palette.PlayerColors[id];
		}
		catch { /* ignore */ }

		return Color.white;
	}

	private static void BuildFillMesh(ViewObjects view, Vector2 origin, Color fillColor)
	{
		if (view.FillMesh == null || view.FillFilter == null || view.FillRenderer == null)
			return;

		int vertCount = RayCount + 1;
		var verts = new Vector3[vertCount];
		var cols = new Color[vertCount];
		var tris = new int[RayCount * 3];

		verts[0] = new Vector3(origin.x, origin.y, 0f);
		cols[0] = fillColor;

		for (int i = 0; i < RayCount; i++)
		{
			verts[i + 1] = _rayPoints[i];
			cols[i + 1] = fillColor;

			int ti = i * 3;
			tris[ti] = 0;
			tris[ti + 1] = i + 1;
			tris[ti + 2] = (i + 1) % RayCount + 1;
		}

		var mesh = view.FillMesh;
		mesh.Clear();
		mesh.vertices = verts;
		mesh.colors = cols;
		mesh.triangles = tris;
		mesh.RecalculateBounds();

		view.FillFilter.mesh = mesh;
		view.FillRenderer.enabled = true;

		if (view.FillRenderer.material != null)
		{
			try { view.FillRenderer.material.color = fillColor; } catch { /* ignore */ }
		}
	}

	private static ViewObjects CreateViewObjects()
	{
		EnsureMaterials();

		var root = new GameObject(RootName);
		Object.DontDestroyOnLoad(root);

		var borderGo = new GameObject("Border");
		borderGo.transform.SetParent(root.transform, false);
		var lr = borderGo.AddComponent<LineRenderer>();
		lr.material = _lineMaterial;
		lr.useWorldSpace = true;
		lr.loop = false;
		lr.numCapVertices = 2;
		lr.numCornerVertices = 2;
		lr.sortingOrder = 1000;
		lr.positionCount = RayCount + 1;
		lr.alignment = LineAlignment.View;

		var fillGo = new GameObject("Fill");
		fillGo.transform.SetParent(root.transform, false);
		var mf = fillGo.AddComponent<MeshFilter>();
		var mr = fillGo.AddComponent<MeshRenderer>();
		mr.material = _fillMaterial;
		mr.sortingOrder = 999;
		try { mr.shadowCastingMode = ShadowCastingMode.Off; } catch { /* ignore */ }
		mr.receiveShadows = false;

		var mesh = new Mesh { name = "PlayerViewFill" };

		return new ViewObjects
		{
			Root = root,
			Border = lr,
			FillFilter = mf,
			FillRenderer = mr,
			FillMesh = mesh
		};
	}

	private static void EnsureMaterials()
	{
		if (_lineMaterial == null)
		{
			try
			{
				if (DestroyableSingleton<HatManager>.InstanceExists && HatManager.Instance != null)
					_lineMaterial = HatManager.Instance.PlayerMaterial;
			}
			catch { /* ignore */ }

			if (_lineMaterial == null)
			{
				var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
				if (shader != null)
					_lineMaterial = new Material(shader);
			}
		}

		if (_fillMaterial == null)
		{
			var shader = Shader.Find("Sprites/Default")
			             ?? Shader.Find("UI/Default")
			             ?? Shader.Find("Unlit/Transparent")
			             ?? Shader.Find("Unlit/Color");
			if (shader != null)
			{
				_fillMaterial = new Material(shader);
				try
				{
					_fillMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
					_fillMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
					_fillMaterial.SetInt("_ZWrite", 0);
					_fillMaterial.renderQueue = 3000;
				}
				catch { /* ignore */ }
			}
		}
	}

	private static void DestroyView(byte playerId)
	{
		if (!_views.TryGetValue(playerId, out var view)) return;
		try
		{
			if (view.FillMesh != null)
				Object.Destroy(view.FillMesh);
			if (view.Root != null)
				Object.Destroy(view.Root);
		}
		catch { /* ignore */ }
	}

	private static void CleanupAll()
	{
		if (_views.Count == 0)
		{
			_localInOtherView = false;
			return;
		}

		foreach (var id in new List<byte>(_views.Keys))
			DestroyView(id);
		_views.Clear();
		_localInOtherView = false;
	}
}
