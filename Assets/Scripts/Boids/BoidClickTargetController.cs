using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Boids
{
    [DisallowMultipleComponent]
    public sealed class BoidClickTargetController : MonoBehaviour
    {
        private World _cachedWorld;
        private EntityQuery _simulationQuery;
        private bool _hasQuery;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Camera targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null || !TryProjectPointerToTargetPlane(targetCamera, mouse.position.ReadValue(), out float3 targetPosition))
            {
                return;
            }

            if (!TryGetSimulationData(out EntityManager entityManager, out Entity simulationEntity, out BoidSimulationConfig config))
            {
                return;
            }

            targetPosition = math.clamp(targetPosition, -config.BoundsExtents, config.BoundsExtents);

            entityManager.SetComponentData(simulationEntity, new BoidInteractionTarget
            {
                Position = targetPosition,
                IsActive = 1
            });
        }

        private bool TryGetSimulationData(out EntityManager entityManager, out Entity simulationEntity, out BoidSimulationConfig config)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                entityManager = default;
                simulationEntity = Entity.Null;
                config = default;
                return false;
            }

            if (!_hasQuery || _cachedWorld != world)
            {
                _cachedWorld = world;
                _simulationQuery = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<BoidSimulationConfig>(),
                    ComponentType.ReadWrite<BoidInteractionTarget>());
                _hasQuery = true;
            }

            entityManager = world.EntityManager;
            if (_simulationQuery.IsEmptyIgnoreFilter)
            {
                simulationEntity = Entity.Null;
                config = default;
                return false;
            }

            simulationEntity = _simulationQuery.GetSingletonEntity();
            config = _simulationQuery.GetSingleton<BoidSimulationConfig>();
            return true;
        }

        private static bool TryProjectPointerToTargetPlane(Camera targetCamera, Vector2 screenPosition, out float3 targetPosition)
        {
            Plane targetPlane = new Plane(-targetCamera.transform.forward, Vector3.zero);
            Ray ray = targetCamera.ScreenPointToRay(screenPosition);

            if (targetPlane.Raycast(ray, out float hitDistance) && hitDistance >= 0f)
            {
                targetPosition = ray.GetPoint(hitDistance);
                return true;
            }

            targetPosition = targetCamera.transform.position + targetCamera.transform.forward * 60f;
            return true;
        }
    }
}
