using System;
using System.Collections.Generic;

[Serializable]
public sealed class PlayerProfileData
{
    /// <summary>Indica que Nome, Loja e Turno foram validados e salvos.</summary>
    public bool RegistrationCompleted;

    /// <summary>Nome utilizado pelas interfaces do aplicativo.</summary>
    public string DisplayName;

    /// <summary>Número, código ou nome da loja informado no cadastro.</summary>
    public string StoreId;

    /// <summary>Turno informado no cadastro.</summary>
    public string Shift;

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
    public int BodyIndex;
    public int HairIndex;
    public int OutfitIndex;
    public int AccessoryIndex;
}
