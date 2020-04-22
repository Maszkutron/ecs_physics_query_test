using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;

public class OverlapTester : MonoBehaviour
{
    private static int ID = 0;
    private static double[] timeTable = new double[1024*16];
    private static Collider[] overlapCache = new Collider[4];
    public int scanIterations = 3;
    private int myID = 0;
    private Stopwatch spw;

    public float accquisitionRange = 10;
    public double lastScanTime;

    private void Start()
    {
        myID = ID++;
        var direction = Random.insideUnitCircle * 50f;
        var position = new Vector3(direction.x, 0, direction.y);
        transform.position = position;
        spw = new Stopwatch();
    }

    //private void Update()
    //{
    //    spw.Restart();
    //    target = FindNewTarget();
    //    spw.Stop();
    //    lastScanTime = spw.Elapsed.TotalMilliseconds;
    //}

    private Collider FindNewTarget()
    {
        var position = transform.position;
        var scanRadius = accquisitionRange;
        var cache = overlapCache;
        
        for (int i = 0; i < scanIterations; ++i)
        {
            int count = Physics.OverlapSphereNonAlloc(position, scanRadius, cache, -1, QueryTriggerInteraction.Collide);
            if(count > 0)
            {
                return FindNearestCollider(count, cache);
            }
        }
        return null;
    }

    private Collider FindNearestCollider(int endIndex, Collider[] colliders)
    {
        var position = transform.position;
        var minDistance = float.MaxValue;
        int minIndex = 0;

        for(int i = 0; i < endIndex; ++i)
        {
            var dist = (colliders[i].transform.position - position).sqrMagnitude;
            if ( dist < minDistance )
            {
                minDistance = dist;
                minIndex = i;
            }
        }
        return colliders[minIndex];
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, accquisitionRange);
    }
}
