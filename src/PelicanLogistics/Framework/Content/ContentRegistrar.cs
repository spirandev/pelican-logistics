using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.BigCraftables;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;

namespace PelicanLogistics.Framework.Content;

/// <summary>
/// Registra os itens do mod (blocos em <c>Data/BigCraftables</c>, acessórios em <c>Data/Objects</c>), as folhas de ícones
/// e a loja do Clint (<c>Data/Shops</c>).
/// </summary>
internal sealed class ContentRegistrar
{
    /// <summary>ID da loja do Clint em <c>Data/Shops</c>.</summary>
    private const string BlacksmithShopId = "Blacksmith";

    private readonly IModHelper helper;
    private readonly ModConfig config;

    public ContentRegistrar(IModHelper helper, ModConfig config)
    {
        this.helper = helper;
        this.config = config;
    }

    public void Register()
    {
        this.helper.Events.Content.AssetRequested += this.OnAssetRequested;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(PlaceholderTextures.IconSheetAssetName))
            e.LoadFrom(() => PlaceholderTextures.BuildIconSheet(), AssetLoadPriority.Exclusive);
        else if (e.NameWithoutLocale.IsEquivalentTo(PlaceholderTextures.ObjectSheetAssetName))
            e.LoadFrom(() => PlaceholderTextures.BuildObjectSheet(), AssetLoadPriority.Exclusive);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
            e.Edit(this.EditBigCraftables);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            e.Edit(this.EditObjects);
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops") && this.config.UnlockAll)
            e.Edit(this.EditShops);
    }

    private void EditBigCraftables(IAssetData asset)
    {
        IDictionary<string, BigCraftableData> data = asset.AsDictionary<string, BigCraftableData>().Data;
        foreach (string id in ItemIds.AllBlocks)
        {
            string key = ItemIds.TranslationKeyOf(id);
            data[id] = new BigCraftableData
            {
                Name = id,
                DisplayName = this.helper.Translation.Get($"item.{key}.name").ToString(),
                Description = this.helper.Translation.Get($"item.{key}.description").ToString(),
                Price = 0,
                Fragility = 0,
                CanBePlacedIndoors = true,
                CanBePlacedOutdoors = true,
                IsLamp = false,
                Texture = PlaceholderTextures.IconSheetAssetName,
                SpriteIndex = PlaceholderTextures.SpriteIndexOf(id),
            };
        }
    }

    /// <summary>
    /// Acessórios (Ponte e Regulador): objetos comuns que o jogo não coloca no mapa. <c>Object.isPlaceable</c> só aceita
    /// categorias -8, -9, -74, -19, o tipo "Crafting" e alguns IDs; "Basic" com categoria 0 não é colocável. A tag
    /// <c>not_placeable</c> garante isso mesmo se as regras do jogo mudarem. O mod aplica o acessório pelo clique.
    /// </summary>
    private void EditObjects(IAssetData asset)
    {
        IDictionary<string, ObjectData> data = asset.AsDictionary<string, ObjectData>().Data;
        foreach (string id in ItemIds.AllAccessories)
        {
            string key = ItemIds.TranslationKeyOf(id);
            data[id] = new ObjectData
            {
                Name = id,
                DisplayName = this.helper.Translation.Get($"item.{key}.name").ToString(),
                Description = this.helper.Translation.Get($"item.{key}.description").ToString(),
                Type = "Basic",
                Category = 0,
                Price = 0,
                Texture = PlaceholderTextures.ObjectSheetAssetName,
                SpriteIndex = PlaceholderTextures.ObjectSpriteIndexOf(id),
                CanBeGivenAsGift = false,
                ExcludeFromRandomSale = true,
                ExcludeFromShippingCollection = true,
                ContextTags = new List<string> { "not_placeable" },
            };
        }
    }

    /// <summary>Alpha: o Clint vende todos os itens por 0g, com estoque ilimitado (plano mestre D15).</summary>
    private void EditShops(IAssetData asset)
    {
        IDictionary<string, ShopData> shops = asset.AsDictionary<string, ShopData>().Data;
        if (!shops.TryGetValue(BlacksmithShopId, out ShopData? shop))
            return;

        shop.Items ??= new List<ShopItemData>();
        foreach (string id in ItemIds.AllBlocks)
            shop.Items.Add(FreeItem(id, ItemIds.Qualified(id)));
        foreach (string id in ItemIds.AllAccessories)
            shop.Items.Add(FreeItem(id, ItemIds.QualifiedAccessory(id)));
    }

    /// <summary>Item da loja por 0g, com estoque ilimitado.</summary>
    private static ShopItemData FreeItem(string id, string qualifiedItemId)
    {
        return new ShopItemData
        {
            Id = id,
            ItemId = qualifiedItemId,
            Price = 0,
            AvailableStock = -1,
        };
    }
}
