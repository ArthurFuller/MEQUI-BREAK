using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class AvatarCustomizationItem
{
    public string Id;
    public AvatarCustomizationCategory Category;
    public int OptionIndex;
    public AvatarUnlockType UnlockType = AvatarUnlockType.Free;
    public int BreakPointCost;
    public int RequiredLevel = 1;
    public string DisplayName;

    [Header("Ajuste manual no avatar")]
    [FormerlySerializedAs("AvatarNormalizedOffset")]
    [Tooltip("X move para os lados e Y move para cima/baixo. Exemplo: Y = 0,05 move 5% para cima.")]
    public Vector2 DeslocamentoNoAvatar;

    [FormerlySerializedAs("AvatarScale")]
    [Tooltip("Tamanho individual deste item. 1 mantém o tamanho original.")]
    [Min(0.1f)]
    public float EscalaNoAvatar = 1f;

    [FormerlySerializedAs("AvatarRotation")]
    [Tooltip("Rotação individual deste item, em graus.")]
    public float RotacaoNoAvatar;
}
