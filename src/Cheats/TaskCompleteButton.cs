using UnityEngine;

namespace MalumMenu;

public static class TaskCompleteButton
{
	private static GUIStyle _btnStyle;
	private static Rect _btnRect;

	public static void OnGUI()
	{
		if (!CheatToggles.taskComplete) return;
		if (PlayerControl.LocalPlayer == null) return;
		if (AmongUsClient.Instance == null) return;

		if (Utils.isLobby && !Utils.isFreePlay) return;
		if (Utils.isMeeting) return;

		Color prevColor = GUI.color;
		bool prevEnabled = GUI.enabled;
		int prevDepth = GUI.depth;
		Matrix4x4 prevMatrix = GUI.matrix;

		try
		{
			GUI.depth = 10;
			GUI.matrix = Matrix4x4.identity;
			GUI.enabled = true;
			EnsureStyles();

			float size = Mathf.Clamp(Screen.height * 0.085f, 60f, 92f);
			float fromRight = 16f;
			float yCenter = (Screen.height - size) * 0.5f;
			_btnRect = new Rect(
				Screen.width - size - fromRight,
				yCenter,
				size,
				size);

			bool inTask = IsInCompletableTask(out _);
			GUI.color = inTask
				? new Color(0.15f, 0.8f, 0.25f, 0.95f)
				: new Color(0.25f, 0.45f, 0.28f, 0.75f);

			string label = inTask ? "COMPLETE" : "NOT IN\nA TASK";
			if (GUI.Button(_btnRect, label, _btnStyle))
			{
				TryCompleteCurrentStep();
			}
		}
		finally
		{
			GUI.color = prevColor;
			GUI.enabled = prevEnabled;
			GUI.depth = prevDepth;
			GUI.matrix = prevMatrix;
		}
	}

	private static bool IsInCompletableTask(out NormalPlayerTask normTask)
	{
		normTask = null;
		try
		{
			var mini = Minigame.Instance;
			if (mini == null) return false;

			normTask = mini.MyNormTask;
			if (normTask != null && !normTask.IsComplete)
				return true;

			if (mini.MyTask != null && !mini.MyTask.IsComplete)
				return true;
		}
		catch { /* ignore */ }

		return false;
	}

	private static void TryCompleteCurrentStep()
	{
		try
		{
			var mini = Minigame.Instance;
			if (mini == null)
			{
				MalumMenu.Log.LogInfo("[TaskComplete] No minigame open");
				return;
			}

			if (mini.MyNormTask != null)
			{
				var task = mini.MyNormTask;
				if (task.IsComplete)
				{
					mini.Close();
					return;
				}

				int before = task.taskStep;
				task.NextStep();
				MalumMenu.Log.LogInfo($"[TaskComplete] NextStep {before} → {task.taskStep} (Max={task.MaxStep}) IsComplete={task.IsComplete}");

				try { mini.Close(); } catch { try { mini.ForceClose(); } catch { /* ignore */ } }
				return;
			}

			if (mini.MyTask != null && !mini.MyTask.IsComplete)
			{
				mini.MyTask.Complete();
				MalumMenu.Log.LogInfo($"[TaskComplete] Complete() on {mini.MyTask.TaskType}");
				try { mini.Close(); } catch { try { mini.ForceClose(); } catch { /* ignore */ } }
				return;
			}

			MalumMenu.Log.LogInfo("[TaskComplete] Minigame has no player task — closing only");
			try { mini.Close(); } catch { /* ignore */ }
		}
		catch (System.Exception ex)
		{
			MalumMenu.Log.LogWarning($"[TaskComplete] {ex.Message}");
		}
	}

	private static void EnsureStyles()
	{
		if (_btnStyle != null) return;

		_btnStyle = new GUIStyle(GUI.skin != null ? GUI.skin.button : new GUIStyle())
		{
			fontSize = 14,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			wordWrap = true,
			richText = false
		};
		_btnStyle.normal.textColor = Color.white;
		_btnStyle.hover.textColor = Color.white;
		_btnStyle.active.textColor = Color.white;
	}
}
