using System.Collections.Generic;
using SObject = StardewValley.Object;

namespace PelicanLogistics.Framework.Menus;

/// <summary>Categorias do jogo que o Filtro Completo oferece, na ordem da interface, com a chave do nome no i18n.</summary>
internal static class FilterCategories
{
    public static readonly IReadOnlyList<(int Category, string TranslationKey)> All = new[]
    {
        (SObject.VegetableCategory, "category.vegetables"),
        (SObject.FruitsCategory, "category.fruits"),
        (SObject.flowersCategory, "category.flowers"),
        (SObject.GreensCategory, "category.forage"),
        (SObject.SeedsCategory, "category.seeds"),
        (SObject.FishCategory, "category.fish"),
        (SObject.baitCategory, "category.bait"),
        (SObject.junkCategory, "category.trash"),
        (SObject.mineralsCategory, "category.minerals"),
        (SObject.GemCategory, "category.gems"),
        (SObject.metalResources, "category.ores-and-bars"),
        (SObject.buildingResources, "category.resources"),
        (SObject.artisanGoodsCategory, "category.artisan"),
        (SObject.EggCategory, "category.eggs"),
        (SObject.MilkCategory, "category.milk"),
        (SObject.CookingCategory, "category.cooking"),
        (SObject.fertilizerCategory, "category.fertilizers"),
    };
}
