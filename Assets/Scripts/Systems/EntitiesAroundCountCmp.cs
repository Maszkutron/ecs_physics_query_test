using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public struct EntitiesAroundCountCmp : IComponentData
{
    public int count;
    public float range;
}