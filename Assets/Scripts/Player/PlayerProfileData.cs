using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

[Serializable]
public sealed class PlayerProfileData
{
    /// <summary>Indica que Nome, Loja e Turno foram validados e salvos.</summary>
    public bool RegistrationCompleted;

    /// <summary>
    /// Etapa do guia inicial: 0 = Energy Station no Hub, 2 = interação na
    /// Energy Station e 3 = guia concluído ou ignorado. O valor 1 é aceito
    /// apenas para compatibilidade com perfis da antiga etapa intermediária.
    /// </summary>
    public int OnboardingStep;

    /// <summary>Nome utilizado pelas interfaces do aplicativo.</summary>
    public string DisplayName;

    /// <summary>Número, código ou nome da loja informado no cadastro.</summary>
    public string StoreId;

    /// <summary>Turno informado no cadastro.</summary>
    public string Shift;

    /// <summary>Data local da última conclusão válida do Energy Station (yyyy-MM-dd).</summary>
    public string LastEnergyStationCompletionDate;

    /// <summary>Turno associado à última conclusão válida do Energy Station.</summary>
    public string LastEnergyStationCompletionShift;

    public int Level = 1;

    /// <summary>Saldo disponível para compras de customização.</summary>
    public int BreakPoints;

    /// <summary>Total histórico recebido. Nunca diminui e define o nível do jogador.</summary>
    public int LifetimeBreakPoints;

    /// <summary>IDs dos itens comprados com Break Points. Itens de nível são calculados dinamicamente.</summary>
    public List<string> UnlockedCustomizationIds = new List<string>();

    public AvatarCustomizationData Avatar = new AvatarCustomizationData();
}

[Serializable]
public sealed class AvatarCustomizationData
{
    [FormerlySerializedAs("HairIndex")]
    public int HatIndex;

    [FormerlySerializedAs("OutfitIndex")]
    public int FaceIndex;

    [FormerlySerializedAs("AccessoryIndex")]
    public int ColorIndex;
}
