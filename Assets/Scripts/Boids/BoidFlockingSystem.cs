using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Boids
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BoidSpatialHashBuildSystem))]
    public partial struct BoidFlockingSystem : ISystem
    {
        private EntityQuery _boidQuery;

        public void OnCreate(ref SystemState state)
        {
            _boidQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<BoidTag>(),
                ComponentType.ReadWrite<BoidVelocity>(),
                ComponentType.ReadWrite<LocalTransform>());

            state.RequireForUpdate<BoidSimulationConfig>();
            state.RequireForUpdate<BoidInteractionTarget>();
        }

        public void OnDestroy(ref SystemState state)
        {
            BoidSpatialHashState.CompletePendingAccess();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int boidCount = _boidQuery.CalculateEntityCount();
            if (boidCount == 0 || !BoidSpatialHashState.IsCreated || BoidSpatialHashState.Count != boidCount)
            {
                return;
            }

            state.Dependency = new FlockingJob
            {
                Config = SystemAPI.GetSingleton<BoidSimulationConfig>(),
                Target = SystemAPI.GetSingleton<BoidInteractionTarget>(),
                DeltaTime = SystemAPI.Time.DeltaTime,
                Snapshots = BoidSpatialHashState.Snapshots,
                CellMap = BoidSpatialHashState.CellMap
            }.ScheduleParallel(_boidQuery, state.Dependency);

            BoidSpatialHashState.RegisterPendingAccess(state.Dependency);
        }

        [BurstCompile]
        private partial struct FlockingJob : IJobEntity
        {
            [ReadOnly] public NativeArray<BoidSnapshot> Snapshots;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> CellMap;
            public BoidSimulationConfig Config;
            public BoidInteractionTarget Target;
            public float DeltaTime;

            public void Execute(
                [EntityIndexInQuery] int entityIndexInQuery,
                ref LocalTransform transform,
                ref BoidVelocity velocity,
                in BoidTag tag)
            {
                BoidSnapshot self = Snapshots[entityIndexInQuery];
                float neighborRadiusSq = Config.NeighborRadius * Config.NeighborRadius;
                float separationRadiusSq = Config.SeparationRadius * Config.SeparationRadius;

                float3 alignmentSum = float3.zero;
                float3 cohesionSum = float3.zero;
                float3 separationSum = float3.zero;
                int neighborCount = 0;
                int separationCount = 0;

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            int3 cell = self.Cell + new int3(x, y, z);
                            int cellHash = BoidMath.HashCell(cell);

                            if (!CellMap.TryGetFirstValue(cellHash, out int neighborIndex, out NativeParallelMultiHashMapIterator<int> iterator))
                            {
                                continue;
                            }

                            do
                            {
                                if (neighborIndex == entityIndexInQuery)
                                {
                                    continue;
                                }

                                BoidSnapshot neighbor = Snapshots[neighborIndex];
                                if (!math.all(neighbor.Cell == cell))
                                {
                                    continue;
                                }

                                float3 offset = neighbor.Position - self.Position;
                                float distanceSq = math.lengthsq(offset);
                                if (distanceSq > neighborRadiusSq || distanceSq < 0.0001f)
                                {
                                    continue;
                                }

                                alignmentSum += neighbor.Velocity;
                                cohesionSum += neighbor.Position;
                                neighborCount++;

                                if (distanceSq < separationRadiusSq)
                                {
                                    separationSum -= offset / math.max(distanceSq, 0.0001f);
                                    separationCount++;
                                }
                            }
                            while (CellMap.TryGetNextValue(out neighborIndex, ref iterator));
                        }
                    }
                }

                float3 currentVelocity = velocity.Value;
                float3 fallbackForward = transform.Forward();
                float3 steering = float3.zero;

                if (neighborCount > 0)
                {
                    float3 desiredAlignment = math.normalizesafe(alignmentSum / neighborCount, fallbackForward) * Config.MaxSpeed;
                    float3 desiredCohesion = math.normalizesafe((cohesionSum / neighborCount) - self.Position, fallbackForward) * Config.MaxSpeed;

                    steering += BoidMath.SteerTowards(desiredAlignment, currentVelocity, Config.MaxSteer) * Config.AlignmentWeight;
                    steering += BoidMath.SteerTowards(desiredCohesion, currentVelocity, Config.MaxSteer) * Config.CohesionWeight;
                }

                if (separationCount > 0)
                {
                    float3 desiredSeparation = math.normalizesafe(separationSum / separationCount, fallbackForward) * Config.MaxSpeed;
                    steering += BoidMath.SteerTowards(desiredSeparation, currentVelocity, Config.MaxSteer) * Config.SeparationWeight;
                }

                if (Target.IsActive != 0)
                {
                    float3 desiredTarget = math.normalizesafe(Target.Position - self.Position, fallbackForward) * Config.MaxSpeed;
                    steering += BoidMath.SteerTowards(desiredTarget, currentVelocity, Config.MaxSteer) * Config.TargetWeight;
                }

                float3 boundsDirection = CalculateBoundsDirection(self.Position);
                if (math.lengthsq(boundsDirection) > 0f)
                {
                    steering += BoidMath.SteerTowards(boundsDirection * Config.MaxSpeed, currentVelocity, Config.MaxSteer) * Config.BoundsWeight;
                }

                currentVelocity += steering * DeltaTime;

                float speed = math.length(currentVelocity);
                if (speed < 0.0001f)
                {
                    currentVelocity = fallbackForward * Config.MinSpeed;
                }
                else
                {
                    currentVelocity = currentVelocity / speed * math.clamp(speed, Config.MinSpeed, Config.MaxSpeed);
                }

                float3 heading = math.normalizesafe(currentVelocity, fallbackForward);
                transform.Position = self.Position + currentVelocity * DeltaTime;
                transform.Rotation = quaternion.LookRotationSafe(heading, math.up());
                velocity.Value = currentVelocity;
            }

            private float3 CalculateBoundsDirection(float3 position)
            {
                float margin = math.max(0.5f, math.min(Config.NeighborRadius, math.cmin(Config.BoundsExtents)));
                float3 marginVector = new float3(margin, margin, margin);
                float3 distanceIntoMargin = math.max(math.abs(position) - (Config.BoundsExtents - marginVector), float3.zero);
                return -math.sign(position) * math.saturate(distanceIntoMargin / marginVector);
            }
        }
    }
}
