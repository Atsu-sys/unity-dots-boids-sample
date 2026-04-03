using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Boids
{
    [DisallowMultipleComponent]
    public sealed class BoidPrefabAuthoring : MonoBehaviour
    {
        private sealed class BoidPrefabBaker : Baker<BoidPrefabAuthoring>
        {
            public override void Bake(BoidPrefabAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<BoidTag>(entity);
                AddComponent(entity, new BoidVelocity { Value = float3.zero });
            }
        }
    }
}
