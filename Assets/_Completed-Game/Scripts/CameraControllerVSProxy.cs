using System;
using System.ComponentModel;
using Unity.Entities;
using UnityEngine;
using VisualScripting.Entities.Runtime;
using System.Collections.Generic;
[Serializable, ComponentEditor]
public struct CameraControllerVS : IComponentData
{
    public Unity.Entities.Entity Player;
}

[AddComponentMenu("Visual Scripting Components/CameraControllerVS")]
class CameraControllerVSProxy : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
{
    public UnityEngine.GameObject Player;

    public void Convert(Unity.Entities.Entity entity, Unity.Entities.EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponentData(entity, new CameraControllerVS { Player = conversionSystem.GetPrimaryEntity(Player) });
    }

    public void DeclareReferencedPrefabs(List<UnityEngine.GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(Player);
    }
}