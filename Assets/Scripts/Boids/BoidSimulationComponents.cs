using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Boids
{
    public struct BoidSimulationConfig : IComponentData
    {
        public int Count;
        public float NeighborRadius;
        public float SeparationRadius;
        public float MinSpeed;
        public float MaxSpeed;
        public float MaxSteer;
        public float AlignmentWeight;
        public float CohesionWeight;
        public float SeparationWeight;
        public float BoundsWeight;
        public float TargetWeight;
        public float3 BoundsExtents;
        public float3 SpawnExtents;
    }

    public struct BoidPrefabReference : IComponentData
    {
        public Entity Value;
    }

    public struct BoidSpawnState : IComponentData
    {
        public byte HasSpawned;
        public uint RandomSeed;
    }

    public struct BoidInteractionTarget : IComponentData
    {
        public float3 Position;
        public byte IsActive;
    }

    public struct BoidTag : IComponentData
    {
    }

    public struct BoidVelocity : IComponentData
    {
        public float3 Value;
    }

    internal struct BoidSnapshot
    {
        public float3 Position;
        public float3 Velocity;
        public int3 Cell;
    }

    internal static class BoidMath
    {
        public static int3 PositionToCell(float3 position, float cellSize)
        {
            return (int3)math.floor(position / cellSize);
        }

        public static int HashCell(int3 cell)
        {
            return unchecked((int)math.hash(cell));
        }

        public static float3 SteerTowards(float3 desiredVelocity, float3 currentVelocity, float maxSteer)
        {
            float3 steering = desiredVelocity - currentVelocity;
            float steeringLengthSq = math.lengthsq(steering);
            float maxSteerSq = maxSteer * maxSteer;

            if (steeringLengthSq <= maxSteerSq)
            {
                return steering;
            }

            return math.normalizesafe(steering) * maxSteer;
        }
    }

    internal static class BoidSpatialHashState
    {
        public static NativeArray<BoidSnapshot> Snapshots;
        public static NativeParallelMultiHashMap<int, int> CellMap;
        public static JobHandle PendingAccess;
        public static int Count;

        public static bool IsCreated => Snapshots.IsCreated && CellMap.IsCreated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDomainState()
        {
            Dispose();
        }

        public static void EnsureCapacity(int count)
        {
            int capacity = math.max(1, count);

            if (!Snapshots.IsCreated || Snapshots.Length != capacity)
            {
                if (Snapshots.IsCreated)
                {
                    Snapshots.Dispose();
                }

                Snapshots = new NativeArray<BoidSnapshot>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            if (!CellMap.IsCreated || CellMap.Capacity < capacity)
            {
                if (CellMap.IsCreated)
                {
                    CellMap.Dispose();
                }

                CellMap = new NativeParallelMultiHashMap<int, int>(capacity, Allocator.Persistent);
            }
        }

        public static void Clear()
        {
            if (CellMap.IsCreated)
            {
                CellMap.Clear();
            }

            Count = 0;
        }

        public static void RegisterPendingAccess(JobHandle handle)
        {
            PendingAccess = handle;
        }

        public static void CompletePendingAccess()
        {
            PendingAccess.Complete();
            PendingAccess = default;
        }

        public static void Dispose()
        {
            CompletePendingAccess();

            if (Snapshots.IsCreated)
            {
                Snapshots.Dispose();
            }

            if (CellMap.IsCreated)
            {
                CellMap.Dispose();
            }

            Count = 0;
        }
    }
}
