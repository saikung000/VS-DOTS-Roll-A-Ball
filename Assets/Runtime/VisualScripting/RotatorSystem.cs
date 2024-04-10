using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Microsoft.CSharp;
using UnityEngine;

public class RotatorSystem : JobComponentSystem
{
    private Unity.Entities.EntityQuery Rotator_Query;
    [BurstCompile]
    struct Update_Rotator_Query_Job : IJobForEachWithEntity_ECC<Unity.Transforms.Rotation, RotatorVS>
    {
        public float TimeDeltaTime;
        public void Execute(Unity.Entities.Entity Rotator_QueryEntity, int Update_Rotator_Query_JobIdx, ref Unity.Transforms.Rotation Rotator_QueryRotation, [ReadOnlyAttribute] ref RotatorVS Rotator_QueryRotatorVS)
        {
            Rotator_QueryRotation.Value = math.mul(Rotator_QueryRotation.Value, quaternion.AxisAngle(math.normalize((Rotator_QueryRotatorVS.Speed * TimeDeltaTime)), 0.01F));
        }
    }

    protected override void OnCreate()
    {
        Rotator_Query = GetEntityQuery(ComponentType.ReadWrite<Unity.Transforms.Rotation>(), ComponentType.ReadOnly<RotatorVS>());
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        inputDeps = JobForEachExtensions.Schedule(new Update_Rotator_Query_Job()
        {TimeDeltaTime = Time.deltaTime}, Rotator_Query, inputDeps);
        return inputDeps;
    }
}