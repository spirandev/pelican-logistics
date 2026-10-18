using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PelicanLogistics.Core.Filters;

/// <summary>
/// Converte as regras dos filtros para JSON e de volta, para guardar no <c>modData</c> do bloco. Ler nunca lança
/// exceção: JSON ausente ou corrompido, ou modo inválido, dá a regra vazia; valores inválidos dentro das listas
/// (nulos, vazios, qualidade desconhecida) são descartados.
/// </summary>
public static class FilterRuleSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(IFilterRule rule)
    {
        return rule switch
        {
            CommonFilterRule common => JsonSerializer.Serialize(common, Options),
            CompleteFilterRule complete => JsonSerializer.Serialize(complete, Options),
            null => throw new ArgumentNullException(nameof(rule)),
            _ => throw new ArgumentException($"Regra de filtro desconhecida: {rule.GetType().Name}.", nameof(rule)),
        };
    }

    public static CommonFilterRule DeserializeCommon(string? json)
    {
        CommonFilterRule? rule = TryDeserialize<CommonFilterRule>(json);
        if (rule == null || string.IsNullOrWhiteSpace(rule.QualifiedItemId))
            return new CommonFilterRule();

        return rule;
    }

    public static CompleteFilterRule DeserializeComplete(string? json)
    {
        CompleteFilterRule? rule = TryDeserialize<CompleteFilterRule>(json);
        if (rule == null || !Enum.IsDefined(rule.Mode))
            return new CompleteFilterRule();

        return new CompleteFilterRule
        {
            Mode = rule.Mode,
            ItemIds = CleanStrings(rule.ItemIds),
            Categories = rule.Categories?.ToList() ?? new List<int>(),
            Qualities = rule.Qualities?.Where(CompleteFilterRule.ValidQualities.Contains).ToList() ?? new List<int>(),
            ContextTags = CleanStrings(rule.ContextTags),
        };
    }

    private static T? TryDeserialize<T>(string? json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Sem nulos nem vazios, sem espaços nas pontas.</summary>
    private static List<string> CleanStrings(List<string>? values)
    {
        return values?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList() ?? new List<string>();
    }
}
