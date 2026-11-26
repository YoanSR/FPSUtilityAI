using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieFPS
{

    public class MyDirector : MonoBehaviour, IAIDirector
    {
        [SerializeField] private Image _healthBar;
        [SerializeField] private Image _ammoBar;
        [SerializeField] private Image _enemyNumber;
        
        
        public List<UtilityAction> _actions;
        
        public Dictionary<int, EnemyType> RandomEnemy = new();

        [SerializeField] private float _spawnTime;

        [SerializeField] private AnimationCurve _enemySpeedCurve;

        private bool FirstBoss;

        public Dictionary<string, float> Context = new();

        public bool Test;

        private float _waveTimer;
        private float _waveTime;
        [SerializeField] private AnimationCurve _waveMobsCurve;
        
        private bool _adrenaline;
        private float _adrenalineCooldown = 20;
        
        public void OnGameStart(DirectorContext context, DirectorActions actions)
        {
            //context.Player.HealthRatio
            //context.MaxAliveEnemies = 5;
            // foreach (var SpawnPoint in context.EnemySpawnPoints)
            // {
            //     //actions.SpawnEnemies(EnemyType.Common, SpawnPoint.Position);
            //     
            // }
            // //actions.StartFog(5);
            //
            // RandomEnemy.Add(1, EnemyType.Common);
            // RandomEnemy.Add(2, EnemyType.Runner);
            // RandomEnemy.Add(3, EnemyType.Bloater);
            //
            
            _waveTimer = Random.Range(60, 240);
            
            Context.Add("EnemySpeed", 0);
            Context.Add("EnemyDamage", 0);
            Context.Add("EnemyNumber", 0);
            Context.Add("MaxEnemy", 0);
            Context.Add("LevelProgress", 0);
            Context.Add("Test", 0);
            Context.Add("PlayerHealth", 0);
            Context.Add("Weapons", 0);
            Context.Add("Ammo", 0);
            
            Context.Add("Combo", 0);

        }

        public void Tick(DirectorContext context, DirectorActions actions, float deltaTime)
        {
            Context["EnemySpeed"] = context.EnemySpeedMultiplier;
            Context["EnemyDamage"] = context.EnemyDamageMultiplier;
            Context["EnemyNumber"] = context.Enemies.Count / 10;
            Context["MaxEnemy"] = context.MaxAliveEnemies;
            Context["LevelProgress"] = context.Player.Progress;

            Context["Ammo"] = context.Player.AmmoRatio;

            Context["PlayerHealth"] = context.Player.HealthRatio;

            Context["Weapons"] = context.Player.ownedWeapons.Length/3;

            Context["Combo"] = context.Stats.TimeSinceKill - context.Stats.TimeSinceDamageDealt ;
            
            //Debug.Log(context.Stats.TimeSinceKill - context.Stats.TimeSinceDamageDealt);
            
            Context["TimeSinceLastAdrenaline"] = _adrenalineCooldown/20;
            
            Context["Test"] = Test ? 1 : 0;
            
            
            _healthBar.fillAmount = Context["PlayerHealth"];
            _ammoBar.fillAmount = Context["Ammo"];
            _enemyNumber.fillAmount = Context["EnemyNumber"];
            // var enemyNumber = context.CountEnemies(EnemyType.Common);
            //
            //
            //
            // _spawnTime += deltaTime;
            // if (_spawnTime > 0f)
            // {
            //     var EnemyGender = Random.Range(1,4);
            //     SpawnPointInfo enemySpawnPoint = context.EnemySpawnPoints[Random.Range(0, context.EnemySpawnPoints.Count)];
            //     if (!context.IsVisibleToPlayer(enemySpawnPoint.Position) && Vector3.Distance(enemySpawnPoint.Position, context.Player.Position) < 50 )
            //         
            //         actions.SpawnEnemy(RandomEnemy[EnemyGender], enemySpawnPoint);
            //     
            //     _spawnTime = 0;
            // }
            //
            // for (int i = 0; i < context.Enemies.Count; i++)
            // {
            //     if (enemyNumber >= 20 && !context.IsVisibleToPlayer(context.Player.Position) &&
            //         Vector3.Distance(context.Enemies[i].Position, context.Player.Position) >= 20)
            //         actions.DespawnEnemy(context.Enemies[i]);
            // }
            //
            // for (int i = 0; i < context.Enemies.Count; i++)
            // {
            //         actions.SetEnemySpeedMultiplier(_enemySpeedCurve.Evaluate(Vector3.Distance(context.Enemies[i].Position, context.Player.Position)/20));
            //         Debug.Log(Vector3.Distance(context.Enemies[i].Position, context.Player.Position)/20);
            // }
            //
            // Debug.Log(context.Player.Progress);
            //
            // if (context.Player.Progress > 0.25 && !FirstBoss)
            // {
            //     for (int i = 0; i < context.EnemySpawnPoints.Count; i++)
            //     {
            //             actions.StartFog(5);
            //             actions.SpawnEnemy(EnemyType.Tank, context.EnemySpawnPoints[i]);
            //     }
            //     
            //     FirstBoss = true;
            // }
            
            _spawnTime += deltaTime;
            _waveTimer += deltaTime;
            
            if (_adrenaline  == true)
                _adrenalineCooldown -= deltaTime;

            var bestAction = ChooseAction(context,  actions,  deltaTime);
            Debug.Log(bestAction.Name);
            
            if (bestAction.Name == "SpawnEnemy" && _spawnTime > 0.5f)
            {
                SpawnPointInfo enemySpawnPoint =
                    context.EnemySpawnPoints[Random.Range(0, context.EnemySpawnPoints.Count)];

                if (!context.IsVisibleToPlayer(enemySpawnPoint.Position) &&
                    context.Player.CurrentZone == enemySpawnPoint.Zone)
                    actions.SpawnEnemy(EnemyType.Common, enemySpawnPoint);
                
                _spawnTime = 0;
            }

            if (bestAction.Name == "SpawnAmmo" && _spawnTime > 0.5f)
            {
                var pickupSpawnPoint = context.PickupSpawnPoints[Random.Range(0, context.PickupSpawnPoints.Count)];
                
                if (!context.IsVisibleToPlayer(context.Player.Position) && pickupSpawnPoint.Zone == context.Player.CurrentZone)
                    actions.SpawnPickup(PickupType.Ammo, pickupSpawnPoint);
                
                _spawnTime = 0;
            }
            if (bestAction.Name == "SpawnHealth" && _spawnTime > 0.5f)
            {
                var pickupSpawnPoint = context.PickupSpawnPoints[Random.Range(0, context.PickupSpawnPoints.Count)];
                
                if (!context.IsVisibleToPlayer(context.Player.Position) && pickupSpawnPoint.Zone == context.Player.CurrentZone)
                    actions.SpawnPickup(PickupType.Health, pickupSpawnPoint);
                
                _spawnTime = 0;
            }
            if (bestAction.Name == "SpawnGrenade" && _spawnTime > 0.5f)
            {
                var pickupSpawnPoint = context.PickupSpawnPoints[Random.Range(0, context.PickupSpawnPoints.Count)];
                
                if (!context.IsVisibleToPlayer(context.Player.Position) && pickupSpawnPoint.Zone == context.Player.CurrentZone)
                    actions.SpawnPickup(PickupType.Grenade, pickupSpawnPoint);
                
                _spawnTime = 0;
            }


            if (bestAction.Name == "Adrenaline")
            {
                actions.GiveAdrenaline(5);
                _adrenaline = true;
            }
            
            if (_waveTimer >= _waveTime)
            {
                for (int i = 0; i < _waveMobsCurve.Evaluate(Context["PlayerHealth"]); i++)
                {
                    // if (!context.IsVisibleToPlayer(enemySpawnPoint.Position) &&
                    //     context.Player.CurrentZone == enemySpawnPoint.Zone)
                    //     actions.SpawnEnemy(EnemyType.Common, enemySpawnPoint);
                }
                _waveTimer = 0;
                _waveTime = Random.Range(60, 240);
            }


        }

        private UtilityAction ChooseAction(DirectorContext context, DirectorActions actions, float deltaTime)
        {
            UtilityAction bestAction = null;
            float bestScore = -1f;
            foreach (UtilityAction utilityAction in _actions)
            {
                float score = utilityAction.Evaluate(Context);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAction = utilityAction;

                    //Debug.Log(bestAction.Name);
                }
            }

            return bestAction;
        }
    }
}
