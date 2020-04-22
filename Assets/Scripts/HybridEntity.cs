using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;

using Collider = Unity.Physics.Collider;
using SphereCollider = Unity.Physics.SphereCollider;
using Unity.Physics.Authoring;
using Unity.Mathematics;

[SelectionBase]
public class HybridEntity : MonoBehaviour
{
    private EntityManager entityManager = null;
    private Entity entity;

    private void OnEnable()
    {
        // some random initial position
        var position = UnityEngine.Random.insideUnitCircle * 80f;
        transform.position = new Vector3(position.x, 0, position.y);

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype archetype = entityManager.CreateArchetype(
                typeof(Translation),
                typeof(Rotation),
                typeof(LocalToWorld),
                typeof(CopyInitialTransformToLocalToWorldTagCmp),
                typeof(CopyTransformToGameObject),
                typeof(CopyTransformFromGameObject),

                typeof(EntitiesAroundCountCmp),

                typeof(PhysicsCollider),
                typeof(PhysicsMass)
            );

        entity = entityManager.CreateEntity(archetype);
        entityManager.SetName(entity, name);
        entityManager.SetComponentData(entity, new PhysicsCollider() { Value = PhysicsBootstrap.colliderUnitSmall });
        entityManager.SetComponentData(entity, new EntitiesAroundCountCmp() { range = 10f });
        entityManager.SetComponentData(entity, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
        entityManager.AddComponentObject(entity, transform);
    }

    private void OnDrawGizmosSelected()
    {
        // range-radius
        Gizmos.DrawWireSphere(transform.position, 10.0f);

        // range AABB
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(10, 1, 10) * 2);
    }
}
