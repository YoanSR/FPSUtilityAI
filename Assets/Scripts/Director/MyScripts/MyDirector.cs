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
        [SerializeField] private Image _waveTimeBar;
        
        
        public List<UtilityAction> _actions;
        
        public Dictionary<int, EnemyType> RandomEnemy = new();
        
        public Dictionary<int, PickupType> RandomWeapon = new();

        [SerializeField] private float _enemySpawnTime;
        
        [SerializeField] private float _pickupSpawnTime;

        [SerializeField] private AnimationCurve _enemySpeedCurve;
        [SerializeField] private AnimationCurve _enemyDamageCurve;

        [SerializeField] private AnimationCurve _enemyNumberInHorde;
        
        private bool FirstBoss;

        public Dictionary<string, float> Context = new();

        public bool Test;

        private float _waveTimer;
        [SerializeField] private float _waveTime;
        [SerializeField] private AnimationCurve _waveMobsCurve;
        
        private bool _adrenaline;
        private float _adrenalineCooldown = 20;
        [SerializeField] private float _adrenalineTimer;

        private bool _firstWeapon;
        private bool _secondWeapon;
        
        public void OnGameStart(DirectorContext context, DirectorActions actions)
        {
            _waveTime = Random.Range(60, 240);
            _waveTime = 10;
            
            RandomWeapon.Add(1, PickupType.Shotgun);
            RandomWeapon.Add(2, PickupType.Rifle);
            
            RandomEnemy.Add(1, EnemyType.Common);
            RandomEnemy.Add(2, EnemyType.Runner);
            RandomEnemy.Add(3, EnemyType.Bloater);
            
            Context.Add("EnemySpeed", 0);
            Context.Add("EnemyDamage", 0);
            Context.Add("EnemyNumber", 0);
            Context.Add("MaxEnemy", 0);
            Context.Add("LevelProgress", 0);
            Context.Add("Test", 0);
            Context.Add("PlayerHealth", 0);
            Context.Add("Weapons", 0);
            Context.Add("Ammo", 0);
            
            Context.Add("EnemyKilled", 0);
            Context.Add("TimeForEachWave", 0);

            Context.Add("DamageTaken", 0);
        }

        public void Tick(DirectorContext context, DirectorActions actions, float deltaTime)
        {
            Context["EnemySpeed"] = context.EnemySpeedMultiplier;
            Context["EnemyDamage"] = context.EnemyDamageMultiplier;
            Context["EnemyNumber"] = context.Enemies.Count / 10;
            
            Context["LevelProgress"] = context.Player.Progress;
            Debug.Log(context.Player.Progress);
            
            Context["EnemyKilled"] = context.Stats.TotalKills/100;

            Context["Ammo"] = context.Player.AmmoRatio;

            Context["PlayerHealth"] = context.Player.HealthRatio;

            Context["Weapons"] = context.Player.ownedWeapons.Length /3;

            Context["DamageTaken"] = context.Stats.TimeSinceDamageTaken;

            Context["TimeForEachWave"] = _waveTimer / _waveTime;
            
            _healthBar.fillAmount = Context["PlayerHealth"];
            _ammoBar.fillAmount = Context["Ammo"];
            _enemyNumber.fillAmount = Context["EnemyNumber"];
            _waveTimeBar.fillAmount = Context["TimeForEachWave"];
            
            //Debug.Log(Context["TimeForEachWave"]);

            _enemySpawnTime += deltaTime;
            _pickupSpawnTime += deltaTime;
            _waveTimer += deltaTime;
            
            if (_adrenaline  == true)
                _adrenalineCooldown -= deltaTime;
            
            var enemySpawnPoint = context.EnemySpawnPoints[Random.Range(0, context.EnemySpawnPoints.Count)];
            var pickupSpawnPoint = context.PickupSpawnPoints[Random.Range(0, context.PickupSpawnPoints.Count)];

            var bestAction = ChooseAction(context,  actions,  deltaTime);
            Debug.Log(bestAction.Name);

            foreach (var Enemy in context.Enemies)
            {
                actions.SetEnemySpeedMultiplier(_enemySpeedCurve.Evaluate(Vector3.Distance(Enemy.Position, context.Player.Position)/10));
                actions.SetEnemyDamageMultiplier(_enemyDamageCurve.Evaluate(Vector3.Distance(Enemy.Position, context.Player.Position)/10));
            }
  

            switch (bestAction.Name)
            {
                case "SpawnEnemy" :
                    if (!(!context.IsVisibleToPlayer(enemySpawnPoint.Position) && context.Player.CurrentZone == enemySpawnPoint.Zone && _enemySpawnTime > 0.5f)) 
                        return;
                    
                    actions.SpawnEnemy(RandomEnemy[Random.Range(1,4)], enemySpawnPoint);
                    _enemySpawnTime = 0;
                    break;
                
                case "SpawnAmmo" :
                    if (!(!context.IsVisibleToPlayer(context.Player.Position) && pickupSpawnPoint.Zone == context.Player.CurrentZone && _pickupSpawnTime > 2)) 
                        return;
                    
                    actions.SpawnPickup(PickupType.Ammo, pickupSpawnPoint);
                    _pickupSpawnTime = 0;
                    break;
                
                case "SpawnHealth" :
                    if (!(!context.IsVisibleToPlayer(context.Player.Position) && pickupSpawnPoint.Zone == context.Player.CurrentZone && _pickupSpawnTime > 2)) 
                        return;
                    
                    actions.SpawnPickup(PickupType.Health, pickupSpawnPoint);
                    _pickupSpawnTime = 0;
                    break;
                
                case "SpawnGrenade" :
                    if (!(!context.IsVisibleToPlayer(context.Player.Position) && pickupSpawnPoint.Zone == context.Player.CurrentZone && _pickupSpawnTime > 2))
                        return;
                    
                    actions.SpawnPickup(PickupType.Grenade, pickupSpawnPoint);
                    _pickupSpawnTime = 0;
                    break;
                
                case "Horde" :
                    for (int i = 0; i < _enemyNumberInHorde.Evaluate(Context["EnemyKilled"]); i++)
                    {
                        if (!(!context.IsVisibleToPlayer(enemySpawnPoint.Position) && context.Player.CurrentZone == enemySpawnPoint.Zone)) 
                            return;
                        
                        actions.SpawnEnemy(EnemyType.Common, enemySpawnPoint);
                    }
                    
                    if (_waveTime < _waveTimer)
                    {
                        _waveTimer = 0;
                        _waveTime = Random.Range(60, 240);
                    }
                    break;
                
                case "Adrenaline" :
                    if (_adrenalineTimer < _adrenalineCooldown)
                    {
                        actions.GiveAdrenaline(10);
                        _adrenalineTimer = 0;
                    }
                    break;
            }
            //marche pas 
            if ((!_firstWeapon && context.Player.Progress >= 0.25) || (!_secondWeapon && context.Player.Progress >= 0.75))
            {
                var SelectedWeaponNumber = Random.Range(1, 2);
                var SelectedWeapon = RandomWeapon[SelectedWeaponNumber];
                if (!(!context.IsVisibleToPlayer(context.Player.Position) && pickupSpawnPoint.Zone == context.Player.CurrentZone && _pickupSpawnTime > 2 && context.Player.ownedWeapons[SelectedWeaponNumber])) 
                    return;
                actions.ShowMessage("A weapon is located in this area !", 3);
                actions.SpawnPickup(SelectedWeapon, pickupSpawnPoint);
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
                }
            }
            return bestAction;
        }
    }
}