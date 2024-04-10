using System;
using System.ComponentModel;
using Unity.Entities;
using UnityEngine;
using VisualScripting.Entities.Runtime;
using System.Collections.Generic;
[Serializable, ComponentEditor]
public struct GameManager : ISharedComponentData, IEquatable<GameManager>
{
    public UnityEngine.UI.Text Score;
    public UnityEngine.UI.Text WinText;
    public bool Equals(GameManager other)
    {
        return Score == other.Score && WinText == other.WinText;
    }

    public override int GetHashCode()
    {
        int hash = 0;
        if (!ReferenceEquals(Score, null))
            hash ^= Score.GetHashCode();
        if (!ReferenceEquals(WinText, null))
            hash ^= WinText.GetHashCode();
        return hash;
    }
}

[AddComponentMenu("Visual Scripting Components/GameManager")]
class GameManagerProxy : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
{
    public UnityEngine.UI.Text Score;
    public UnityEngine.UI.Text WinText;

    public void Convert(Unity.Entities.Entity entity, Unity.Entities.EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddSharedComponentData(entity, new GameManager { Score = Score, WinText = WinText });
    }

    public void DeclareReferencedPrefabs(List<UnityEngine.GameObject> referencedPrefabs)
    {
    }
}