using BepInEx;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.UI;

namespace LCHack
{
    [BepInPlugin("com.jabao.mod_menu", "Mod Menu", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource logSource;
        private void Awake()
        {
            var harmony = new Harmony("com.jabao.mod_menu");
            harmony.PatchAll();
        }


        internal class Hacks : MonoBehaviour
        {


            private Dictionary<Type, List<Component>> objectCache = new Dictionary<Type, List<Component>>();
            private float cacheRefreshInterval = 1.5f;
            private bool isMenuOpen = false;
            private int windowId = 69420;

            private bool cursorIsLocked = true;
            private bool insertKeyWasPressed = false;
            private float lastToggleTime = 0f;
            private const float toggleCooldown = 0.5f;

            private int enemyCount = 0;

            private string levelInput = "";

            private bool isESPEnabled = true;
            private bool isGrabbableObjectEspEnabled = true;
            private bool isEnemyEspEnabled = true;

            private bool addMoney = false;
            public bool isGodModeEnabled = false;
            public bool isUnlimitedScanRangeEnabled = false;
            public bool isUnlimitedItemPowerEnabled = false;
            public bool isInfiniteSprintEnabled = false;
            public bool isHighScrapValueEnabled = false;
            public bool isHighJumpEnabled = false;
            public bool isNightVisionEnabled = false;
            public bool isEnemySpawnableEnabled = false;

            // create a class to store dead player information
            public class DeadPlayerInfo
            {
                public string playerName;
                public float timeOfDeath;

                public DeadPlayerInfo(string playerName, float timeOfDeath)
                {
                    this.playerName = playerName;
                    this.timeOfDeath = timeOfDeath;
                }
            }

            // Maintain a list of dead players
            public List<DeadPlayerInfo> deadPlayers = new List<DeadPlayerInfo>();


            #region Keypress logic

            private const int VK_INSERT = 0x2D;

            [DllImport("user32.dll")]
            private static extern short GetAsyncKeyState(int vKey);

            private bool IsKeyDown(int keyCode)
            {
                return (GetAsyncKeyState(keyCode) & 0x8000) > 0;
            }

            #endregion

            #region Create singleton
            private static Hacks instance;

            // This is called when the game is loaded
            public void Awake()
            {
                if (Hacks.instance == null)
                {
                    Hacks.instance = this;
                    UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
                    return;
                }
                ManualLogSource logSource = BepInEx.Logging.Logger.CreateLogSource("com.jabao.mod_menu");
                UnityEngine.Object.Destroy(base.gameObject);
            }
            public static Hacks Instance
            {
                get
                {
                    if (Hacks.instance == null)
                    {
                        Hacks.instance = UnityEngine.Object.FindObjectOfType<Hacks>();
                        if (Hacks.instance == null)
                        {
                            Hacks.instance = new GameObject("HacksSingleton").AddComponent<Hacks>();
                        }
                    }
                    return Hacks.instance;
                }
            }
            #endregion
            // Start is called before the first frame update
            public void Start()
            {
                try
                {
                    var harmony = new Harmony("com.p1st.LCHack");
                    harmony.PatchAll();
                }
                catch (Exception ex)
                {
                    logSource.LogError("Error during Harmony patching: " + ex.Message);
                }

                StartCoroutine(CacheRefreshRoutine());

            }

            #region Cache
            IEnumerator CacheRefreshRoutine()
            {
                while (true)
                {
                    RefreshCache();
                    yield return new WaitForSeconds(cacheRefreshInterval);
                }
            }

            void RefreshCache()
            {
                objectCache.Clear();
                CacheObjects<EntranceTeleport>();
                CacheObjects<GrabbableObject>();
                CacheObjects<Landmine>();
                CacheObjects<Turret>();
                CacheObjects<Terminal>();
                CacheObjects<PlayerControllerB>();
                CacheObjects<SteamValveHazard>();
                CacheObjects<EnemyAI>();

                UpdateEnemyCount();
                UpdateDeadPlayerInfo();
            }

            void UpdateDeadPlayerInfo()
            {
                if (objectCache.TryGetValue(typeof(PlayerControllerB), out var players))
                {
                    foreach (PlayerControllerB player in players.Cast<PlayerControllerB>())
                    {
                        if (player.isPlayerDead && !deadPlayers.Any(deadPlayer => deadPlayer.playerName == player.playerUsername))
                        {
                            deadPlayers.Add(new DeadPlayerInfo(player.playerUsername, Time.time));
                        }
                        if (!player.isPlayerDead && deadPlayers.Any(deadPlayer => deadPlayer.playerName == player.playerUsername))
                        {
                            deadPlayers.RemoveAll(deadPlayer => deadPlayer.playerName == player.playerUsername);
                        }
                    }
                }
                else
                {
                    deadPlayers.Clear();
                }
            }

            void UpdateEnemyCount()
            {
                if (objectCache.TryGetValue(typeof(EnemyAI), out var enemies))
                {
                    enemyCount = enemies.Count;
                }
                else
                {
                    enemyCount = 0;
                }
            }

            void CacheObjects<T>() where T : Component
            {
                objectCache[typeof(T)] = new List<Component>(FindObjectsOfType<T>());
            }
            #endregion

            #region ESP Drawing
            //This is a copy of the WorldToScreen function from the game's Camera class
            public static bool WorldToScreen(Camera camera, Vector3 world, out Vector3 screen)
            {
                screen = camera.WorldToViewportPoint(world);

                screen.x *= Screen.width;
                screen.y *= Screen.height;

                screen.y = Screen.height - screen.y;

                return screen.z > 0;
            }
            //ProcessObjects is a generic function that takes a type and a function that builds a label for that type

            private void ProcessObjects<T>(Func<T, Vector3, string> labelBuilder) where T : Component
            {
                if (!objectCache.TryGetValue(typeof(T), out var cachedObjects))
                    return;

                foreach (T obj in cachedObjects.Cast<T>())
                {
                    if (obj is GrabbableObject GO && (GO.isPocketed || GO.isHeld))
                    {
                        continue;
                    }

                    if (obj is GrabbableObject GO2 && GO2.itemProperties.itemName is "clipboard" or "Sticky note")
                    {
                        continue;
                    }

                    if (obj is SteamValveHazard valve && valve.triggerScript.interactable == false)
                    {
                        continue;
                    }

                    if (obj is Terminal terminal && addMoney)
                    {
                        if (GameNetworkManager.Instance.localPlayerController.IsServer)
                        {
                            terminal.groupCredits += 300;
                            addMoney = false;
                        }
                        else
                        {
                            terminal.groupCredits += 300;
                            terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);
                            addMoney = false;
                        }

                    }

                    Vector3 screen;

                    if (WorldToScreen(GameNetworkManager.Instance.localPlayerController.gameplayCamera,
                            obj.transform.position, out screen))
                    {
                        string label = labelBuilder(obj, screen);
                        float distance = Vector3.Distance(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, obj.transform.position);
                        distance = (float)Math.Round(distance);
                        DrawLabel(screen, label, GetColorForObject<T>(), distance);
                    }
                }
            }
           
            //ProcessPlayers is a special case of ProcessObjects that only processes PlayerControllerB objects
            private void ProcessPlayers()
            {
                if (!objectCache.TryGetValue(typeof(PlayerControllerB), out var cachedPlayers))
                    return;

                foreach (PlayerControllerB player in cachedPlayers.Cast<PlayerControllerB>())
                {
                    if (player.isPlayerDead || player.IsLocalPlayer || player.playerUsername == GameNetworkManager.Instance.localPlayerController.playerUsername || player.disconnectedMidGame)
                    {
                        continue;
                    }

                    Vector3 screen;
                    if (WorldToScreen(GameNetworkManager.Instance.localPlayerController.gameplayCamera,
                            player.transform.position, out screen))
                    {
                        string label = player.playerUsername + " ";
                        float distance = Vector3.Distance(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, player.transform.position);
                        distance = (float)Math.Round(distance);
                        DrawLabel(screen, label, Color.green, distance);
                    }
                }
            }

            //ProcessEnemies is a special case of ProcessObjects that only processes EnemyAI objects
            private void ProcessEnemies()
            {

                if (!objectCache.TryGetValue(typeof(EnemyAI), out var cachedEnemies))
                    return;

                Action<EnemyAI> processEnemy = enemyAI =>
                {
                    Vector3 screen;
                    if (WorldToScreen(GameNetworkManager.Instance.localPlayerController.gameplayCamera,
                            enemyAI.eye.transform.position, out screen))
                    {
                        string label;
                        if (string.IsNullOrWhiteSpace(enemyAI.enemyType.enemyName))
                        {
                            label = "Unknown Enemy ";
                        }
                        else
                            label = enemyAI.enemyType.enemyName + " ";
                        float distance = Vector3.Distance(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, enemyAI.eye.transform.position);
                        distance = (float)Math.Round(distance);
                        DrawLabel(screen, label, Color.red, distance);
                    }
                };

                foreach (EnemyAI enemyAI in cachedEnemies.Cast<EnemyAI>())
                {
                    processEnemy(enemyAI);
                }
            }

            //DrawLabel is a helper function that draws a label on the screen
            private void DrawLabel(Vector3 screenPosition, string text, Color color, float distance)
            {
                GUI.contentColor = color;
                GUI.Label(new Rect(screenPosition.x, screenPosition.y, 75f, 50f), text + distance + "m");
            }

            //GetColorForObject is a helper function that returns a color for a given type
            private Color GetColorForObject<T>()
            {
                switch (typeof(T).Name)
                {
                    case "EntranceTeleport":
                        return Color.cyan;
                    case "GrabbableObject":
                        return Color.blue;
                    case "Landmine":
                        return Color.red;
                    case "Turret":
                        return Color.red;
                    case "SteamValveHazard":
                        return Color.yellow;
                    case "Terminal":
                        return Color.magenta;
                    default:
                        return Color.white;
                }
            }

            #endregion
            // OnGUI is called once per frame
            public void OnGUI()
            {
                if (StartOfRound.Instance != null)
                {
                    GUI.Label(new Rect(10f, 25f, 200f, 30f), $"Enemy count: {enemyCount}");
                    GUI.Label(new Rect(10f, 5f, 200f, 30f), "Dead Player");
                    
                   
                        float labelHeight = 25f;
                        float padding = 5f;

                        foreach (DeadPlayerInfo deadPlayer in deadPlayers)
                        {
                            float y = 45f + deadPlayers.IndexOf(deadPlayer) * (labelHeight + padding);
                            GUI.Label(new Rect(10f, y, 200f, labelHeight), $"Dead player: {deadPlayer.playerName} ({deadPlayer.timeOfDeath})");
                        }
                   
            
                }
                


                if (isMenuOpen)
                {
                    Rect windowRect = new Rect(100, 100, 300, 500); // Adjust size and position as needed
                    windowRect = GUILayout.Window(windowId, windowRect, DrawMenuWindow, "Lethal Company");
                }

                if (isESPEnabled)
                {
                    ProcessObjects<EntranceTeleport>((entrance, vector) => entrance.isEntranceToBuilding ? " Entrance " : " Exit ");
                    ProcessObjects<Landmine>((landmine, vector) => "LANDMINE ");
                    ProcessObjects<Turret>((turret, vector) => "TURRET ");
                    ProcessObjects<Terminal>((terminal, vector) => "SHIP TERMINAL ");
                    ProcessObjects<SteamValveHazard>((valve, vector) => "Steam Valve ");
                    ProcessPlayers();

                    if (isGrabbableObjectEspEnabled)
                    {
                        ProcessObjects<GrabbableObject>((grabbableObject, vector) => grabbableObject.itemProperties.itemName + " ");
                    }

                    if (isEnemyEspEnabled)
                    {
                        ProcessEnemies();
                    }
                }

                if (StartOfRound.Instance != null)
                {
                    if (isUnlimitedItemPowerEnabled)
                    {
                        if (GameNetworkManager.Instance.localPlayerController.currentlyHeldObjectServer != null)
                        {
                            if (GameNetworkManager.Instance.localPlayerController.IsServer)
                                GameNetworkManager.Instance.localPlayerController.currentlyHeldObjectServer.insertedBattery.charge = 1f;
                        }
                    }
                }

            }

            void Update()
            {
                bool isKeyDown = IsKeyDown(VK_INSERT);

                if (isKeyDown && !insertKeyWasPressed && Time.time - lastToggleTime > toggleCooldown)
                {
                    isMenuOpen = !isMenuOpen;
                    lastToggleTime = Time.time;
                }

                if (StartOfRound.Instance != null)
                {
                    if (isMenuOpen)
                    {
                        // Menu opened, unlock cursor
                        Cursor.visible = true;
                        Cursor.lockState = CursorLockMode.None;
                        cursorIsLocked = false;
                    }
                    else if (!cursorIsLocked)
                    {
                        // To prevent not being able to use ESC menu. We only free up the cursor once
                        Cursor.visible = false;
                        Cursor.lockState = CursorLockMode.Locked;
                        cursorIsLocked = true;
                    }
                }

                insertKeyWasPressed = isKeyDown;
            }



            void DrawMenuWindow(int id)
            {
                GUILayout.BeginHorizontal();

                // Left Column
                GUILayout.BeginVertical();

                GUILayout.Label($"Master ESP: {(isESPEnabled ? "enabled" : "disabled")}");
                GUILayout.Label($"Item ESP: {(isGrabbableObjectEspEnabled ? "enabled" : "disabled")}");
                GUILayout.Label($"Enemy ESP: {(isEnemyEspEnabled ? "enabled" : "disabled")}");

                GUILayout.Label($"God mode: {(isGodModeEnabled ? "enabled" : "disabled")}");
                GUILayout.Label($"Infinite sprint: {(isInfiniteSprintEnabled ? "enabled" : "disabled")}");
                GUILayout.Label($"Night Vision: {(isNightVisionEnabled ? "enabled" : "disabled")}");
                GUILayout.Label($"High jump: {(isHighJumpEnabled ? "enabled" : "disabled")}");
                GUILayout.Label($"Spawn Enemy: {(isEnemySpawnableEnabled ? "enabled" : "disabled")}");
         
                GUILayout.Label($"Unlimited Scan Range: {(isUnlimitedScanRangeEnabled ? "enabled" : "disabled")}");
                GUILayout.Label($"Unlimited Item Power: {(isUnlimitedItemPowerEnabled ? "enabled" : "disabled")}");
                GUILayout.Label($"High Scrap Value: {(isHighScrapValueEnabled ? "enabled" : "disabled")}");

                GUILayout.EndVertical();

                // Right Column
                GUILayout.BeginVertical();

                if (GUILayout.Button("Toggle God mode"))
                {
                    isGodModeEnabled = !isGodModeEnabled;
                }

                if (GUILayout.Button("Toggle Infinite Sprint"))
                {
                    isInfiniteSprintEnabled = !isInfiniteSprintEnabled;
                }

                if (GUILayout.Button("Toggle Night Vision"))
                {
                    isNightVisionEnabled = !isNightVisionEnabled;
                }

                if (GUILayout.Button("High Jump"))
                {
                    isHighJumpEnabled = !isHighJumpEnabled;
                }

                if (GUILayout.Button("High Jump"))
                {
                    isHighJumpEnabled = !isHighJumpEnabled;
                }

                if (GUILayout.Button("Toggle All ESP"))
                {
                    isESPEnabled = !isESPEnabled;
                }

                if (GUILayout.Button("Toggle Item ESP"))
                {
                    isGrabbableObjectEspEnabled = !isGrabbableObjectEspEnabled;
                }

                if (GUILayout.Button("Toggle Enemy ESP"))
                {
                    isEnemyEspEnabled = !isEnemyEspEnabled;
                }

                if (GUILayout.Button("Unlimited Scan Range"))
                {
                    isUnlimitedScanRangeEnabled = !isUnlimitedScanRangeEnabled;
                }

                if (GUILayout.Button("Add 200 Cash"))
                {
                    addMoney = true;
                }

                GUILayout.Label("Add EXP Input (1500+ is BOSS level)");
                levelInput = GUILayout.TextField(levelInput, 4);

                if (GUILayout.Button("Set Level (Must be in a lobby)"))
                {
                    if (HUDManager.Instance != null)
                    {
                        if (int.TryParse(levelInput, out int expValue))
                        {
                            SetLevelToExp(expValue);
                        }
                    }
                }

                GUILayout.Label($"When non-host, drop the item on the ground and pick it back up for a full charge.");
                if (GUILayout.Button("Toggle Unlimited Item Power"))
                {
                    isUnlimitedItemPowerEnabled = !isUnlimitedItemPowerEnabled;
                }

                GUILayout.Label($"Host only features:");

                if (GUILayout.Button("Set Quota Reached"))
                {
                    if (TimeOfDay.Instance != null)
                    {
                        TimeOfDay.Instance.quotaFulfilled = TimeOfDay.Instance.profitQuota;
                        TimeOfDay.Instance.UpdateProfitQuotaCurrentTime();
                    }
                }

                if (GUILayout.Button("High scrap value"))
                {
                    isHighScrapValueEnabled = !isHighScrapValueEnabled;
                }

                GUILayout.EndVertical();

                GUILayout.EndHorizontal();

                GUI.DragWindow();
            }

            private void SetLevelToExp(int expValue)
            {
                StartCoroutine(HUDManager.Instance.SetPlayerLevelSmoothly(expValue));
            }
        }
    }
}