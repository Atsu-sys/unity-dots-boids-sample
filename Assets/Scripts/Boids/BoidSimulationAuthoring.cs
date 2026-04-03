using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Boids
{
    [DisallowMultipleComponent]
    public sealed class BoidSimulationAuthoring : MonoBehaviour
    {
        public GameObject BoidPrefab;
        [Min(1)] public int Count = 5000;
        [Min(0.1f)] public float NeighborRadius = 6f;
        [Min(0.1f)] public float SeparationRadius = 2f;
        [Min(0.1f)] public float MinSpeed = 8f;
        [Min(0.1f)] public float MaxSpeed = 14f;
        [Min(0.1f)] public float MaxSteer = 12f;
        [Min(0f)] public float AlignmentWeight = 1f;
        [Min(0f)] public float CohesionWeight = 0.65f;
        [Min(0f)] public float SeparationWeight = 1.8f;
        [Min(0f)] public float BoundsWeight = 1.4f;
        [Min(0f)] public float TargetWeight = 1.2f;
        public Vector3 BoundsExtents = new Vector3(60f, 40f, 60f);
        public Vector3 SpawnExtents = new Vector3(40f, 25f, 40f);

        private void OnValidate()
        {
            Count = Mathf.Max(1, Count);
            NeighborRadius = Mathf.Max(0.1f, NeighborRadius);
            SeparationRadius = Mathf.Clamp(SeparationRadius, 0.1f, NeighborRadius);
            MinSpeed = Mathf.Max(0.1f, MinSpeed);
            MaxSpeed = Mathf.Max(MinSpeed, MaxSpeed);
            MaxSteer = Mathf.Max(0.1f, MaxSteer);
            AlignmentWeight = Mathf.Max(0f, AlignmentWeight);
            CohesionWeight = Mathf.Max(0f, CohesionWeight);
            SeparationWeight = Mathf.Max(0f, SeparationWeight);
            BoundsWeight = Mathf.Max(0f, BoundsWeight);
            TargetWeight = Mathf.Max(0f, TargetWeight);
            BoundsExtents = new Vector3(
                Mathf.Max(1f, BoundsExtents.x),
                Mathf.Max(1f, BoundsExtents.y),
                Mathf.Max(1f, BoundsExtents.z));
            SpawnExtents = new Vector3(
                Mathf.Clamp(SpawnExtents.x, 0.5f, BoundsExtents.x),
                Mathf.Clamp(SpawnExtents.y, 0.5f, BoundsExtents.y),
                Mathf.Clamp(SpawnExtents.z, 0.5f, BoundsExtents.z));
        }

        private sealed class BoidSimulationBaker : Baker<BoidSimulationAuthoring>
        {
            public override void Bake(BoidSimulationAuthoring authoring)
            {
                if (authoring.BoidPrefab == null)
                {
                    Debug.LogError("Boid prefab is required for DOTS baking.", authoring);
                    return;
                }

                Entity simulationEntity = GetEntity(TransformUsageFlags.None);
                Entity prefabEntity = GetEntity(authoring.BoidPrefab, TransformUsageFlags.Dynamic);

                AddComponent(simulationEntity, new BoidSimulationConfig
                {
                    Count = math.max(1, authoring.Count),
                    NeighborRadius = math.max(0.1f, authoring.NeighborRadius),
                    SeparationRadius = math.clamp(authoring.SeparationRadius, 0.1f, math.max(0.1f, authoring.NeighborRadius)),
                    MinSpeed = math.max(0.1f, authoring.MinSpeed),
                    MaxSpeed = math.max(math.max(0.1f, authoring.MinSpeed), authoring.MaxSpeed),
                    MaxSteer = math.max(0.1f, authoring.MaxSteer),
                    AlignmentWeight = math.max(0f, authoring.AlignmentWeight),
                    CohesionWeight = math.max(0f, authoring.CohesionWeight),
                    SeparationWeight = math.max(0f, authoring.SeparationWeight),
                    BoundsWeight = math.max(0f, authoring.BoundsWeight),
                    TargetWeight = math.max(0f, authoring.TargetWeight),
                    BoundsExtents = (float3)authoring.BoundsExtents,
                    SpawnExtents = (float3)authoring.SpawnExtents
                });
                AddComponent(simulationEntity, new BoidPrefabReference { Value = prefabEntity });
                AddComponent(simulationEntity, new BoidSpawnState { RandomSeed = 1u, HasSpawned = 0 });
                AddComponent(simulationEntity, new BoidInteractionTarget { Position = float3.zero, IsActive = 0 });
            }
        }
    }
}
