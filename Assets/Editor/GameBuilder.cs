using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ZombieFPS.Editor
{
    /// <summary>
    /// Generates materials, prefabs and the main scene (level, navmesh, spawn points, player, game systems).
    /// Menu: Tools > Rebuild Game Scene.
    /// </summary>
    public static class GameBuilder
    {
        const string MaterialsFolder = "Assets/Materials";
        const string PrefabsFolder = "Assets/Prefabs";
        const string ScenesFolder = "Assets/Scenes";
        const string ScenePath = ScenesFolder + "/Main.unity";
        const string NavMeshPath = ScenesFolder + "/Main_NavMesh.asset";

        const float WallHeight = 5f;
        const float SafeWallHeight = 3.5f;
        const float WallThickness = 0.5f;
        const float LaneHalfWidth = 2.2f;

        // ---------------------------------------------------------------- Layout

        struct Area
        {
            public int zone;
            public float xMin, zMin, xMax, zMax;
            public bool safe;

            public Area(int zone, float xMin, float zMin, float xMax, float zMax, bool safe = false)
            {
                this.zone = zone;
                this.xMin = xMin;
                this.zMin = zMin;
                this.xMax = xMax;
                this.zMax = zMax;
                this.safe = safe;
            }

            public Vector3 Center => new Vector3((xMin + xMax) * 0.5f, 0f, (zMin + zMax) * 0.5f);
            public float Width => xMax - xMin;
            public float Depth => zMax - zMin;
            public float Surface => Width * Depth;
        }

        static readonly string[] ZoneNames = { "Start Safe Room", "Street", "Plaza", "Avenue", "Parking", "Alley", "End Safe Room" };

        static readonly Area[] Areas =
        {
            new Area(0, -6f, -14f, 6f, -2f, true),
            new Area(0, -1.5f, -2f, 1.5f, 0f, true),
            new Area(1, -9f, 0f, 9f, 80f),
            new Area(2, -9f, 80f, 45f, 115f),
            new Area(3, 27f, 115f, 45f, 200f),
            new Area(4, -3f, 200f, 45f, 240f),
            new Area(5, -3f, 240f, 9f, 300f),
            new Area(6, 1.5f, 300f, 4.5f, 302f, true),
            new Area(6, -3f, 302f, 9f, 314f, true),
        };

        static readonly Vector3[] Path =
        {
            new Vector3(0f, 0f, -10f),
            new Vector3(0f, 0f, 97.5f),
            new Vector3(36f, 0f, 97.5f),
            new Vector3(36f, 0f, 220f),
            new Vector3(3f, 0f, 220f),
            new Vector3(3f, 0f, 310f),
        };

        struct Obstacle
        {
            public Vector2 position;
            public float radius;
        }

        // ---------------------------------------------------------------- Entry point

        [MenuItem("Tools/Rebuild Game Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            SetupLayers();
            EnsureFolder(MaterialsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(ScenesFolder);

            // Create the scene first: NewScene unloads unreferenced assets, which would invalidate references created before it.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var materials = new Materials();
            var prefabs = new Prefabs(materials);
            BuildScene(materials, prefabs);

            AssetDatabase.SaveAssets();
            Debug.Log("[GameBuilder] Scene built: " + ScenePath);
        }

        static void SetupLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(Layers.Enemy).stringValue = "Enemy";
            layers.GetArrayElementAtIndex(Layers.Player).stringValue = "Player";
            tagManager.ApplyModifiedProperties();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        // ---------------------------------------------------------------- Materials

        sealed class Materials
        {
            public readonly Material ground = Lit("Ground", new Color(0.17f, 0.17f, 0.19f), 0.15f);
            public readonly Material groundSafe = Lit("GroundSafe", new Color(0.26f, 0.3f, 0.36f), 0.3f);
            public readonly Material wall = Lit("Wall", new Color(0.4f, 0.37f, 0.35f));
            public readonly Material wallSafe = Lit("WallSafe", new Color(0.55f, 0.6f, 0.62f));
            public readonly Material concrete = Lit("Concrete", new Color(0.55f, 0.55f, 0.52f));
            public readonly Material building = Lit("Building", new Color(0.3f, 0.27f, 0.26f));
            public readonly Material crate = Lit("Crate", new Color(0.45f, 0.32f, 0.18f));
            public readonly Material dumpster = Lit("Dumpster", new Color(0.13f, 0.3f, 0.17f), 0.35f);
            public readonly Material glass = Lit("CarGlass", new Color(0.08f, 0.1f, 0.12f), 0.9f);
            public readonly Material[] cars =
            {
                Lit("CarRed", new Color(0.5f, 0.1f, 0.08f), 0.6f),
                Lit("CarBlue", new Color(0.12f, 0.22f, 0.45f), 0.6f),
                Lit("CarGrey", new Color(0.35f, 0.35f, 0.37f), 0.6f),
                Lit("CarWhite", new Color(0.75f, 0.75f, 0.72f), 0.6f),
            };
            public readonly Material lampPost = Lit("LampPost", new Color(0.12f, 0.12f, 0.13f), 0.5f);
            public readonly Material lampBulb = Lit("LampBulb", new Color(1f, 0.85f, 0.6f), 0.5f, new Color(1f, 0.75f, 0.45f) * 3f);

            public readonly Material zombieClothes = Lit("ZombieClothes", new Color(0.2f, 0.2f, 0.24f));
            public readonly Material zombieEyes = Lit("ZombieEyes", Color.red, 0.5f, new Color(1f, 0.1f, 0.05f) * 4f);
            public readonly Material zombieCommon = Lit("ZombieCommon", new Color(0.36f, 0.46f, 0.3f));
            public readonly Material zombieRunner = Lit("ZombieRunner", new Color(0.65f, 0.58f, 0.25f));
            public readonly Material zombieTank = Lit("ZombieTank", new Color(0.48f, 0.2f, 0.17f));
            public readonly Material zombieBloater = Lit("ZombieBloater", new Color(0.5f, 0.32f, 0.55f), 0.6f);

            public readonly Material pickupHealth = Lit("PickupHealth", new Color(0.92f, 0.92f, 0.9f));
            public readonly Material pickupCross = Lit("PickupCross", new Color(0.9f, 0.1f, 0.1f), 0.5f, new Color(1f, 0.1f, 0.1f) * 1.5f);
            public readonly Material pickupAmmo = Lit("PickupAmmo", new Color(0.35f, 0.38f, 0.2f));
            public readonly Material pickupBrass = Lit("PickupBrass", new Color(0.8f, 0.62f, 0.25f), 0.8f);
            public readonly Material pickupGrenade = Lit("PickupGrenade", new Color(0.2f, 0.28f, 0.15f), 0.4f);
            public readonly Material gunWood = Lit("GunWood", new Color(0.42f, 0.26f, 0.13f));
            public readonly Material gunMetal = Lit("GunMetal", new Color(0.14f, 0.15f, 0.16f), 0.7f);
            public readonly Material viewModel = Lit("ViewModel", new Color(0.25f, 0.25f, 0.27f), 0.5f);

            public readonly Material tracer = Unlit("FxTracer", new Color(1f, 0.9f, 0.55f));
            public readonly Material impact = Unlit("FxImpact", new Color(0.8f, 0.75f, 0.6f));
            public readonly Material blood = Unlit("FxBlood", new Color(0.45f, 0.02f, 0.02f));
            public readonly Material explosion = Unlit("FxExplosion", new Color(1f, 0.55f, 0.2f));

            static Material Lit(string name, Color color, float smoothness = 0.2f, Color? emission = null)
            {
                Material material = GetOrCreate(name, "Universal Render Pipeline/Lit");
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                if (emission.HasValue)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission.Value);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                EditorUtility.SetDirty(material);
                return material;
            }

            static Material Unlit(string name, Color color)
            {
                Material material = GetOrCreate(name, "Universal Render Pipeline/Unlit");
                material.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(material);
                return material;
            }

            static Material GetOrCreate(string name, string shaderName)
            {
                string path = $"{MaterialsFolder}/{name}.mat";
                Shader shader = Shader.Find(shaderName);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    material.shader = shader;
                }
                return material;
            }
        }

        // ---------------------------------------------------------------- Prefabs

        sealed class Prefabs
        {
            public readonly Dictionary<EnemyType, Zombie> zombies = new Dictionary<EnemyType, Zombie>();
            public readonly Dictionary<PickupType, Pickup> pickups = new Dictionary<PickupType, Pickup>();
            public Grenade grenade;

            // NewScene unloads unreferenced assets: reload prefab references from disk before using them.
            public void Reload()
            {
                foreach (EnemyType type in new List<EnemyType>(zombies.Keys)) zombies[type] = Load<Zombie>("Zombie_" + type);
                foreach (PickupType type in new List<PickupType>(pickups.Keys)) pickups[type] = Load<Pickup>("Pickup_" + type);
                grenade = Load<Grenade>("Grenade");
            }

            static T Load<T>(string name) where T : Component
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/{name}.prefab").GetComponent<T>();
            }

            public Prefabs(Materials m)
            {
                zombies[EnemyType.Common] = CreateZombie("Zombie_Common", EnemyType.Common, m.zombieCommon, m, 1f, false, z =>
                {
                    z.maxHealth = 50f; z.wanderSpeed = 1.1f; z.chaseSpeed = 4.2f; z.attackDamage = 7f;
                    z.attackRange = 1.6f; z.attackInterval = 1.1f; z.sightRadius = 18f; z.proximityRadius = 5f;
                });
                zombies[EnemyType.Runner] = CreateZombie("Zombie_Runner", EnemyType.Runner, m.zombieRunner, m, 0.92f, false, z =>
                {
                    z.maxHealth = 30f; z.wanderSpeed = 1.6f; z.chaseSpeed = 6.8f; z.attackDamage = 5f;
                    z.attackRange = 1.5f; z.attackInterval = 0.8f; z.sightRadius = 22f; z.proximityRadius = 7f;
                });
                zombies[EnemyType.Tank] = CreateZombie("Zombie_Tank", EnemyType.Tank, m.zombieTank, m, 1.65f, false, z =>
                {
                    z.maxHealth = 900f; z.wanderSpeed = 1f; z.chaseSpeed = 5.2f; z.attackDamage = 28f;
                    z.attackRange = 2.4f; z.attackInterval = 1.6f; z.sightRadius = 25f; z.proximityRadius = 8f;
                });
                zombies[EnemyType.Bloater] = CreateZombie("Zombie_Bloater", EnemyType.Bloater, m.zombieBloater, m, 1.05f, true, z =>
                {
                    z.maxHealth = 40f; z.wanderSpeed = 0.9f; z.chaseSpeed = 2.8f; z.attackDamage = 0f;
                    z.attackRange = 1.8f; z.attackInterval = 0.6f; z.sightRadius = 16f; z.proximityRadius = 5f;
                    z.explodes = true; z.explosionRadius = 4.5f; z.explosionDamage = 35f;
                });

                pickups[PickupType.Health] = CreatePickup("Pickup_Health", PickupType.Health, 50f, new Color(1f, 0.3f, 0.3f), v =>
                {
                    Part(v, PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.3f, 0.4f), m.pickupHealth);
                    Part(v, PrimitiveType.Cube, new Vector3(0f, 0.16f, 0f), new Vector3(0.3f, 0.03f, 0.09f), m.pickupCross);
                    Part(v, PrimitiveType.Cube, new Vector3(0f, 0.16f, 0f), new Vector3(0.09f, 0.03f, 0.3f), m.pickupCross);
                });
                pickups[PickupType.Ammo] = CreatePickup("Pickup_Ammo", PickupType.Ammo, 0.35f, new Color(1f, 0.85f, 0.4f), v =>
                {
                    Part(v, PrimitiveType.Cube, Vector3.zero, new Vector3(0.45f, 0.26f, 0.3f), m.pickupAmmo);
                    for (int i = -1; i <= 1; i++)
                        Part(v, PrimitiveType.Cube, new Vector3(i * 0.1f, 0.19f, 0f), new Vector3(0.05f, 0.12f, 0.05f), m.pickupBrass);
                });
                pickups[PickupType.Grenade] = CreatePickup("Pickup_Grenade", PickupType.Grenade, 1f, new Color(0.6f, 1f, 0.4f), v =>
                {
                    Part(v, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.25f, m.pickupGrenade);
                    Part(v, PrimitiveType.Cube, new Vector3(0f, 0.14f, 0f), new Vector3(0.06f, 0.08f, 0.06f), m.gunMetal);
                });
                pickups[PickupType.Shotgun] = CreatePickup("Pickup_Shotgun", PickupType.Shotgun, 1f, new Color(1f, 0.7f, 0.4f), v =>
                {
                    Part(v, PrimitiveType.Cube, new Vector3(0f, 0f, 0.15f), new Vector3(0.1f, 0.12f, 0.75f), m.gunMetal);
                    Part(v, PrimitiveType.Cube, new Vector3(0f, -0.03f, -0.3f), new Vector3(0.09f, 0.16f, 0.32f), m.gunWood);
                });
                pickups[PickupType.Rifle] = CreatePickup("Pickup_Rifle", PickupType.Rifle, 1f, new Color(0.6f, 0.8f, 1f), v =>
                {
                    Part(v, PrimitiveType.Cube, new Vector3(0f, 0f, 0.1f), new Vector3(0.09f, 0.14f, 0.8f), m.gunMetal);
                    Part(v, PrimitiveType.Cube, new Vector3(0f, -0.13f, 0.12f), new Vector3(0.06f, 0.2f, 0.09f), m.gunMetal);
                    Part(v, PrimitiveType.Cube, new Vector3(0f, -0.02f, -0.35f), new Vector3(0.08f, 0.15f, 0.25f), m.gunMetal);
                });

                grenade = CreateGrenade(m);
            }

            static Zombie CreateZombie(string name, EnemyType type, Material skin, Materials m, float scale, bool bloated, System.Action<Zombie> configure)
            {
                var root = new GameObject(name);
                root.transform.localScale = Vector3.one * scale;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.78f, 0f);
                capsule.height = 1.56f;
                capsule.radius = bloated ? 0.5f : 0.33f;

                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = bloated ? 0.5f : 0.4f;
                agent.height = 1.9f;
                agent.angularSpeed = 540f;
                agent.acceleration = 24f;
                agent.autoBraking = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;

                Transform body = new GameObject("Body").transform;
                body.SetParent(root.transform, false);
                Part(body, PrimitiveType.Cube, new Vector3(-0.14f, 0.4f, 0f), new Vector3(0.18f, 0.8f, 0.2f), m.zombieClothes);
                Part(body, PrimitiveType.Cube, new Vector3(0.14f, 0.4f, 0f), new Vector3(0.18f, 0.8f, 0.2f), m.zombieClothes);
                if (bloated)
                    Part(body, PrimitiveType.Sphere, new Vector3(0f, 1.15f, 0f), new Vector3(0.95f, 0.9f, 0.85f), skin);
                else
                    Part(body, PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.52f, 0.68f, 0.3f), m.zombieClothes);
                float armX = bloated ? 0.5f : 0.33f;
                Part(body, PrimitiveType.Cube, new Vector3(-armX, 1.35f, 0.28f), new Vector3(0.13f, 0.13f, 0.62f), skin);
                Part(body, PrimitiveType.Cube, new Vector3(armX, 1.35f, 0.28f), new Vector3(0.13f, 0.13f, 0.62f), skin);

                GameObject head = Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0.02f), Vector3.one * 0.38f, skin, true);
                head.name = "Head";
                head.AddComponent<Hitbox>();
                Part(head.transform, PrimitiveType.Cube, new Vector3(-0.22f, 0.1f, 0.42f), Vector3.one * 0.16f, m.zombieEyes);
                Part(head.transform, PrimitiveType.Cube, new Vector3(0.22f, 0.1f, 0.42f), Vector3.one * 0.16f, m.zombieEyes);

                var zombie = root.AddComponent<Zombie>();
                zombie.type = type;
                configure(zombie);
                agent.stoppingDistance = zombie.attackRange * 0.7f;

                SetLayerRecursively(root, Layers.Enemy);
                return SavePrefab(root, name).GetComponent<Zombie>();
            }

            static Pickup CreatePickup(string name, PickupType type, float amount, Color glow, System.Action<Transform> buildVisual)
            {
                var root = new GameObject(name);
                Transform visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                visual.localPosition = new Vector3(0f, 0.45f, 0f);
                buildVisual(visual);

                var lightGo = new GameObject("Glow");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = glow;
                light.range = 3f;
                light.intensity = 1.5f;
                light.shadows = LightShadows.None;

                var pickup = root.AddComponent<Pickup>();
                pickup.type = type;
                pickup.amount = amount;
                pickup.visual = visual;
                return SavePrefab(root, name).GetComponent<Pickup>();
            }

            static Grenade CreateGrenade(Materials m)
            {
                GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                root.name = "Grenade";
                root.transform.localScale = Vector3.one * 0.2f;
                root.GetComponent<Renderer>().sharedMaterial = m.pickupGrenade;
                root.layer = Layers.IgnoreRaycast;
                var body = root.AddComponent<Rigidbody>();
                body.mass = 0.5f;
                body.linearDamping = 0.2f;
                body.angularDamping = 1f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                root.AddComponent<Grenade>();
                return SavePrefab(root, "Grenade").GetComponent<Grenade>();
            }

            static GameObject SavePrefab(GameObject root, string name)
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsFolder}/{name}.prefab");
                Object.DestroyImmediate(root);
                return prefab;
            }
        }

        // ---------------------------------------------------------------- Scene

        static void BuildScene(Materials m, Prefabs prefabs)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.32f, 0.38f);
            RenderSettings.fog = false;
            RenderSettings.fogColor = new Color(0.12f, 0.13f, 0.16f);

            var sunGo = new GameObject("Sun");
            sunGo.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.88f, 0.75f);
            sun.intensity = 0.9f;
            sun.shadows = LightShadows.Soft;

            var random = new System.Random(1234);
            var levelGo = new GameObject("Level");
            Level level = BuildLevel(levelGo, m, random, out List<Obstacle> obstacles);

            NavMeshSurface surface = levelGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = Layers.EnvironmentMask;
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);

            BuildSpawnPoints(levelGo.transform, random, obstacles);
            prefabs.Reload();

            PlayerController player = BuildPlayer(m, prefabs);
            BuildSafeRoomLoadout(prefabs);
            BuildGameSystems(m, prefabs, level, player, sun);

            Scene_Save();
        }

        static void Scene_Save()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static Level BuildLevel(GameObject levelGo, Materials m, System.Random random, out List<Obstacle> obstacles)
        {
            Transform root = levelGo.transform;
            Transform groundRoot = Group(root, "Ground");
            Transform wallRoot = Group(root, "Walls");
            Transform obstacleRoot = Group(root, "Obstacles");
            Transform lightRoot = Group(root, "Lights");

            foreach (Area area in Areas)
            {
                Static(Box(groundRoot, "Ground", area.Center + Vector3.down * 0.25f, new Vector3(area.Width, 0.5f, area.Depth), area.safe ? m.groundSafe : m.ground));
                BuildWalls(wallRoot, area, area.safe ? m.wallSafe : m.wall);
                if (area.safe)
                {
                    Static(Box(wallRoot, "Roof", area.Center + Vector3.up * (SafeWallHeight + 0.15f), new Vector3(area.Width + WallThickness * 2f, 0.3f, area.Depth + WallThickness * 2f), m.wallSafe));
                    if (area.Surface > 20f) AddPointLight(lightRoot, area.Center + Vector3.up * 2.6f, new Color(0.8f, 0.9f, 1f), 12f, 1.2f);
                }
            }

            obstacles = new List<Obstacle>();
            foreach (Area area in Areas)
            {
                if (!area.safe) BuildObstacles(obstacleRoot, area, m, random, obstacles);
            }

            BuildLamps(lightRoot, m);

            var level = levelGo.AddComponent<Level>();
            level.pathPoints = Path;
            level.zoneNames = ZoneNames;
            var zoneAreas = new Level.ZoneArea[Areas.Length];
            for (int i = 0; i < Areas.Length; i++)
            {
                Area a = Areas[i];
                zoneAreas[i] = new Level.ZoneArea
                {
                    zone = a.zone,
                    bounds = new Bounds(a.Center + Vector3.up * 2f, new Vector3(a.Width, 4f, a.Depth)),
                };
            }
            level.areas = zoneAreas;
            return level;
        }

        static void BuildWalls(Transform parent, Area a, Material material)
        {
            float height = a.safe ? SafeWallHeight : WallHeight;

            // Bottom / top edges run along X, left / right edges run along Z.
            BuildEdge(parent, material, height, a.xMin, a.xMax, a.zMin, true, -1f, Openings(a, true, a.zMin, false));
            BuildEdge(parent, material, height, a.xMin, a.xMax, a.zMax, true, 1f, Openings(a, true, a.zMax, true));
            BuildEdge(parent, material, height, a.zMin, a.zMax, a.xMin, false, -1f, Openings(a, false, a.xMin, false));
            BuildEdge(parent, material, height, a.zMin, a.zMax, a.xMax, false, 1f, Openings(a, false, a.xMax, true));
        }

        static List<Vector2> Openings(Area a, bool alongX, float line, bool maxSide)
        {
            var openings = new List<Vector2>();
            foreach (Area b in Areas)
            {
                if (b.xMin == a.xMin && b.zMin == a.zMin && b.xMax == a.xMax && b.zMax == a.zMax) continue;
                float other = alongX ? (maxSide ? b.zMin : b.zMax) : (maxSide ? b.xMin : b.xMax);
                if (!Mathf.Approximately(other, line)) continue;

                float from = alongX ? Mathf.Max(a.xMin, b.xMin) : Mathf.Max(a.zMin, b.zMin);
                float to = alongX ? Mathf.Min(a.xMax, b.xMax) : Mathf.Min(a.zMax, b.zMax);
                if (to - from > 0.01f) openings.Add(new Vector2(from, to));
            }
            openings.Sort((p, q) => p.x.CompareTo(q.x));
            return openings;
        }

        static void BuildEdge(Transform parent, Material material, float height, float from, float to, float line, bool alongX, float outward, List<Vector2> openings)
        {
            var segments = new List<Vector2>();
            float cursor = from;
            foreach (Vector2 opening in openings)
            {
                if (opening.x > cursor) segments.Add(new Vector2(cursor, opening.x));
                cursor = Mathf.Max(cursor, opening.y);
            }
            if (to > cursor) segments.Add(new Vector2(cursor, to));

            foreach (Vector2 segment in segments)
            {
                float length = segment.y - segment.x + WallThickness * 2f;
                float middle = (segment.x + segment.y) * 0.5f;
                float offset = line + outward * WallThickness * 0.5f;
                Vector3 center = alongX ? new Vector3(middle, height * 0.5f, offset) : new Vector3(offset, height * 0.5f, middle);
                Vector3 size = alongX ? new Vector3(length, height, WallThickness) : new Vector3(WallThickness, height, length);
                Static(Box(parent, "Wall", center, size, material));
            }
        }

        static void BuildObstacles(Transform parent, Area area, Materials m, System.Random random, List<Obstacle> obstacles)
        {
            int target = Mathf.RoundToInt(area.Surface / 110f);
            bool large = area.Surface > 1500f;
            int placed = 0;

            for (int attempt = 0; attempt < target * 25 && placed < target; attempt++)
            {
                int kind = random.Next(large ? 6 : 5);
                float radius = kind switch { 0 => 2.4f, 1 => 2.4f, 2 => 1.1f, 3 => 1.7f, 4 => 1.5f, _ => 3.8f };
                const float inset = 1.5f;
                float x = Range(random, area.xMin + inset + radius * 0.5f, area.xMax - inset - radius * 0.5f);
                float z = Range(random, area.zMin + inset + radius * 0.5f, area.zMax - inset - radius * 0.5f);
                var position = new Vector2(x, z);

                if (DistanceToPath(position) < LaneHalfWidth + radius) continue;
                if (Overlaps(obstacles, position, radius + 1.4f)) continue;

                float angle = (random.Next(2) * 90f) + Range(random, -18f, 18f);
                var go = new GameObject("Obstacle");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, angle, 0f));
                Transform t = go.transform;

                switch (kind)
                {
                    case 0:
                    case 1:
                        go.name = "Car";
                        Material paint = m.cars[random.Next(m.cars.Length)];
                        Box(t, "Body", new Vector3(0f, 0.55f, 0f), new Vector3(2f, 0.8f, 4.3f), paint);
                        Box(t, "Cabin", new Vector3(0f, 1.25f, -0.2f), new Vector3(1.8f, 0.6f, 2.2f), m.glass);
                        break;
                    case 2:
                        go.name = "Crates";
                        int count = 1 + random.Next(3);
                        for (int i = 0; i < count; i++)
                        {
                            Vector3 offset = i == 2 ? new Vector3(0.3f, 1.8f, 0f) : new Vector3(i * 1.25f - 0.6f, 0.6f, 0f);
                            Box(t, "Crate", offset, Vector3.one * 1.2f, m.crate, Quaternion.Euler(0f, Range(random, -10f, 10f), 0f));
                        }
                        break;
                    case 3:
                        go.name = "Barrier";
                        Box(t, "Barrier", new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 1f, 0.6f), m.concrete);
                        break;
                    case 4:
                        go.name = "Dumpster";
                        Box(t, "Dumpster", new Vector3(0f, 0.75f, 0f), new Vector3(2.4f, 1.5f, 1.4f), m.dumpster);
                        break;
                    default:
                        go.name = "Block";
                        Box(t, "Block", new Vector3(0f, 2f, 0f), new Vector3(5f, 4f, 5f), m.building);
                        break;
                }

                Static(go);
                obstacles.Add(new Obstacle { position = position, radius = radius });
                placed++;
            }
        }

        static void BuildLamps(Transform parent, Materials m)
        {
            float length = 0f;
            for (int i = 1; i < Path.Length; i++) length += Vector3.Distance(Path[i - 1], Path[i]);

            int side = 1;
            for (float d = 12f; d < length - 12f; d += 22f)
            {
                Vector3 point = PointOnPath(d, out Vector3 direction);
                Vector3 normal = Vector3.Cross(Vector3.up, direction).normalized;
                Vector3 position = point + normal * side * 4.5f;
                if (!InsideCombatArea(position, 1f))
                {
                    position = point - normal * side * 4.5f;
                    if (!InsideCombatArea(position, 1f)) continue;
                }
                side = -side;

                var lamp = new GameObject("Lamp");
                lamp.transform.SetParent(parent, false);
                lamp.transform.position = position;
                Box(lamp.transform, "Post", new Vector3(0f, 2.25f, 0f), new Vector3(0.15f, 4.5f, 0.15f), m.lampPost);
                Box(lamp.transform, "Bulb", new Vector3(0f, 4.5f, 0f), new Vector3(0.5f, 0.15f, 0.5f), m.lampBulb, null, false);
                Static(lamp);
                AddPointLight(lamp.transform, position + Vector3.up * 4.2f, new Color(1f, 0.78f, 0.5f), 16f, 3f);
            }
        }

        static void AddPointLight(Transform parent, Vector3 position, Color color, float range, float intensity)
        {
            var go = new GameObject("Point Light");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        // ---------------------------------------------------------------- Spawn points

        static void BuildSpawnPoints(Transform levelRoot, System.Random random, List<Obstacle> obstacles)
        {
            Transform enemyRoot = Group(levelRoot, "Enemy Spawn Points");
            Transform pickupRoot = Group(levelRoot, "Pickup Spawn Points");
            var used = new List<Vector2>();

            foreach (Area area in Areas)
            {
                if (area.safe) continue;

                // Enemy points: along the walls, away from the main lane.
                const float inset = 1.5f;
                const float step = 11f;
                var perimeter = new List<Vector2>();
                for (float x = area.xMin + inset; x <= area.xMax - inset; x += step)
                {
                    perimeter.Add(new Vector2(x, area.zMin + inset));
                    perimeter.Add(new Vector2(x, area.zMax - inset));
                }
                for (float z = area.zMin + inset + step; z <= area.zMax - inset - step; z += step)
                {
                    perimeter.Add(new Vector2(area.xMin + inset, z));
                    perimeter.Add(new Vector2(area.xMax - inset, z));
                }
                for (int i = 0; i < 3; i++)
                    perimeter.Add(new Vector2(Range(random, area.xMin + 3f, area.xMax - 3f), Range(random, area.zMin + 3f, area.zMax - 3f)));

                float laneClearance = Mathf.Min(5f, Mathf.Min(area.Width, area.Depth) * 0.5f - 2f);
                foreach (Vector2 candidate in perimeter)
                {
                    if (DistanceToPath(candidate) < laneClearance) continue;
                    if (Overlaps(obstacles, candidate, 1f)) continue;
                    if (NearAny(used, candidate, 4f)) continue;
                    if (!OnNavMesh(candidate, out Vector3 position)) continue;
                    CreateSpawnPoint(enemyRoot, SpawnPointKind.Enemy, area.zone, position);
                    used.Add(candidate);
                }

                // Pickup points: scattered in the area.
                int pickupTarget = Mathf.Clamp(Mathf.RoundToInt(area.Surface / 250f), 3, 8);
                int placed = 0;
                for (int attempt = 0; attempt < 80 && placed < pickupTarget; attempt++)
                {
                    var candidate = new Vector2(Range(random, area.xMin + 2f, area.xMax - 2f), Range(random, area.zMin + 2f, area.zMax - 2f));
                    if (Overlaps(obstacles, candidate, 0.8f)) continue;
                    if (NearAny(used, candidate, 6f)) continue;
                    if (!OnNavMesh(candidate, out Vector3 position)) continue;
                    CreateSpawnPoint(pickupRoot, SpawnPointKind.Pickup, area.zone, position);
                    used.Add(candidate);
                    placed++;
                }
            }
        }

        static void CreateSpawnPoint(Transform parent, SpawnPointKind kind, int zone, Vector3 position)
        {
            var go = new GameObject($"{kind} Spawn (zone {zone})");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var point = go.AddComponent<SpawnPoint>();
            point.kind = kind;
            point.zone = zone;
        }

        static bool OnNavMesh(Vector2 position, out Vector3 result)
        {
            if (NavMesh.SamplePosition(new Vector3(position.x, 0f, position.y), out NavMeshHit hit, 0.8f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
            result = Vector3.zero;
            return false;
        }

        // ---------------------------------------------------------------- Player & systems

        static PlayerController BuildPlayer(Materials m, Prefabs prefabs)
        {
            var root = new GameObject("Player");
            root.tag = "Player";
            root.layer = Layers.Player;
            root.transform.position = new Vector3(0f, 0.05f, -11f);

            var controller = root.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.stepOffset = 0.4f;

            var cameraGo = new GameObject("Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(root.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 400f;
            camera.fieldOfView = 75f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            cameraGo.AddComponent<AudioListener>();

            var flashGo = new GameObject("Flashlight");
            flashGo.transform.SetParent(cameraGo.transform, false);
            flashGo.transform.localPosition = new Vector3(0.2f, -0.1f, 0.1f);
            var flashlight = flashGo.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.range = 35f;
            flashlight.spotAngle = 55f;
            flashlight.innerSpotAngle = 30f;
            flashlight.intensity = 8f;
            flashlight.color = new Color(1f, 0.96f, 0.85f);
            flashlight.shadows = LightShadows.Soft;
            flashlight.enabled = false;

            GameObject viewModel = Box(cameraGo.transform, "ViewModel", new Vector3(0.17f, -0.16f, 0.38f), new Vector3(0.045f, 0.08f, 0.2f), m.viewModel, null, false);
            viewModel.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(viewModel.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            var muzzleLight = muzzle.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.range = 7f;
            muzzleLight.intensity = 5f;
            muzzleLight.color = new Color(1f, 0.75f, 0.4f);
            muzzleLight.shadows = LightShadows.None;
            muzzleLight.enabled = false;

            root.AddComponent<PlayerHealth>();
            var weapons = root.AddComponent<PlayerWeapons>();
            weapons.grenadePrefab = prefabs.grenade;
            weapons.viewModel = viewModel.transform;
            weapons.viewModelRenderer = viewModel.GetComponent<Renderer>();
            weapons.muzzle = muzzle.transform;
            weapons.muzzleLight = muzzleLight;

            var player = root.AddComponent<PlayerController>();
            player.playerCamera = camera;
            player.cameraPivot = cameraGo.transform;
            player.flashlight = flashlight;
            return player;
        }

        static void BuildSafeRoomLoadout(Prefabs prefabs)
        {
            var root = new GameObject("Safe Room Loadout");
            Place(prefabs.pickups[PickupType.Shotgun], new Vector3(-4f, 0f, -12.5f), root.transform);
            Place(prefabs.pickups[PickupType.Rifle], new Vector3(4f, 0f, -12.5f), root.transform);
            Place(prefabs.pickups[PickupType.Ammo], new Vector3(-4f, 0f, -4.5f), root.transform);
            Place(prefabs.pickups[PickupType.Grenade], new Vector3(4f, 0f, -4.5f), root.transform);
        }

        static void Place(Component prefab, Vector3 position, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            instance.transform.position = position;
        }

        static void BuildGameSystems(Materials m, Prefabs prefabs, Level level, PlayerController player, Light sun)
        {
            var game = new GameObject("Game");

            var spawner = game.AddComponent<Spawner>();
            var enemyEntries = new List<Spawner.EnemyEntry>();
            foreach (KeyValuePair<EnemyType, Zombie> pair in prefabs.zombies)
                enemyEntries.Add(new Spawner.EnemyEntry { type = pair.Key, prefab = pair.Value });
            spawner.enemies = enemyEntries.ToArray();
            var pickupEntries = new List<Spawner.PickupEntry>();
            foreach (KeyValuePair<PickupType, Pickup> pair in prefabs.pickups)
                pickupEntries.Add(new Spawner.PickupEntry { type = pair.Key, prefab = pair.Value });
            spawner.pickups = pickupEntries.ToArray();

            var worldEvents = game.AddComponent<WorldEvents>();
            worldEvents.sun = sun;

            var fx = game.AddComponent<Fx>();
            fx.tracerMaterial = m.tracer;
            fx.impactMaterial = m.impact;
            fx.bloodMaterial = m.blood;
            fx.explosionMaterial = m.explosion;

            game.AddComponent<Hud>();

            var gameManager = game.AddComponent<GameManager>();
            gameManager.player = player;
            gameManager.level = level;
            gameManager.spawner = spawner;
            gameManager.worldEvents = worldEvents;

            var directorGo = new GameObject("AI Director");
            var runner = directorGo.AddComponent<DirectorRunner>();
            directorGo.AddComponent<MyDirector>();

            var overlay = game.AddComponent<DebugOverlay>();
            overlay.runner = runner;
        }

        // ---------------------------------------------------------------- Helpers

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, Quaternion? rotation = null, bool collider = true)
        {
            GameObject go = Part(parent, PrimitiveType.Cube, localPosition, size, material, collider);
            go.name = name;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            return go;
        }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Static(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        static bool Overlaps(List<Obstacle> obstacles, Vector2 position, float radius)
        {
            foreach (Obstacle o in obstacles)
                if (Vector2.Distance(o.position, position) < o.radius + radius) return true;
            return false;
        }

        static bool NearAny(List<Vector2> points, Vector2 position, float distance)
        {
            foreach (Vector2 p in points)
                if (Vector2.Distance(p, position) < distance) return true;
            return false;
        }

        static float DistanceToPath(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 1; i < Path.Length; i++)
            {
                var a = new Vector2(Path[i - 1].x, Path[i - 1].z);
                var b = new Vector2(Path[i].x, Path[i].z);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static Vector3 PointOnPath(float distance, out Vector3 direction)
        {
            for (int i = 1; i < Path.Length; i++)
            {
                float length = Vector3.Distance(Path[i - 1], Path[i]);
                direction = (Path[i] - Path[i - 1]).normalized;
                if (distance <= length) return Path[i - 1] + direction * distance;
                distance -= length;
            }
            direction = Vector3.forward;
            return Path[Path.Length - 1];
        }

        static bool InsideCombatArea(Vector3 position, float margin)
        {
            foreach (Area a in Areas)
            {
                if (a.safe) continue;
                if (position.x > a.xMin + margin && position.x < a.xMax - margin && position.z > a.zMin + margin && position.z < a.zMax - margin)
                    return true;
            }
            return false;
        }
    }
}
