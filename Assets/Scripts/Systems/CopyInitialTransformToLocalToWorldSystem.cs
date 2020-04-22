using UnityEngine;
using UnityEngine.Jobs;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using Unity.Burst;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public class CopyInitialTransformToLocalToWorldSystem : JobComponentSystem
{
    private EntityQuery query = null;
    private EndInitializationEntityCommandBufferSystem barrier = null;

    protected override void OnCreate()
    {
        base.OnCreate();

        query = GetEntityQuery(
                ComponentType.ReadOnly<Transform>(),
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.ReadOnly<CopyInitialTransformToLocalToWorldTagCmp>()
            );

        barrier = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        // first copy all transform data to cache buffer
        var transforms = query.GetTransformAccessArray();

        var transformCache = new NativeArray<TransformData>(transforms.length, Allocator.TempJob);
        inputDeps = new PrepareTransformDataJob()
        {
            transformCache = transformCache
        }.Schedule(transforms, inputDeps);

        // copy positions
        var copyPositionsJobHandle = Entities.WithAll<CopyInitialTransformToLocalToWorldTagCmp, Transform, LocalToWorld>().ForEach((int entityInQueryIndex, ref Translation translation) =>
        {
            translation.Value = transformCache[entityInQueryIndex].translation;
        })
        .WithReadOnly(transformCache)
        .WithBurst()
        .Schedule(inputDeps);

        // copy rotations
        var copyRotationsJobHandle = Entities.WithAll<CopyInitialTransformToLocalToWorldTagCmp, Transform, LocalToWorld>().ForEach((int entityInQueryIndex, ref Rotation rotation) =>
        {
            rotation.Value = transformCache[entityInQueryIndex].rotation;
        })
        .WithReadOnly(transformCache)
        .WithBurst()
        .Schedule(inputDeps);

        // copy data over to localToWorld
        inputDeps = Entities.WithAll<CopyInitialTransformToLocalToWorldTagCmp, Transform>().ForEach((int entityInQueryIndex, ref LocalToWorld localToWorld) =>
        {
            localToWorld.Value = float4x4.TRS(
                    transformCache[entityInQueryIndex].translation,
                    transformCache[entityInQueryIndex].rotation,
                    new float3(1.0f, 1.0f, 1.0f)
                );
        })
        .WithReadOnly(transformCache)
        .WithDeallocateOnJobCompletion(transformCache)
        .WithBurst()
        .Schedule(JobHandle.CombineDependencies(copyPositionsJobHandle, copyRotationsJobHandle));

        // remove components
        //EntityCommandBuffer.Concurrent commandBuffer = barrier.CreateCommandBuffer().ToConcurrent();
        //inputDeps = Entities.WithAll<LocalToWorld, CopyInitialTransformToLocalToWorldTagCmp, Transform>().ForEach((Entity entity, int entityInQueryIndex) =>
        //{
        //    commandBuffer.RemoveComponent<CopyInitialTransformToLocalToWorldTagCmp>(entityInQueryIndex, entity);
        //})
        //.WithBurst()
        //.Schedule(inputDeps); 

        barrier.AddJobHandleForProducer(inputDeps);
        return inputDeps;
    }

    [BurstCompile]
    private struct PrepareTransformDataJob : IJobParallelForTransform
    {
        public NativeArray<TransformData> transformCache;

        public void Execute(int index, TransformAccess transform)
        {
            transformCache[index] = new TransformData
            {
                rotation = transform.rotation,
                translation = transform.position,
            };
        }
    }

    private struct TransformData
    {
        public float3 translation;
        public quaternion rotation;
    }
}
