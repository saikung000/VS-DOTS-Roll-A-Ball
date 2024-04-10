using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Microsoft.CSharp;
using UnityEngine;

public class PlayerControllerSystem : ComponentSystem
{
    private Unity.Entities.EntityQuery Component_Query;
    private Unity.Entities.EntityQuery Pickup_Query;
    protected override void OnCreate()
    {
        Component_Query = GetEntityQuery(ComponentType.ReadWrite<Unity.Transforms.Translation>(), ComponentType.ReadOnly<PlayerControllerVS>(), ComponentType.ReadOnly<JumpComponent>());
        Pickup_Query = GetEntityQuery(ComponentType.ReadOnly<PickupTag>(), ComponentType.ReadOnly<Unity.Transforms.Translation>());
    }

    protected override void OnUpdate()
    {
        {
            Entities.With(Component_Query).ForEach((Unity.Entities.Entity Component_QueryEntity, PlayerControllerVS Component_QueryPlayerControllerVS, ref Unity.Transforms.Translation Component_QueryTranslation, ref JumpComponent Component_QueryJumpComponent) =>
            {
                var Variable = 0F;
                var var = 0F;
                Component_QueryTranslation.Value.x = math.clamp((Component_QueryTranslation.Value.x + ((UnityEngine.Input.GetAxis("Horizontal") * Time.deltaTime) * Component_QueryPlayerControllerVS.Speed)), -9F, 9F);
                Component_QueryTranslation.Value.y = math.clamp((Component_QueryTranslation.Value.z + ((UnityEngine.Input.GetAxis("Jump") * Time.deltaTime) * Component_QueryJumpComponent.jumpPower)), -1F, 5F);
                Component_QueryTranslation.Value.z = math.clamp((Component_QueryTranslation.Value.z + ((UnityEngine.Input.GetAxis("Vertical") * Time.deltaTime) * Component_QueryPlayerControllerVS.Speed)), -9F, 9F);
                {
                    var Pickup_QueryEntities = Pickup_Query.ToEntityArray(Allocator.TempJob);
                    var Pickup_QueryTranslationArray = Pickup_Query.ToComponentDataArray<Unity.Transforms.Translation>(Allocator.TempJob);
                    for (int Pickup_QueryIdx = 0; Pickup_QueryIdx < Pickup_QueryEntities.Length; Pickup_QueryIdx++)
                    {
                        var Pickup_QueryEntity = Pickup_QueryEntities[Pickup_QueryIdx];
                        var Pickup_QueryTranslation = Pickup_QueryTranslationArray[Pickup_QueryIdx];
                        if ((1F >= math.distance(Component_QueryTranslation.Value, Pickup_QueryTranslation.Value)))
                        {
                            PostUpdateCommands.AddComponent<DestroyTag>(Pickup_QueryEntity, new DestroyTag{});
                        }
                    }

                    Pickup_QueryTranslationArray.Dispose();
                    Pickup_QueryEntities.Dispose();
                }
            }

            );
        }
    }
}