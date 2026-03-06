using System;
using DotsRts.Systems;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DotsRts.MonoBehaviours
{
    public class GridSystemDebug : MonoBehaviour
    {
        public static GridSystemDebug Instance { get; private set; }
        [SerializeField] private Transform _debugPrefab;
        [SerializeField] private Sprite _circleSprite;
        [SerializeField] private Sprite _arrowSprite;

        private bool _isInit;
        private GridSystemDebugSingle[,] _gridSystemDebugSingleArray;

        private void Awake()
        {
            Instance = this;
        }

        public void InitializeGrid(GridSystem.GridSystemData gridSystemData)
        {
            if (_isInit)
            {
                return;
            }

            _isInit = true;

            _gridSystemDebugSingleArray = new GridSystemDebugSingle[gridSystemData.Width, gridSystemData.Height];
            for (int x = 0; x < gridSystemData.Width; x++)
            {
                for (int y = 0; y < gridSystemData.Height; y++)
                {
                    var debugTransform = Instantiate(_debugPrefab);
                    var gridSystemDebugSingle = debugTransform.GetComponent<GridSystemDebugSingle>();
                    gridSystemDebugSingle.Setup(x, y, gridSystemData.GridNodeSize);

                    _gridSystemDebugSingleArray[x, y] = gridSystemDebugSingle;
                }
            }
        }

        public void UpdateGrid(GridSystem.GridSystemData gridSystemData)
        {
            for (int x = 0; x < gridSystemData.Width; x++)
            {
                for (int y = 0; y < gridSystemData.Height; y++)
                {
                    var gridSystemDebugSingle = _gridSystemDebugSingleArray[x, y];

                    var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                    var index = GridSystem.CalculateIndex(x, y, gridSystemData.Width);
                    var gridIndex = gridSystemData.NextGridIndex - 1;
                    if (gridIndex < 0)
                    {
                        gridIndex = 0;
                    }

                    var gridNodeEntity = gridSystemData.GridMapArray[gridIndex].GridEntityArray[index];
                    var gridNode = entityManager.GetComponentData<GridSystem.GridNode>(gridNodeEntity);

                    if (gridNode.Cost == 0)
                    {
                        // This is the target
                        gridSystemDebugSingle.SetSprite(_circleSprite);
                        gridSystemDebugSingle.SetColor(Color.green);
                    }
                    else
                    {
                        if (gridNode.Cost == GridSystem.WALL_COST)
                        {
                            gridSystemDebugSingle.SetSprite(_circleSprite);
                            gridSystemDebugSingle.SetColor(Color.black);
                        }
                        else
                        {
                            gridSystemDebugSingle.SetSprite(_arrowSprite);
                            gridSystemDebugSingle.SetColor(Color.white);
                            gridSystemDebugSingle.SetSpriteRotation(
                                Quaternion.LookRotation(new float3(gridNode.Vector.x, 0f, gridNode.Vector.y),
                                    Vector3.up));
                        }
                    }
                }
            }
        }
    }
}