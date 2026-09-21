using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarcraftTD
{
    // Playable first-mission vertical slice. No asset downloads are needed.
    public sealed class PrototypeGame : MonoBehaviour
    {
        private const int Width = 24;
        private const int Height = 14;
        private const float CellSize = 2f;
        private static readonly Vector2Int SpawnCell = new Vector2Int(1, 7);
        private static readonly Vector2Int BaseCell = new Vector2Int(22, 7);
        private readonly List<Tower> towers = new List<Tower>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly int[] builtByType = new int[3];
        private Camera gameCamera;
        private LineRenderer routeLine;
        private FirstMissionFlow missionFlow;
        private int selectedType = -1;
        private int gold = 100;
        private int baseHealth = 20;
        private int spawned;
        private float preparation = 60f;
        private float spawnClock;
        private float cameraYaw = 0f;
        private float cameraPitch = 55f;
        private float cameraDistance = 36f;
        private bool waveStarted;
        private bool finished;
        private bool smokeTest;
        private bool missionTest;
        private bool paladinRescue;
        private float smokeTestStartedAt;
        private int wallCharges;
        private string status = "Выберите башню и коснитесь клетки. Волна начнётся через минуту.";

        private enum TowerKind { Archer, Ballista, Wall }

        private sealed class Tower
        {
            public TowerKind kind;
            public Vector2Int cell;
            public GameObject visual;
            public float health = 40f;
            public float cooldown;
        }

        private sealed class Enemy
        {
            public GameObject visual;
            public float health = 36f;
            public float cooldown;
            public float pathClock;
            public ArmorType armor;
            public List<Vector2Int> path = new List<Vector2Int>();
            public int step;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartPrototype()
        {
            if (FindFirstObjectByType<PrototypeGame>() != null) return;
            new GameObject("Warcraft TD Prototype").AddComponent<PrototypeGame>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            string[] arguments = Environment.GetCommandLineArgs();
            smokeTest = Array.Exists(arguments, argument => argument == "-warcraftTdSmokeTest");
            missionTest = Array.Exists(arguments, argument => argument == "-warcraftTdMissionTest");
            if (missionTest) preparation = 0.05f;
            smokeTestStartedAt = Time.realtimeSinceStartup;
            wallCharges = PlayerPrefs.GetInt("WallsUnlocked", 0) == 1 ? 3 : 0;
            BuildWorld();
            RefreshRoutePreview();
            missionFlow = gameObject.AddComponent<FirstMissionFlow>();
            missionFlow.Initialize(this);
        }

        private void Update()
        {
            if (smokeTest && Time.realtimeSinceStartup - smokeTestStartedAt >= 2f)
            {
                Debug.Log("WARCRAFT_TD_SMOKE_OK");
                smokeTest = false;
                Application.Quit(0);
                return;
            }
            if (finished) return;
            float dt = Time.deltaTime;
            HandleCamera();
            HandlePlacement();
            missionFlow.Tick();
            if (!waveStarted)
            {
                preparation -= dt;
                if (preparation <= 0f) StartWave();
            }
            else
            {
                spawnClock -= dt;
                if (spawned < 10 && spawnClock <= 0f)
                {
                    SpawnEnemy();
                    spawnClock = missionTest ? 0.05f : 1.2f;
                }
                UpdateEnemies(dt);
                UpdateTowers(dt);
                if (spawned == 10 && enemies.Count == 0 && baseHealth > 0 && !paladinRescue)
                {
                    CompleteMission("Волна отбита. Миссия пройдена, строительство стен открыто.");
                }
            }
        }

        internal bool IsFinished => finished;
        internal bool IsWaveStarted => waveStarted;
        internal float PreparationRemaining => preparation;

        internal void SetStatus(string message)
        {
            status = message;
        }

        private void BuildWorld()
        {
            var oldCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var oldCamera in oldCameras) oldCamera.gameObject.SetActive(false);
            foreach (var oldLight in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (oldLight.type == LightType.Directional) oldLight.gameObject.SetActive(false);
            var cameraObject = new GameObject("Orbit Camera");
            gameCamera = cameraObject.AddComponent<Camera>();
            gameCamera.clearFlags = CameraClearFlags.Skybox;
            gameCamera.fieldOfView = 48f;
            cameraObject.AddComponent<AudioListener>();
            UpdateCameraPosition();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(-CellSize / 2f, -0.15f, -CellSize / 2f);
            ground.transform.localScale = new Vector3(Width * CellSize, 0.3f, Height * CellSize);
            Paint(ground, new Color(0.25f, 0.36f, 0.25f));
            MakeMarker("A — враги", SpawnCell, Color.red, 1.4f);
            MakeMarker("Б — база", BaseCell, new Color(0.2f, 0.55f, 1f), 2.1f);

            var routeObject = new GameObject("Route preview");
            routeLine = routeObject.AddComponent<LineRenderer>();
            routeLine.positionCount = 0;
            routeLine.startWidth = routeLine.endWidth = 0.18f;
            routeLine.useWorldSpace = true;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) routeLine.material = new Material(shader);
        }

        private void MakeMarker(string name, Vector2Int cell, Color color, float scale)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.position = CellWorld(cell) + Vector3.up * 0.45f;
            marker.transform.localScale = new Vector3(scale, 0.45f, scale);
            Paint(marker, color);
        }

        private void HandlePlacement()
        {
            if (selectedType < 0) return;
            Vector2 screenPoint;
            if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended)
                screenPoint = Input.GetTouch(0).position;
            else if (Input.GetMouseButtonDown(0))
                screenPoint = Input.mousePosition;
            else return;

            if (screenPoint.x < 250f || screenPoint.y > Screen.height - 100f) return;
            var ray = gameCamera.ScreenPointToRay(screenPoint);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            var cell = WorldCell(ray.GetPoint(distance));
            if (!Inside(cell) || cell == SpawnCell || cell == BaseCell || TowerAt(cell) != null)
            {
                status = "Здесь строить нельзя.";
                return;
            }

            bool placingWall = selectedType == (int)TowerKind.Wall;
            if (placingWall && wallCharges <= 0)
            {
                status = "Запас стен закончился.";
                return;
            }

            int price = placingWall ? 0 : 10 * (builtByType[selectedType] + 1);
            if (gold < price)
            {
                status = "Недостаточно золота.";
                return;
            }
            gold -= price;
            builtByType[selectedType]++;
            var kind = (TowerKind)selectedType;
            if (placingWall) wallCharges--;
            var visual = GameObject.CreatePrimitive(kind == TowerKind.Archer ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            visual.name = kind switch
            {
                TowerKind.Archer => "Archer Tower",
                TowerKind.Ballista => "Ballista Tower",
                _ => "Stone Wall"
            };
            visual.transform.position = CellWorld(cell) + Vector3.up * 1.1f;
            visual.transform.localScale = kind switch
            {
                TowerKind.Archer => new Vector3(1.1f, 1.1f, 1.1f),
                TowerKind.Ballista => new Vector3(1.5f, 2.2f, 1.5f),
                _ => new Vector3(1.8f, 1.8f, 1.8f)
            };
            Paint(visual, kind switch
            {
                TowerKind.Archer => new Color(0.82f, 0.72f, 0.45f),
                TowerKind.Ballista => new Color(0.6f, 0.55f, 0.45f),
                _ => new Color(0.45f, 0.48f, 0.52f)
            });
            towers.Add(new Tower
            {
                kind = kind,
                cell = cell,
                visual = visual,
                health = placingWall ? 120f : 40f
            });
            status = placingWall
                ? $"Каменная стена установлена. Осталось: {wallCharges}."
                : "Башня построена. Цена следующей этого типа увеличилась на 10.";
            RefreshRoutePreview();
            foreach (var enemy in enemies) enemy.pathClock = 0f;
        }

        private void StartWave()
        {
            waveStarted = true;
            spawnClock = 0f;
            status = "Волна людей-зомби началась. Башни можно строить во время волны.";
            routeLine.startColor = routeLine.endColor = Color.green;
        }

        private void SpawnEnemy()
        {
            ArmorType armor = spawned < 4 ? ArmorType.Light :
                spawned < 7 ? ArmorType.Medium : ArmorType.Heavy;
            bool finalBrute = spawned == 9;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = finalBrute ? "Armored Zombie Brute" : $"Zombie Human ({armor})";
            visual.transform.position = CellWorld(SpawnCell) + Vector3.up;
            visual.transform.localScale = finalBrute
                ? new Vector3(1.25f, 1.45f, 1.25f)
                : new Vector3(0.8f, 1f, 0.8f);
            Paint(visual, armor switch
            {
                ArmorType.Light => new Color(0.44f, 0.58f, 0.42f),
                ArmorType.Medium => new Color(0.38f, 0.46f, 0.36f),
                _ => new Color(0.30f, 0.34f, 0.30f)
            });
            float health = finalBrute ? 1200f : armor switch
            {
                ArmorType.Light => 36f,
                ArmorType.Medium => 52f,
                _ => 74f
            };
            enemies.Add(new Enemy { visual = visual, armor = armor, health = health });
            spawned++;
        }

        private void UpdateEnemies(float dt)
        {
            if (paladinRescue) return;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                enemy.pathClock -= dt;
                enemy.cooldown -= dt;
                if (enemy.pathClock <= 0f)
                {
                    enemy.path = GridPathfinder.Find(Width, Height,
                        WorldCell(enemy.visual.transform.position), BaseCell,
                        cell =>
                        {
                            var blocker = TowerAt(cell);
                            return blocker == null ? 1f : 1f + blocker.health / 8f;
                        });
                    enemy.step = enemy.path.Count > 1 ? 1 : 0;
                    enemy.pathClock = 0.6f;
                }

                if (enemy.path.Count == 0 || enemy.step >= enemy.path.Count) continue;
                Vector2Int next = enemy.path[enemy.step];
                var tower = TowerAt(next);
                if (tower != null)
                {
                    if (enemy.cooldown <= 0f)
                    {
                        tower.health -= 8f;
                        enemy.cooldown = 1f;
                        if (tower.health <= 0f)
                        {
                            towers.Remove(tower);
                            Destroy(tower.visual);
                            enemy.pathClock = 0f;
                            RefreshRoutePreview();
                        }
                    }
                    continue;
                }

                Vector3 destination = CellWorld(next) + Vector3.up;
                enemy.visual.transform.position = Vector3.MoveTowards(
                    enemy.visual.transform.position, destination, (missionTest ? 26f : 2.6f) * dt);
                if (Vector3.Distance(enemy.visual.transform.position, destination) < 0.04f)
                    enemy.step++;
                if (Vector3.Distance(enemy.visual.transform.position,
                        CellWorld(BaseCell) + Vector3.up) < 0.3f)
                {
                    Destroy(enemy.visual);
                    enemies.RemoveAt(i);
                    baseHealth--;
                    if (baseHealth <= 0)
                    {
                        finished = true;
                        status = "База разрушена. Перезапустите сцену для новой попытки.";
                    }
                }
            }
        }

        internal bool ShouldPaladinIntervene()
        {
            if (paladinRescue || spawned < 10) return false;
            Vector3 basePosition = CellWorld(BaseCell) + Vector3.up;
            foreach (var enemy in enemies)
                if (Vector3.Distance(enemy.visual.transform.position, basePosition) <= 11f)
                    return true;
            return false;
        }

        internal void StartPaladinRescue()
        {
            if (paladinRescue || finished) return;
            paladinRescue = true;
            selectedType = -1;

            var paladin = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            paladin.name = "Paladin";
            paladin.transform.position = CellWorld(new Vector2Int(20, 9)) + Vector3.up;
            paladin.transform.localScale = new Vector3(1.15f, 1.3f, 1.15f);
            Paint(paladin, new Color(0.95f, 0.84f, 0.30f));

            AddPaladinWall(new Vector2Int(19, 6));
            AddPaladinWall(new Vector2Int(19, 7));
            AddPaladinWall(new Vector2Int(19, 8));
            RefreshRoutePreview();
            status = "Паладин: «Они почти у базы! Свет защитит нас!» Паладин возводит каменные стены.";
            Invoke(nameof(FinishPaladinRescue), 4f);
        }

        private void AddPaladinWall(Vector2Int cell)
        {
            if (TowerAt(cell) != null) return;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Paladin Stone Wall";
            visual.transform.position = CellWorld(cell) + Vector3.up * 1.1f;
            visual.transform.localScale = new Vector3(1.85f, 2.1f, 1.85f);
            Paint(visual, new Color(0.72f, 0.72f, 0.78f));
            towers.Add(new Tower
            {
                kind = TowerKind.Wall,
                cell = cell,
                visual = visual,
                health = 600f
            });
        }

        private void FinishPaladinRescue()
        {
            foreach (var enemy in enemies)
                if (enemy.visual != null) Destroy(enemy.visual);
            enemies.Clear();
            CompleteMission("Миссия пройдена. Паладин спас базу; теперь вам доступны каменные стены.");
        }

        private void CompleteMission(string message)
        {
            finished = true;
            PlayerPrefs.SetInt("WallsUnlocked", 1);
            PlayerPrefs.Save();
            status = message;
            if (missionTest)
            {
                Debug.Log("WARCRAFT_TD_MISSION_OK");
                Application.Quit(0);
            }
        }

        private void UpdateTowers(float dt)
        {
            foreach (var tower in towers)
            {
                if (tower.kind == TowerKind.Wall) continue;
                tower.cooldown -= dt;
                if (tower.cooldown > 0f) continue;
                Enemy target = null;
                float best = 6f * 6f;
                foreach (var enemy in enemies)
                {
                    float sqrDistance = (enemy.visual.transform.position - tower.visual.transform.position).sqrMagnitude;
                    if (sqrDistance >= best) continue;
                    best = sqrDistance;
                    target = enemy;
                }
                if (target == null) continue;
                DamageType damageType = tower.kind == TowerKind.Archer
                    ? DamageType.Normal : DamageType.Piercing;
                float rawDamage = tower.kind == TowerKind.Archer ? 6f : 12f;
                target.health -= CombatRules.Resolve(rawDamage, damageType, target.armor);
                tower.cooldown = tower.kind == TowerKind.Archer ? 0.8f : 1.5f;
                if (target.health > 0f) continue;
                gold += 10;
                Destroy(target.visual);
                enemies.Remove(target);
            }
        }

        private void RefreshRoutePreview()
        {
            var path = GridPathfinder.Find(Width, Height, SpawnCell, BaseCell,
                cell => TowerAt(cell) == null ? 1f : 6f);
            routeLine.positionCount = path.Count;
            routeLine.startColor = routeLine.endColor = waveStarted ? Color.green : Color.red;
            for (int i = 0; i < path.Count; i++)
                routeLine.SetPosition(i, CellWorld(path[i]) + Vector3.up * 0.22f);
        }

        private void HandleCamera()
        {
            if (Input.GetMouseButton(1))
            {
                cameraYaw += Input.GetAxis("Mouse X") * 3f;
                cameraPitch = Mathf.Clamp(cameraPitch - Input.GetAxis("Mouse Y") * 3f, 25f, 80f);
            }
            cameraDistance = Mathf.Clamp(cameraDistance - Input.mouseScrollDelta.y * 3f, 18f, 65f);
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                cameraYaw += a.deltaPosition.x * 0.08f;
                cameraPitch = Mathf.Clamp(cameraPitch - a.deltaPosition.y * 0.08f, 25f, 80f);
                Vector2 previousA = a.position - a.deltaPosition;
                Vector2 previousB = b.position - b.deltaPosition;
                cameraDistance = Mathf.Clamp(cameraDistance -
                    (Vector2.Distance(a.position, b.position) - Vector2.Distance(previousA, previousB)) * 0.04f,
                    18f, 65f);
            }
            UpdateCameraPosition();
        }

        private void UpdateCameraPosition()
        {
            Vector3 focus = Vector3.zero;
            Quaternion rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            gameCamera.transform.position = focus + rotation * new Vector3(0f, 0f, -cameraDistance);
            gameCamera.transform.LookAt(focus);
        }

        private void OnGUI()
        {
            bool wallsUnlocked = PlayerPrefs.GetInt("WallsUnlocked", 0) == 1;
            GUI.Box(new Rect(10f, 10f, 255f, wallsUnlocked ? 250f : 202f), "Миссия 1 — Последний рубеж");
            GUI.Label(new Rect(22f, 38f, 235f, 25f), $"Золото: {gold}    База: {baseHealth}/20");
            GUI.Label(new Rect(22f, 62f, 235f, 25f), waveStarted
                ? $"Волна: {spawned}/10" : $"До волны: {Mathf.CeilToInt(preparation)} с");
            if (GUI.Button(new Rect(22f, 92f, 225f, 40f),
                    $"Лучник — {10 * (builtByType[0] + 1)} золота")) selectedType = 0;
            if (GUI.Button(new Rect(22f, 140f, 225f, 40f),
                    $"Баллиста — {10 * (builtByType[1] + 1)} золота")) selectedType = 1;
            if (wallsUnlocked && GUI.Button(new Rect(22f, 188f, 225f, 40f),
                    $"Каменная стена — осталось {wallCharges}")) selectedType = 2;
            GUI.Box(new Rect(10f, Screen.height - 62f, Screen.width - 20f, 52f), status);
        }

        private Tower TowerAt(Vector2Int cell)
        {
            foreach (var tower in towers)
                if (tower.cell == cell) return tower;
            return null;
        }

        private static bool Inside(Vector2Int cell) =>
            cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

        private static Vector3 CellWorld(Vector2Int cell) =>
            new Vector3((cell.x - Width / 2) * CellSize, 0f,
                (cell.y - Height / 2) * CellSize);

        private static Vector2Int WorldCell(Vector3 point) =>
            new Vector2Int(Mathf.RoundToInt(point.x / CellSize + Width / 2f),
                Mathf.RoundToInt(point.z / CellSize + Height / 2f));

        private static void Paint(GameObject visual, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;
            var material = new Material(shader) { color = color };
            visual.GetComponent<Renderer>().material = material;
        }
    }
}
