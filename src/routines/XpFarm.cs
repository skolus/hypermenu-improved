using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using MalumMenu.features;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MalumMenu.routines
{
	public class XpFarmRoutine : IRoutine
	{
		private enum Phase
		{
			Idle,
			WaitLobby,
			ForceStart,
			WaitInGame,
			CompleteTasks,
			WaitBeforeFinish,
			ForceVictory,
			WaitEndScreen,
			PlayAgain,
			WaitBackToLobby
		}

		private const float LobbyReadyDelay = 0.3f;
		private const float EndScreenDelay = 0.3f;
		private const float PostPlayAgainDelay = 0.3f;
		private const float TaskCompleteInterval = 0.05f;
		private const float PhaseRetryInterval = 1.5f;
		private const float VictoryRetryInterval = 2.0f;

		public float waitBeforeEndSeconds = 60f;

		public string StatusText { get; private set; } = "Off";

		private Phase _phase = Phase.Idle;
		private float _timer;
		private float _victoryRetryTimer;
		private bool _tasksRunning;
		private bool _tasksFinished;
		private bool _wasEnabled;
		private Coroutine _taskCoroutine;
		private int _playAgainAttempts;
		private bool _loggedWaitingEndUi;
		private bool _playAgainInvoked;

		public XpFarmRoutine()
		{
			RoutineName = "XpFarm";
		}

		public override void Run()
		{
			if (!CheatToggles.xpFarm)
			{
				if (_wasEnabled)
				{
					StopTaskCoroutine();
					ResetCycle();
					_wasEnabled = false;
					StatusText = "Off";
					MalumMenu.Log.LogInfo("[XP Farm] Disabled by player");
				}
				return;
			}

			if (!_wasEnabled)
			{
				_wasEnabled = true;
				ResetCycle();
				_phase = Phase.WaitLobby;
				MalumMenu.notifications.Send("XP Farm", "XP Farm enabled. Stays on until you turn it off.", 5);
				MalumMenu.Log.LogInfo("[XP Farm] Enabled");
			}

			if (AmongUsClient.Instance == null)
			{
				StatusText = "Waiting for client connection…";
				return;
			}

			bool endFlow = _phase is Phase.ForceVictory or Phase.WaitEndScreen
				or Phase.PlayAgain or Phase.WaitBackToLobby;

			if (!endFlow)
			{
				if (PlayerControl.LocalPlayer == null)
				{
					StatusText = "Waiting for local player…";
					return;
				}

				if (!AmongUsClient.Instance.AmHost)
				{
					StatusText = "Paused — not host (toggle stays on)";
					return;
				}

				CheatToggles.noGameEnd = true;
			}
			else
			{
				Host.DisableGameEnd.Enabled = false;
				CheatToggles.noGameEnd = false;
			}

			_timer += Time.deltaTime;
			UpdateStatusText();

			switch (_phase)
			{
				case Phase.WaitLobby:
					if (!Utils.isLobby)
					{
						_timer = 0f;
						return;
					}

					if (_timer < LobbyReadyDelay) return;

					_phase = Phase.ForceStart;
					_timer = 0f;
					break;

				case Phase.ForceStart:
					if (!Utils.isLobby)
					{
						_phase = Phase.WaitInGame;
						_timer = 0f;
						return;
					}

					AmongUsClient.Instance.SendStartGame();
					MalumMenu.Log.LogInfo("[XP Farm] Force-started game");
					_phase = Phase.WaitInGame;
					_timer = 0f;
					break;

				case Phase.WaitInGame:
					if (Utils.isLobby)
					{
						if (_timer > 20f)
						{
							MalumMenu.Log.LogWarning("[XP Farm] Still in lobby after force-start — retrying");
							_phase = Phase.WaitLobby;
							_timer = 0f;
						}
						return;
					}

					if (!IsGameplayReady()) return;

					_phase = Phase.CompleteTasks;
					_timer = 0f;
					break;

				case Phase.CompleteTasks:
					if (!IsGameplayReady())
					{
						_phase = Phase.WaitEndScreen;
						_timer = 0f;
						_victoryRetryTimer = 0f;
						return;
					}

					if (!_tasksRunning && !_tasksFinished)
					{
						_tasksRunning = true;
						_tasksFinished = false;
						_taskCoroutine = MalumMenu.routines.StartCoroutine(CompleteAllTasksLikeModule().WrapToIl2Cpp());
					}

					if (_tasksFinished)
					{
						_tasksRunning = false;
						_phase = Phase.WaitBeforeFinish;
						_timer = 0f;
						MalumMenu.Log.LogInfo($"[XP Farm] Tasks done — waiting {waitBeforeEndSeconds:F0}s before ending");
					}
					break;

				case Phase.WaitBeforeFinish:
					if (_timer < Mathf.Max(0f, waitBeforeEndSeconds)) return;

					_phase = Phase.ForceVictory;
					_timer = 0f;
					break;

				case Phase.ForceVictory:
					ForceCrewmateVictory();
					_phase = Phase.WaitEndScreen;
					_timer = 0f;
					_victoryRetryTimer = 0f;
					_playAgainAttempts = 0;
					_playAgainInvoked = false;
					_loggedWaitingEndUi = false;
					break;

				case Phase.WaitEndScreen:
					if (!_loggedWaitingEndUi && _timer > 0.5f)
					{
						_loggedWaitingEndUi = true;
						MalumMenu.Log.LogInfo(
							$"[XP Farm] End-screen wait (GameState={AmongUsClient.Instance.GameState}, " +
							$"IsGameOver={AmongUsClient.Instance.IsGameOver}, " +
							$"LocalPlayer={(PlayerControl.LocalPlayer != null)}, " +
							$"nav={FindEndGameNavigation() != null}, mgr={FindEndGameManager() != null})");
					}

					if (!IsGameEnded() && (Utils.isInGame || ShipStatus.Instance != null))
					{
						_victoryRetryTimer += Time.deltaTime;
						if (_victoryRetryTimer >= VictoryRetryInterval)
						{
							_victoryRetryTimer = 0f;
							ForceCrewmateVictory();
						}
					}

					if (_timer < EndScreenDelay) return;

					_phase = Phase.PlayAgain;
					_timer = 0f;
					MalumMenu.Log.LogInfo("[XP Farm] End-screen wait done — attempting play again");
					break;

				case Phase.PlayAgain:
					if (TryPlayAgain())
					{
						_playAgainInvoked = true;
						MalumMenu.Log.LogInfo("[XP Farm] Play again requested");
						_phase = Phase.WaitBackToLobby;
						_timer = 0f;
					}
					else if (_timer >= PhaseRetryInterval)
					{
						_playAgainAttempts++;
						MalumMenu.Log.LogWarning($"[XP Farm] Play again not ready (attempt {_playAgainAttempts})");
						_timer = 0f;
					}
					break;

				case Phase.WaitBackToLobby:
					if (Utils.isLobby || AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Joined)
					{
						if (_timer < PostPlayAgainDelay) return;

						ResetCycle();
						_phase = Phase.WaitLobby;
						MalumMenu.Log.LogInfo("[XP Farm] Back in lobby — next cycle");
						return;
					}

					if (_timer >= 2.5f)
					{
						TryPlayAgain();
						_timer = 0f;
					}
					break;
			}

			UpdateStatusText();
		}

		private void UpdateStatusText()
		{
			float wait = Mathf.Max(0f, waitBeforeEndSeconds);
			float remain;

			switch (_phase)
			{
				case Phase.Idle:
					StatusText = "Off";
					break;
				case Phase.WaitLobby:
					remain = Mathf.Max(0f, LobbyReadyDelay - _timer);
					StatusText = Utils.isLobby
						? $"In lobby — force-start in {remain:F1}s"
						: "Waiting for lobby…";
					break;
				case Phase.ForceStart:
					StatusText = "Force-starting game…";
					break;
				case Phase.WaitInGame:
					StatusText = IsGameplayReady()
						? "Gameplay ready — starting tasks…"
						: "Waiting for intro / ship…";
					break;
				case Phase.CompleteTasks:
					StatusText = _tasksRunning
						? "Completing tasks…"
						: (_tasksFinished ? "Tasks complete" : "Starting task completion…");
					break;
				case Phase.WaitBeforeFinish:
					remain = Mathf.Max(0f, wait - _timer);
					StatusText = $"Waiting before end: {remain:F1}s left (set {wait:F0}s)";
					break;
				case Phase.ForceVictory:
					StatusText = "Forcing crewmate victory…";
					break;
				case Phase.WaitEndScreen:
					remain = Mathf.Max(0f, EndScreenDelay - _timer);
					StatusText = $"End screen — play-again in {remain:F1}s";
					break;
				case Phase.PlayAgain:
					StatusText = $"Play again… (try #{_playAgainAttempts + 1})";
					break;
				case Phase.WaitBackToLobby:
					StatusText = _playAgainInvoked
						? "Play again sent — waiting for lobby…"
						: "Waiting to return to lobby…";
					break;
				default:
					StatusText = _phase.ToString();
					break;
			}
		}

		private static bool IsGameplayReady()
		{
			return Utils.isInGame
			       && ShipStatus.Instance != null
			       && PlayerControl.LocalPlayer != null
			       && IntroCutscene.Instance == null
			       && !Utils.isMeeting;
		}

		private static bool IsGameEnded()
		{
			if (AmongUsClient.Instance == null) return false;

			if (AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Ended)
				return true;

			if (AmongUsClient.Instance.IsGameOver)
				return true;

			if (ShipStatus.Instance == null && !Utils.isLobby
			    && AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
				return true;

			return FindEndGameNavigation() != null || FindEndGameManager() != null;
		}

		private IEnumerator CompleteAllTasksLikeModule()
		{
			try
			{
				if (PlayerControl.LocalPlayer == null)
				{
					yield break;
				}

				var snapshot = PlayerControl.LocalPlayer.myTasks.ToArray();
				MalumMenu.Log.LogInfo($"[XP Farm] Completing {snapshot.Length} local tasks...");

				foreach (PlayerTask task in snapshot)
				{
					if (!CheatToggles.xpFarm) yield break;
					if (task == null || task.IsComplete) continue;
					if (IsSystemOrSabotageTask(task.TaskType)) continue;

					PlayerControl.LocalPlayer.RpcCompleteTask(task.Id);
					yield return Effects.Wait(TaskCompleteInterval);
				}
			}
			finally
			{
				_tasksFinished = true;
				_tasksRunning = false;
				_taskCoroutine = null;
			}
		}

		private static bool IsSystemOrSabotageTask(TaskTypes type)
		{
			return type is TaskTypes.ResetReactor or TaskTypes.RestoreOxy or TaskTypes.FixLights
				or TaskTypes.FixComms or TaskTypes.ResetSeismic or TaskTypes.StopCharles
				or TaskTypes.MushroomMixupSabotage;
		}

		private void ForceCrewmateVictory()
		{
			if (GameManager.Instance == null)
			{
				MalumMenu.Log.LogWarning("[XP Farm] GameManager missing — cannot end game");
				return;
			}

			Host.DisableGameEnd.Enabled = false;
			CheatToggles.noGameEnd = false;

			GameManager.Instance.RpcEndGame(GameOverReason.CrewmatesByTask, false);
			MalumMenu.Log.LogInfo("[XP Farm] Forced crewmate victory");
		}

		private bool TryPlayAgain()
		{
			var nav = FindEndGameNavigation();
			var manager = FindEndGameManager();

			if (nav == null && manager != null)
			{
				try { manager.ShowButtons(); } catch { /* ignore */ }
				nav = manager.Navigation;
			}

			if (nav == null)
			{
				MalumMenu.Log.LogWarning("[XP Farm] No EndGameNavigation — trying AmongUsClient.CoEndGame / scene search dump");
				try
				{
					int scenes = SceneManager.sceneCount;
					for (int i = 0; i < scenes; i++)
					{
						var sc = SceneManager.GetSceneAt(i);
						MalumMenu.Log.LogInfo($"[XP Farm] Scene[{i}] name={sc.name} loaded={sc.isLoaded} roots={sc.rootCount}");
					}
				}
				catch { /* ignore */ }

				return false;
			}

			MalumMenu.Log.LogInfo($"[XP Farm] Found EndGameNavigation on '{nav.gameObject.name}' active={nav.gameObject.activeInHierarchy}");

			try
			{
				nav.ShowDefaultNavigation();
				MalumMenu.Log.LogInfo("[XP Farm] ShowDefaultNavigation OK");
			}
			catch (System.Exception ex)
			{
				MalumMenu.Log.LogWarning($"[XP Farm] ShowDefaultNavigation: {ex.Message}");
			}

			if (nav.ContinueButton != null && nav.ContinueButton.activeInHierarchy)
			{
				if (TryClickPassive(nav.ContinueButton, "Continue"))
					return false; // wait a tick for play-again controls
			}

			if (nav.PlayAgainButton != null)
			{
				try
				{
					var go = nav.PlayAgainButton.gameObject;
					go.SetActive(true);
					if (TryClickPassive(go, "PlayAgain"))
						return true;
				}
				catch (System.Exception ex)
				{
					MalumMenu.Log.LogWarning($"[XP Farm] PlayAgain button: {ex.Message}");
				}
			}

			try
			{
				nav.NextGame();
				MalumMenu.Log.LogInfo("[XP Farm] Called EndGameNavigation.NextGame()");
				return true;
			}
			catch (System.Exception ex)
			{
				MalumMenu.Log.LogWarning($"[XP Farm] NextGame failed: {ex.Message}");
			}

			try
			{
				nav.StartCoroutine(nav.CoJoinGame());
				MalumMenu.Log.LogInfo("[XP Farm] Started CoJoinGame coroutine");
				return true;
			}
			catch (System.Exception ex)
			{
				MalumMenu.Log.LogWarning($"[XP Farm] CoJoinGame failed: {ex.Message}");
			}

			try
			{
				AmongUsClient.Instance.StartCoroutine(AmongUsClient.Instance.CoEndGame());
				MalumMenu.Log.LogInfo("[XP Farm] Started AmongUsClient.CoEndGame()");
			}
			catch (System.Exception ex)
			{
				MalumMenu.Log.LogWarning($"[XP Farm] CoEndGame failed: {ex.Message}");
			}

			return false;
		}

		private static bool TryClickPassive(GameObject go, string label)
		{
			if (go == null) return false;

			try
			{
				go.SetActive(true);

				var passive = go.GetComponent<PassiveButton>();
				if (passive == null)
					passive = go.GetComponentInChildren<PassiveButton>(true);

				if (passive == null)
				{
					var behavior = go.GetComponent<ButtonBehavior>();
					if (behavior == null)
						behavior = go.GetComponentInChildren<ButtonBehavior>(true);
					if (behavior != null)
					{
						behavior.ReceiveClick();
						MalumMenu.Log.LogInfo($"[XP Farm] Clicked {label} ButtonBehavior");
						return true;
					}
					return false;
				}

				passive.ReceiveClickDown();
				passive.ReceiveClickUp();

				if (passive.OnClick != null)
					passive.OnClick.Invoke();

				MalumMenu.Log.LogInfo($"[XP Farm] Clicked {label} PassiveButton");
				return true;
			}
			catch (System.Exception ex)
			{
				MalumMenu.Log.LogWarning($"[XP Farm] Click {label}: {ex.Message}");
				return false;
			}
		}

		private static EndGameNavigation FindEndGameNavigation()
		{
			return FindInLoadedScenes<EndGameNavigation>();
		}

		private static EndGameManager FindEndGameManager()
		{
			return FindInLoadedScenes<EndGameManager>();
		}

		private static T FindInLoadedScenes<T>() where T : Component
		{
			try
			{
				int count = SceneManager.sceneCount;
				for (int i = 0; i < count; i++)
				{
					var scene = SceneManager.GetSceneAt(i);
					if (!scene.isLoaded) continue;

					var roots = scene.GetRootGameObjects();
					foreach (var root in roots)
					{
						if (root == null) continue;
						var found = root.GetComponentInChildren<T>(true);
						if (found != null) return found;
					}
				}
			}
			catch (System.Exception ex)
			{
				MalumMenu.Log.LogWarning($"[XP Farm] Scene search for {typeof(T).Name}: {ex.Message}");
			}

			try
			{
				return Object.FindObjectOfType<T>(true);
			}
			catch
			{
				try { return Object.FindObjectOfType<T>(); }
				catch { return null; }
			}
		}

		private void StopTaskCoroutine()
		{
			if (_taskCoroutine != null && MalumMenu.routines != null)
			{
				MalumMenu.routines.StopCoroutine(_taskCoroutine);
			}
			_taskCoroutine = null;
			_tasksRunning = false;
			_tasksFinished = false;
		}

		private void ResetCycle()
		{
			StopTaskCoroutine();
			_phase = Phase.Idle;
			_timer = 0f;
			_victoryRetryTimer = 0f;
			_tasksRunning = false;
			_tasksFinished = false;
			_playAgainAttempts = 0;
			_playAgainInvoked = false;
			_loggedWaitingEndUi = false;
		}
	}
}
