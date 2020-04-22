using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Unity.Physics;
using Unity.Mathematics;

public class PhysicsBootstrap : MonoBehaviour
{
    public static BlobAssetReference<Unity.Physics.Collider> colliderUnitSmall { get; private set; }

    private void Awake()
    {
        colliderUnitSmall = Unity.Physics.SphereCollider.Create(new SphereGeometry() { Center = new float3(0, 0.5f, 0f), Radius = 0.5f });
    }
}
