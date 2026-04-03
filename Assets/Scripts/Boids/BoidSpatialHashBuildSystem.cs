using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Boids
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BoidSpawnSystem))]
    [UpdateBefore(typeof(BoidFlockingSystem))]
    public partial struct BoidSpatialHashBuildSystem : ISystem
    {
        private EntityQuery _boidQuery;

        public void OnCreate(ref SystemState state)
        {
            _boidQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<BoidTag>(),
                ComponentType.ReadOnly<BoidVelocity>(),
                ComponentType.ReadOnly<LocalTransform>());

            state.RequireForUpdate<BoidSimulationConfig>();
        }

        public void OnDestroy(ref SystemState state)
        {
            BoidSpatialHashState.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            BoidSpatialHashState.CompletePendingAccess();

            int boidCount = _boidQuery.CalculateEntityCount();
            if (boidCount == 0)
            {
                BoidSpatialHashState.Clear();
                return;
            }

            BoidSimulationConfig config = SystemAPI.GetSingleton<BoidSimulationConfig>();
            float cellSize = math.max(config.NeighborRadius, 0.1f);

            BoidSpatialHashState.EnsureCapacity(boidCount);
            BoidSpatialHashState.CellMap.Clear();
            BoidSpatialHashState.Count = boidCount;

            JobHandle collectHandle = new CollectBoidSnapshotsJob
            {
                CellSize = cellSize,
                Snapshots = BoidSpatialHashState.Snapshots
            }.ScheduleParallel(_boidQuery, state.Dependency);

            state.Dependency = new BuildSpatialHashJob
            {
                Snapshots = BoidSpatialHashState.Snapshots,
                CellMap = BoidSpatialHashState.CellMap.AsParallelWriter()
            }.Schedule(boidCount, 64, collectHandle);

            BoidSpatialHashState.RegisterPendingAccess(state.Dependency);
        }

        [BurstCompile]
        private partial struct CollectBoidSnapshotsJob : IJobEntity
        {
            public NativeArray<BoidSnapshot> Snapshots;
            public float CellSize;

            public void Execute(
                [EntityIndexInQuery] int entityIndexInQuery,
                in LocalTransform transform,
                in BoidVelocity velocity,
                in BoidTag tag)
            {
                Snapshots[entityIndexInQuery] = new BoidSnapshot
                {
                    Position = transform.Position,
                    Velocity = velocity.Value,
                    Cell = BoidMath.PositionToCell(transform.Position, CellSize)
                };
            }
        }

        [BurstCompile]
        private struct BuildSpatialHashJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<BoidSnapshot> Snapshots;
            public NativeParallelMultiHashMap<int, int>.ParallelWriter CellMap;

            public void Execute(int index)
            {
                CellMap.Add(BoidMath.HashCell(Snapshots[index].Cell), index);
            }
        }
    }
}
