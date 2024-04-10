using System;
using System.ComponentModel;
using Unity.Entities;
using UnityEngine;
using VisualScripting.Entities.Runtime;
using System.Collections.Generic;
[Serializable, ComponentEditor]
public struct PlayerControllerVS : ISharedComponentData, IEquatable<PlayerControllerVS>
{
    public int Speed;
    public bool Equals(PlayerControllerVS other)
    {
        return Speed == other.Speed;
    }

    public override int GetHashCode()
    {
        int hash = 0;
        hash ^= Speed.GetHashCode();
        return hash;
    }
}

[AddComponentMenu("Visual Scripting Components/PlayerControllerVS")]
class PlayerControllerVSProxy : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
{
    public int Speed;

    public void Convert(Unity.Entities.Entity entity, Unity.Entities.EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddSharedComponentData(entity, new PlayerControllerVS { Speed = Speed });
    }

    public void DeclareReferencedPrefabs(List<UnityEngine.GameObject> referencedPrefabs)
    {
    }
}