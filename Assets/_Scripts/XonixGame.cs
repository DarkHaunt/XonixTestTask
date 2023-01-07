using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections;
using System;
using Xonix.Entities.Enemies;
using Xonix.Entities.Players;
using Xonix.PlayerInput;
using Xonix.Entities;
using Xonix.Grid;
using Xonix.UI;
using UnityEngine;



namespace Xonix
{
    using static EnemyBehaviour;

    public class XonixGame : MonoBehaviour
    {
        private static XonixGame _instance;

        #region [Constant Data]

        private const float GameEndTime = 90; // One and a half hour of max game time
        private const float EntitiesMoveTimeDelaySeconds = 0.03f;
        private const float TargetSeaFieldCorruptionPercent = 0.50f; // A percent of corrupted sea field, when level will be completed

        private const int IndexOfFirstSeaEnemy = 1; // For the enemy collection
        private const int StartCountOfSeaEnemies = 3;

        private const int CameraLeftSidePixelsPadding = 60;

        // Cached for optimization
        private readonly YieldInstruction MoveTimeYield = new WaitForSeconds(EntitiesMoveTimeDelaySeconds);
        private readonly YieldInstruction OneMinuteYield = new WaitForSeconds(60f);  

        #endregion

        public static event Action OnLevelCompleted;
        public static event Action OnLevelLosen;
        public static event Action OnLevelReloaded;

        [SerializeField] private XonixGrid _grid;
        [SerializeField] private FourDirectionInputTranslator _inputSystem;
        [SerializeField] private GamePrintUI _printUI;
        [SerializeField] private Camera _mainCamera;

        private EntitySpawner _entitySpawner;
        private List<Enemy> _enemies;
        private Player _player;

        private int _score = 0;
        private int _levelNumber = 1;
        private float _minutesForLevelLeft = GameEndTime;



        public static IEnumerable<Enemy> SeaEnemies
        {
            get
            {
                for (int i = IndexOfFirstSeaEnemy; i < _instance._enemies.Count; i++)
                    yield return _instance._enemies[i];
            }
        }



        public static bool TryToGetNodeWithPosition(Vector2 position, out GridNode node)
        {
            return _instance._grid.TryToGetNodeWithPosition(position, out node);
        }

        private async void Init()
        {
            _grid.OnSeaNodesPercentChange += CheckForLevelComplete;

            _entitySpawner = new EntitySpawner(_grid);

            await _entitySpawner.Init();

            OnLevelLosen += ReloadLevel;
            OnLevelCompleted += ReloadLevel;

            await InitSpawn();

            _printUI.SetTimeSeconds(_minutesForLevelLeft);
            _printUI.SetLevelNumber(_levelNumber);
            _printUI.SetLifesNumber(_player.Lifes);

            InitCamera();

            StartCoroutine(LevelTimerCoroutine());
            StartCoroutine(EntitiesMoveCoroutine());
        }

        private async Task InitSpawn()
        {
            #region [Player Spawn]

            _enemies = new List<Enemy>(StartCountOfSeaEnemies + 1); // Sea enemies + one earth enemy;


            var playerSpawnTask = _entitySpawner.SpawnPlayer(_inputSystem);
            await playerSpawnTask;


            _player = playerSpawnTask.Result;

            _player.OnTrailNodeStepped += LoseLevel;

            OnLevelReloaded += () =>
            {
                _player.StopMoving();
                _player.transform.position = _grid.GetFieldTopCenterPosition();
            };

            OnLevelLosen += () => _printUI.SetLifesNumber(_player.Lifes);

            _player.OnNodesCorrupted += (corruptedNodes) =>
            {
                _grid.RemoveSeaNodes(corruptedNodes);
                _score += corruptedNodes.Count;
                _printUI.SetScoreNumber(_score);
            };

            _player.OnLivesEnd += EndGame;

            #endregion

            var earthEnemy = _entitySpawner.SpawnEnemy(EnemyType.EarthEnemy);
            earthEnemy.OnTrailNodeStepped += LoseLevel;
            _enemies.Add(earthEnemy);

            OnLevelReloaded += () => earthEnemy.transform.position = _grid.GetFieldBottomCenterPosition();

            for (int i = 0; i < StartCountOfSeaEnemies; i++)
                SpawnSeaEnemy();
        }

        private void InitCamera()
        {
            _mainCamera.transform.position = _grid.GetGridCenter();

            var leftXPosition = _mainCamera.ScreenToWorldPoint(new Vector2(CameraLeftSidePixelsPadding, 0f)).x;
            var leftSideGridPosition = _grid.GetGridLeftCenterPosition();

            var deltaX = leftSideGridPosition.x - leftXPosition;

            _mainCamera.transform.position += new Vector3(deltaX, 0f, -10f);
        }

        private void EndGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif

#if UNITY_ANDROID_API
            Application.Quit();
#endif
        }

        private void PassLevel()
        {
            _levelNumber++;

            _printUI.SetLevelNumber(_levelNumber);
            _printUI.SetFillPercent(0f);

            OnLevelCompleted?.Invoke();

            SpawnSeaEnemy();
        }

        private void LoseLevel()
        {
            OnLevelLosen?.Invoke();
        }

        private void ReloadLevel() => OnLevelReloaded?.Invoke();

        private void SpawnSeaEnemy()
        {
            var seaEnemy = _entitySpawner.SpawnEnemy(EnemyType.SeaEnemy);
            _enemies.Add(seaEnemy);

            OnLevelReloaded += () => seaEnemy.transform.position = _grid.GetRandomSeaFieldNodePosition();
            seaEnemy.OnTrailNodeStepped += LoseLevel;
        }

        private void CheckForLevelComplete(float currentSeaCorruptionPercent)
        {
            _printUI.SetFillPercent(currentSeaCorruptionPercent);

            if (currentSeaCorruptionPercent >= TargetSeaFieldCorruptionPercent)
                PassLevel();
        }

        /// <summary>
        /// Controlls properly move speed of the entity
        /// </summary>
        /// <returns></returns>
        private IEnumerator EntitiesMoveCoroutine()
        {
            while (true)
            {
                yield return MoveTimeYield;

                MoveEntity(_player);

                foreach (var entity in _enemies)
                {
                    MoveEntity(entity);

                    if (entity.Position == _player.Position)
                        LoseLevel();
                }
            }
        }

        private void MoveEntity(Entity entity)
        {
            if (!TryToGetNodeWithPosition(entity.NextPosition, out GridNode node))
            {
                entity.OnOutOfField();
                return;
            }

            entity.Move(node);
        }

        private IEnumerator LevelTimerCoroutine()
        {
            while (_minutesForLevelLeft > 0)
            {
                yield return OneMinuteYield;

                _minutesForLevelLeft -= 1f;
                _printUI.SetTimeSeconds(_minutesForLevelLeft);
            }

            EndGame();
        }



        private void Awake()
        {
            #region [Singleton]

            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            #endregion

            Init();
        }
    }
}
