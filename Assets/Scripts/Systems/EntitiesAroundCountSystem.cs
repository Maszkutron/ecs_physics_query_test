using Unity.Jobs;
using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EndFramePhysicsSystem))]
public unsafe class EntitiesAroundCountSystem : JobComponentSystem
{
    private EndFramePhysicsSystem finalPhysicsSystem = null;
    private BuildPhysicsWorld buildPhysicsSystem = null;
    private StepPhysicsWorld stepPhysicsSystem = null;
    private EndSimulationEntityCommandBufferSystem barrier = null;

    protected override void OnCreate()
    {
        base.OnCreate();
        finalPhysicsSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndFramePhysicsSystem>();
        buildPhysicsSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<BuildPhysicsWorld>();
        stepPhysicsSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<StepPhysicsWorld>();
        barrier = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        // overall:
        //  - world queries always return expected values but are slower than expected
        //  - collider-to-collider/collider-to-point either do not return expected values or always return false

        // broken, see method body for more info
        return UpdateWithCollider2ColliderQuery(inputDeps);

        // works but is much less performant than I anticipated
        //return UpdateWithWorldQuery(inputDeps);
    }

    private JobHandle UpdateWithCollider2ColliderQuery(JobHandle inputDeps)
    {
        var physicsWorldSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>();
        var collisionWorld = physicsWorldSystem.PhysicsWorld.CollisionWorld;

        // this is probably redundant but just for safety lets include all previous physics systems
        inputDeps = JobHandle.CombineDependencies(inputDeps, finalPhysicsSystem.FinalJobHandle);
        inputDeps = JobHandle.CombineDependencies(inputDeps, buildPhysicsSystem.FinalJobHandle);
        inputDeps = JobHandle.CombineDependencies(inputDeps, stepPhysicsSystem.FinalJobHandle);

        inputDeps = Entities.ForEach((Entity entity, ref EntitiesAroundCountCmp around, in Translation translation, in PhysicsCollider collider) =>
        {
            around.count = 0;

            float3 offset = new float3(around.range, 1, around.range);
            OverlapAabbInput input = new OverlapAabbInput()
            {
                Aabb = new Aabb()
                {
                    Min = translation.Value - offset,
                    Max = translation.Value + offset,
                },
                Filter = CollisionFilter.Default
            };

            NativeList<int> bodyIndices = new NativeList<int>(Allocator.Temp);

            // OverlapAabb is really nice and fast, all expected colliders are returned
            if (collisionWorld.OverlapAabb(input, ref bodyIndices))
            {
                for (int i = 0; i < bodyIndices.Length; ++i)
                {
                    var body = collisionWorld.Bodies[bodyIndices[i]];

                    // why this returns true for colliders in AABB instead of actual distance?
                    var colliderDistanceInput = new ColliderDistanceInput()
                    {
                        Collider = collider.ColliderPtr,
                        Transform = RigidTransform.identity,
                        MaxDistance = around.range
                    };

                    // why this always returns false?
                    var pointDistanceInput = new PointDistanceInput()
                    { 
                        Filter = CollisionFilter.Default,
                        MaxDistance = around.range,
                        Position = translation.Value
                    };

                    if (body.CalculateDistance(pointDistanceInput))
                    {
                        ++around.count;
                    }
                }
            }
            bodyIndices.Dispose();
        })
        .WithBurst()
        .Schedule(inputDeps);

        barrier.AddJobHandleForProducer(inputDeps);

        return inputDeps;
    }

    private JobHandle UpdateWithWorldQuery(JobHandle inputDeps)
    {
        var physicsWorldSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>();
        var collisionWorld = physicsWorldSystem.PhysicsWorld.CollisionWorld;

        // this is probably redundant but just for safety lets include all previous physics systems
        inputDeps = JobHandle.CombineDependencies(inputDeps, finalPhysicsSystem.FinalJobHandle);
        inputDeps = JobHandle.CombineDependencies(inputDeps, buildPhysicsSystem.FinalJobHandle);
        inputDeps = JobHandle.CombineDependencies(inputDeps, stepPhysicsSystem.FinalJobHandle);

        inputDeps = Entities.ForEach((Entity entity, ref EntitiesAroundCountCmp around, in Translation translation, in PhysicsCollider collider) =>
        {
            NativeList<DistanceHit> distanceHits = new NativeList<DistanceHit>(Allocator.Temp);

            // why in CollisionWorld queries I need to explicitly create RigidTransform
            // but then in ICollider queries I only identity works
            var colliderDistanceInput = new ColliderDistanceInput()
            {
                Collider = collider.ColliderPtr,
                MaxDistance = around.range,
                Transform = new RigidTransform(quaternion.identity, translation.Value)
            };

            // point query for performance testing
            var pointDistanceInput = new PointDistanceInput()
            {
                Filter = CollisionFilter.Default,
                MaxDistance = around.range,
                Position = translation.Value
            };

            if (collisionWorld.CalculateDistance(colliderDistanceInput, ref distanceHits))
            {
                around.count = distanceHits.Length;
            }
            else around.count = 0;

            distanceHits.Dispose();
        })
        .WithBurst()
        .Schedule(inputDeps);

        barrier.AddJobHandleForProducer(inputDeps);

        return inputDeps;
    }
}