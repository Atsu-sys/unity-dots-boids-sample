using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Boids
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(BoidSpatialHashBuildSystem))]
    public partial struct BoidSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BoidSimulationConfig>();
            state.RequireForUpdate<BoidPrefabReference>();
            state.RequireForUpdate<BoidSpawnState>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<BoidSpawnState> spawnState = SystemAPI.GetSingletonRW<BoidSpawnState>();
            if (spawnState.ValueRO.HasSpawned != 0)
            {
                return;
            }

            BoidSimulationConfig config = SystemAPI.GetSingleton<BoidSimulationConfig>();
            Entity prefabEntity = SystemAPI.GetSingleton<BoidPrefabReference>().Value;
            if (prefabEntity == Entity.Null)
            {
                return;
            }

            int boidCount = math.max(1, config.Count);
            var spawnedEntities = new NativeArray<Entity>(boidCount, Allocator.Temp);
            state.EntityManager.Instantiate(prefabEntity, spawnedEntities);

            Random random = Random.CreateFromIndex(math.max(1u, spawnState.ValueRO.RandomSeed));
            float minSpeed = math.min(config.MinSpeed, config.MaxSpeed);
            float maxSpeed = math.max(config.MinSpeed, config.MaxSpeed);

            for (int i = 0; i < spawnedEntities.Length; i++)
            {
                float3 direction = math.normalizesafe(random.NextFloat3Direction(), new float3(0f, 0f, 1f));
                float3 position = random.NextFloat3(-config.SpawnExtents, config.SpawnExtents);
                float speed = math.abs(maxSpeed - minSpeed) < 0.0001f ? minSpeed : random.NextFloat(minSpeed, maxSpeed);

                state.EntityManager.SetComponentData(
                    spawnedEntities[i],
                    LocalTransform.FromPositionRotationScale(position, quaternion.LookRotationSafe(direction, math.up()), 1f));
                state.EntityManager.SetComponentData(
                    spawnedEntities[i],
                    new BoidVelocity { Value = direction * speed });
            }

            spawnedEntities.Dispose();

            spawnState.ValueRW.HasSpawned = 1;
            spawnState.ValueRW.RandomSeed = random.NextUInt(1u, uint.MaxValue);
        }
    }
}
