using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Microsoft.CSharp;
using UnityEngine;

public class GameSystem : ComponentSystem
{
    private Unity.Entities.EntityQuery DestroyPickup_Query_MissingAddPointEvent;
    private Unity.Entities.EntityQuery DestroyPickup_Query;
    private Unity.Entities.EntityQuery Empty_Query_WithAddPointEvent;
    private Unity.Entities.EntityQuery Empty_Query;
    private Unity.Entities.EntityQuery GameManager_Query;
    private Unity.Entities.EntityQuery GameManager_Query0Enter;
    private Unity.Entities.EntityQuery PickupObjects_Query;
    public struct GameManager_QueryTracking : Unity.Entities.ISystemStateComponentData
    {
    }

    public struct GraphData : Unity.Entities.IComponentData
    {
        public int Game_Score;
        public int Pickups_Amount;
    }

    protected override void OnCreate()
    {
        EventSystem<AddPointEvent>.Initialize(World);
        DestroyPickup_Query_MissingAddPointEvent = GetEntityQuery(ComponentType.Exclude<AddPointEvent>(), ComponentType.ReadOnly<DestroyTag>(), ComponentType.ReadOnly<PickupTag>());
        DestroyPickup_Query = GetEntityQuery(ComponentType.ReadOnly<DestroyTag>(), ComponentType.ReadOnly<PickupTag>());
        Empty_Query_WithAddPointEvent = GetEntityQuery(ComponentType.ReadOnly<AddPointEvent>());
        Empty_Query = EntityManager.UniversalQuery;
        GameManager_Query = GetEntityQuery(ComponentType.ReadWrite<GameManager_QueryTracking>(), ComponentType.ReadWrite<GameManager>());
        GameManager_Query0Enter = GetEntityQuery(ComponentType.Exclude<GameManager_QueryTracking>(), ComponentType.ReadWrite<GameManager>());
        PickupObjects_Query = GetEntityQuery(ComponentType.ReadOnly<PickupTag>());
        EntityManager.CreateEntity(typeof (GraphData));
        SetSingleton(new GraphData{Game_Score = 0, Pickups_Amount = 0});
    }

    protected override void OnUpdate()
    {
        GraphData graphData = GetSingleton<GraphData>();
        {
            EventSystem<AddPointEvent>.AddMissingBuffers(Entities, DestroyPickup_Query_MissingAddPointEvent, EntityManager);
            Entities.With(DestroyPickup_Query).ForEach((Unity.Entities.Entity DestroyPickup_QueryEntity) =>
            {
                var DestroyPickup_Query_AddPointEventBuffer = EntityManager.GetBuffer<AddPointEvent>(DestroyPickup_QueryEntity);
                PostUpdateCommands.DestroyEntity(DestroyPickup_QueryEntity);
                DestroyPickup_Query_AddPointEventBuffer.Add(new AddPointEvent{});
                graphData.Pickups_Amount = (graphData.Pickups_Amount - 1);
            }

            );
        }

        {
            Entities.With(Empty_Query_WithAddPointEvent).ForEach((Unity.Entities.Entity Empty_QueryEntity, DynamicBuffer<AddPointEvent> eventBuffer) =>
            {
                for (int event_index = 0; event_index < eventBuffer.Length; event_index++)
                {
                    AddPointEvent ev = eventBuffer[event_index];
                    {
                        var GameManager_QueryEntities = GameManager_Query.ToEntityArray(Allocator.TempJob);
                        for (int GameManager_QueryIdx = 0; GameManager_QueryIdx < GameManager_QueryEntities.Length; GameManager_QueryIdx++)
                        {
                            var GameManager_QueryEntity = GameManager_QueryEntities[GameManager_QueryIdx];
                            var GameManager_QueryGameManager = EntityManager.GetSharedComponentData<GameManager>(GameManager_QueryEntity);
                            graphData.Game_Score++;
                            GameManager_QueryGameManager.Score.text = graphData.Game_Score.ToString();
                            if ((graphData.Pickups_Amount <= 0))
                            {
                                GameManager_QueryGameManager.WinText.enabled = true;
                                Time.timeScale = 0F;
                            }

                            PostUpdateCommands.SetSharedComponent<GameManager>(GameManager_QueryEntities[GameManager_QueryIdx], GameManager_QueryGameManager);
                        }

                        GameManager_QueryEntities.Dispose();
                    }
                }
            }

            );
        }

        {
            Entities.With(GameManager_Query0Enter).ForEach((Unity.Entities.Entity GameManager_QueryEntity, GameManager GameManager_Query0EnterGameManager) =>
            {
                {
                    var PickupObjects_QueryEntities = PickupObjects_Query.ToEntityArray(Allocator.TempJob);
                    for (int PickupObjects_QueryIdx = 0; PickupObjects_QueryIdx < PickupObjects_QueryEntities.Length; PickupObjects_QueryIdx++)
                    {
                        var PickupObjects_QueryEntity = PickupObjects_QueryEntities[PickupObjects_QueryIdx];
                        graphData.Pickups_Amount = (graphData.Pickups_Amount + 1);
                    }

                    PickupObjects_QueryEntities.Dispose();
                }

                GameManager_Query0EnterGameManager.Score.text = graphData.Game_Score.ToString();
                GameManager_Query0EnterGameManager.WinText.enabled = false;
                PostUpdateCommands.AddComponent<GameManager_QueryTracking>(GameManager_QueryEntity, default (GameManager_QueryTracking));
            }

            );
        }

        SetSingleton(graphData);
    }
}