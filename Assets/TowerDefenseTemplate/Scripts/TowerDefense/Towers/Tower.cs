using System;
using ActionGameFramework.Health;
using Core.Utilities;
using TowerDefense.Level;
using TowerDefense.Towers.Placement;
using TowerDefense.UI.HUD;
using UnityEngine;

namespace TowerDefense.Towers
{
    /// <summary>
    /// Represents a tower in the tower defense game. Towers can be upgraded, downgraded, and sold.
    /// </summary>
    public class Tower : Targetable
    {
        // Array of levels that this tower can have
        public TowerLevel[] levels;

        // Name of the tower
        public string towerName;

        // Dimensions of the tower in grid units
        public IntVector2 dimensions;

        // Layer mask to identify enemy layers
        public LayerMask enemyLayerMask;

        // Current level of the tower
        public int currentLevel { get; protected set; }

        // Current tower level object
        public TowerLevel currentTowerLevel { get; protected set; }

        // Property to check if the tower is at its maximum level
        public bool isAtMaxLevel
        {
            get { return currentLevel == levels.Length - 1; }
        }

        // Property to get the tower placement ghost prefab for the current level
        public TowerPlacementGhost towerGhostPrefab
        {
            get { return levels[currentLevel].towerGhostPrefab; }
        }

        // Grid position of the tower
        public IntVector2 gridPosition { get; private set; }

        // Placement area where the tower is placed
        public IPlacementArea placementArea { get; private set; }

        // Cost to purchase the tower
        public int purchaseCost
        {
            get { return levels[0].cost; }
        }

        // Event triggered when the tower is deleted
        public Action towerDeleted;

        // Event triggered when the tower is destroyed
        public Action towerDestroyed;

        /// <summary>
        /// Initializes the tower at a specific placement area and grid position.
        /// </summary>
        /// <param name="targetArea">The placement area where the tower will be placed.</param>
        /// <param name="destination">The grid position where the tower will be placed.</param>
        public virtual void Initialize(IPlacementArea targetArea, IntVector2 destination)
        {
            placementArea = targetArea;
            gridPosition = destination;

            if (targetArea != null)
            {
                transform.position = placementArea.GridToWorld(destination, dimensions);
                transform.rotation = placementArea.transform.rotation;
                targetArea.Occupy(destination, dimensions);
            }

            SetLevel(0);
            if (LevelManager.instanceExists)
            {
                LevelManager.instance.levelStateChanged += OnLevelStateChanged;
            }
        }

        /// <summary>
        /// Gets the cost required to upgrade to the next level.
        /// </summary>
        /// <returns>The cost for the next level, or -1 if at max level.</returns>
        public int GetCostForNextLevel()
        {
            if (isAtMaxLevel)
            {
                return -1;
            }
            return levels[currentLevel + 1].cost;
        }

        /// <summary>
        /// Kills the tower, triggering its destruction.
        /// </summary>
        public void KillTower()
        {
            Kill();
        }

        /// <summary>
        /// Gets the sell value of the tower at its current level.
        /// </summary>
        /// <returns>The sell value of the tower.</returns>
        public int GetSellLevel()
        {
            return GetSellLevel(currentLevel);
        }

        /// <summary>
        /// Gets the sell value of the tower at a specific level.
        /// </summary>
        /// <param name="level">The level to get the sell value for.</param>
        /// <returns>The sell value of the tower at the specified level.</returns>
        public int GetSellLevel(int level)
        {
            if (LevelManager.instance.levelState == LevelState.Building)
            {
                int cost = 0;
                for (int i = 0; i <= level; i++)
                {
                    cost += levels[i].cost;
                }

                return cost;
            }
            return levels[currentLevel].sell;
        }

        /// <summary>
        /// Upgrades the tower to the next level.
        /// </summary>
        /// <returns>True if the upgrade was successful, false if already at max level.</returns>
        public virtual bool UpgradeTower()
        {
            if (isAtMaxLevel)
            {
                return false;
            }
            SetLevel(currentLevel + 1);
            return true;
        }

        /// <summary>
        /// Downgrades the tower to the previous level.
        /// </summary>
        /// <returns>True if the downgrade was successful, false if already at the lowest level.</returns>
        public virtual bool DowngradeTower()
        {
            if (currentLevel == 0)
            {
                return false;
            }
            SetLevel(currentLevel - 1);
            return true;
        }

        /// <summary>
        /// Upgrades the tower to a specific level.
        /// </summary>
        /// <param name="level">The level to upgrade to.</param>
        /// <returns>True if the upgrade was successful, false if the level is invalid or already at max level.</returns>
        public virtual bool UpgradeTowerToLevel(int level)
        {
            if (level < 0 || isAtMaxLevel || level >= levels.Length)
            {
                return false;
            }
            SetLevel(level);
            return true;
        }

        /// <summary>
        /// Sells the tower, removing it from the game.
        /// </summary>
        public void Sell()
        {
            Remove();
        }

        /// <summary>
        /// Removes the tower from the game and clears its occupied space in the placement area.
        /// </summary>
        public override void Remove()
        {
            base.Remove();

            placementArea.Clear(gridPosition, dimensions);
            Destroy(gameObject);
        }

        /// <summary>
        /// Called when the tower is destroyed. Unsubscribes from level state change events.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (LevelManager.instanceExists)
            {
                LevelManager.instance.levelStateChanged -= OnLevelStateChanged;
            }
        }

        /// <summary>
        /// Sets the tower to a specific level.
        /// </summary>
        /// <param name="level">The level to set the tower to.</param>
        protected void SetLevel(int level)
        {
            if (level < 0 || level >= levels.Length)
            {
                return;
            }
            currentLevel = level;
            if (currentTowerLevel != null)
            {
                Destroy(currentTowerLevel.gameObject);
            }

            currentTowerLevel = Instantiate(levels[currentLevel], transform);
            currentTowerLevel.Initialize(this, enemyLayerMask, configuration.alignmentProvider);
            ScaleHealth();

            LevelState levelState = LevelManager.instance.levelState;
            bool initialise = levelState == LevelState.AllEnemiesSpawned || levelState == LevelState.SpawningEnemies;
            currentTowerLevel.SetAffectorState(initialise);
        }

        /// <summary>
        /// Scales the health of the tower based on its current level.
        /// </summary>
        protected virtual void ScaleHealth()
        {
            configuration.SetMaxHealth(currentTowerLevel.maxHealth);

            if (currentLevel == 0)
            {
                configuration.SetHealth(currentTowerLevel.maxHealth);
            }
            else
            {
                int currentHealth = Mathf.FloorToInt(configuration.normalisedHealth * currentTowerLevel.maxHealth);
                configuration.SetHealth(currentHealth);
            }
        }

        /// <summary>
        /// Handles changes in the level state, updating the tower's affector state accordingly.
        /// </summary>
        /// <param name="previous">The previous level state.</param>
        /// <param name="current">The current level state.</param>
        protected virtual void OnLevelStateChanged(LevelState previous, LevelState current)
        {
            bool initialise = current == LevelState.AllEnemiesSpawned || current == LevelState.SpawningEnemies;
            currentTowerLevel.SetAffectorState(initialise);
        }
    }
}