using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using PelicanLogistics.Core.Simulation;

namespace PelicanLogistics.Core.Regulators;

/// <summary>
/// Converte a regra do Regulador para JSON e de volta, para guardar no <c>modData</c> do Inseridor. Ler nunca lança
/// exceção: JSON ausente ou corrompido, ou modo inválido, dá a regra padrão; limites fora de
/// 0..<see cref="Balance.MaxRegulatorLimit"/> são corrigidos e itens com ID vazio são descartados.
/// </summary>
public static class RegulatorRuleSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(RegulatorRule rule)
    {
        if (rule == null)
            throw new ArgumentNullException(nameof(rule));

        return JsonSerializer.Serialize(rule, Options);
    }

    public static RegulatorRule Deserialize(string? json)
    {
        RegulatorRule? rule = TryDeserialize(json);
        if (rule == null || !Enum.IsDefined(rule.Mode))
            return new RegulatorRule();

        var limits = new Dictionary<string, int>();
        foreach ((string? id, int limit) in rule.PerItemLimits ?? new Dictionary<string, int>())
        {
            if (!string.IsNullOrWhiteSpace(id))
                limits[id.Trim()] = ClampLimit(limit);
        }

        return new RegulatorRule
        {
            Mode = rule.Mode,
            GeneralLimit = ClampLimit(rule.GeneralLimit),
            PerItemLimits = limits,
        };
    }

    /// <summary>Limita a 0..<see cref="Balance.MaxRegulatorLimit"/>.</summary>
    public static int ClampLimit(int limit)
    {
        return Math.Clamp(limit, 0, Balance.MaxRegulatorLimit);
    }

    private static RegulatorRule? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<RegulatorRule>(json, Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
