using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Microsoft.CSharp;
using UnityEngine;

public class CameraControllerSystem : ComponentSystem
{
    private Unity.Entities.EntityQuery Camera_Query;
    private Unity.Entities.EntityQuery Player_Query;
    protected override void OnCreate()
    {
        Camera_Query = GetEntityQuery(ComponentType.ReadWrite<UnityEngine.Transform>(), ComponentType.ReadOnly<UnityEngine.Camera>(), ComponentType.ReadOnly<CameraControllerVS>(), ComponentType.ReadOnly<Unity.Transforms.Translation>());
        Player_Query = GetEntityQuery(ComponentType.ReadOnly<Unity.Transforms.Translation>(), ComponentType.ReadOnly<PlayerControllerVS>());
    }

    protected override void OnUpdate()
    {
        {
            Entities.With(Camera_Query).ForEach((Unity.Entities.Entity Camera_QueryEntity, UnityEngine.Transform Camera_QueryTransform) =>
            {
                var offset = new float3(0F, 10F, -10F);
                {
                    var Player_QueryEntities = Player_Query.ToEntityArray(Allocator.TempJob);
                    var Player_QueryTranslationArray = Player_Query.ToComponentDataArray<Unity.Transforms.Translation>(Allocator.TempJob);
                    for (int Player_QueryIdx = 0; Player_QueryIdx < Player_QueryEntities.Length; Player_QueryIdx++)
                    {
                        var Player_QueryEntity = Player_QueryEntities[Player_QueryIdx];
                        var Player_QueryTranslation = Player_QueryTranslationArray[Player_QueryIdx];
                        Camera_QueryTransform.position = (Player_QueryTranslation.Value + offset);
                    }

                    Player_QueryTranslationArray.Dispose();
                    Player_QueryEntities.Dispose();
                }
            }

            );
        }
    }
}