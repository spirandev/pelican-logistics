using PelicanLogistics.Core.Endpoints;
using PelicanLogistics.Core.Items;

namespace PelicanLogistics.Core.Tests.Fakes;

/// <summary>
/// Máquina para testes: regras de entrada principal, requisitos adicionais, tempo de processamento em ticks
/// (avançado por <see cref="Advance"/>, chamado pelo <see cref="GridBuilder"/> a cada tick) e produto gerado.
/// </summary>
public sealed class FakeMachine : IMachineEndpoint
{
    private readonly List<(Func<ILogisticsItem, bool> Matches, int Count)> mainRules = new();
    private readonly List<AdditionalRequirement> additional = new();
    private readonly Func<ILogisticsItem, FakeItem> produce;
    private FakeItem? produced;
    private int remainingTicks;

    /// <param name="produce">Produto gerado a partir da entrada principal.</param>
    /// <param name="processingTicks">Ticks de processamento depois de iniciar.</param>
    /// <param name="repeating">Repete sozinha depois de carregada (Cristalário).</param>
    public FakeMachine(Func<ILogisticsItem, FakeItem> produce, int processingTicks = 100, bool repeating = false)
    {
        this.produce = produce;
        this.ProcessingTicks = processingTicks;
        this.IsRepeating = repeating;
    }

    public int ProcessingTicks { get; }

    /// <summary>Produto pronto.</summary>
    public FakeItem? Output { get; set; }

    /// <summary>Processando (ainda sem produto).</summary>
    public bool Processing { get; private set; }

    /// <summary>Entrada carregada (máquinas repetidoras).</summary>
    public FakeItem? LoadedInput { get; private set; }

    /// <summary>Cada chamada de <see cref="TryStart"/> que iniciou: a entrada principal e os adicionais, como (id, quantidade).</summary>
    public List<(string Id, int Count, (string Id, int Count)[] Additional)> Starts { get; } = new();

    /// <summary>Unidades consumidas (principal e adicionais) e produzidas, para conferir a conservação dos itens.</summary>
    public int Consumed { get; private set; }

    public int Produced { get; private set; }

    public IReadOnlyList<AdditionalRequirement> AdditionalRequirements => this.additional;

    public bool IsIdle => this.Output == null && !this.Processing;

    public bool IsRepeating { get; }

    public bool HasLoadedInput => this.LoadedInput != null;

    /// <summary>Fornalha: 5 minérios de cobre (ou de ferro) + 1 carvão → 1 barra.</summary>
    public static FakeMachine Furnace(int processingTicks = 100)
    {
        return new FakeMachine(main => new FakeItem(main.QualifiedItemId == Ids.CopperOre ? Ids.CopperBar : Ids.IronBar, 1), processingTicks)
            .WithMain(Ids.CopperOre, 5)
            .WithMain(Ids.IronOre, 5)
            .WithAdditional(Ids.Coal, 1);
    }

    /// <summary>Barril: qualquer fruta (categoria -79) ×1 → 1 vinho.</summary>
    public static FakeMachine Keg(int processingTicks = 100)
    {
        return new FakeMachine(_ => new FakeItem(Ids.Wine, 1), processingTicks)
            .WithMain(i => i.Category == Ids.FruitCategory, 1);
    }

    /// <summary>Cristalário: qualquer gema (categoria -2) ×1, repete para sempre produzindo uma cópia.</summary>
    public static FakeMachine Crystalarium(int processingTicks = 100)
    {
        return new FakeMachine(main => new FakeItem(main.QualifiedItemId, 1, category: Ids.GemCategory), processingTicks, repeating: true)
            .WithMain(i => i.Category == Ids.GemCategory, 1);
    }

    public FakeMachine WithMain(string id, int count) => this.WithMain(i => i.QualifiedItemId == id, count);

    public FakeMachine WithMain(Func<ILogisticsItem, bool> matches, int count)
    {
        this.mainRules.Add((matches, count));
        return this;
    }

    public FakeMachine WithAdditional(string id, int count)
    {
        this.additional.Add(new AdditionalRequirement(id, count, i => i.QualifiedItemId == id));
        return this;
    }

    public int? GetMainRequiredCount(ILogisticsItem item)
    {
        foreach ((Func<ILogisticsItem, bool> matches, int count) in this.mainRules)
        {
            if (matches(item))
                return count;
        }

        return null;
    }

    public ILogisticsItem? PeekOutput() => this.Output;

    public ILogisticsItem? TakeOutput(int max)
    {
        FakeItem? output = this.Output;
        if (output == null || max <= 0)
            return null;

        if (max < output.Count)
            return output.Split(max);

        // Coleta: o Cristalário recomeça com a mesma gema.
        this.Output = null;
        if (this.IsRepeating && this.LoadedInput != null)
            this.Begin();
        return output;
    }

    public bool TryStart(ILogisticsItem main, IReadOnlyList<ILogisticsItem> additional)
    {
        if (!this.IsIdle || (this.IsRepeating && this.HasLoadedInput))
            return false;

        int? required = this.GetMainRequiredCount(main);
        if (required == null || main.Count < required || additional.Count != this.additional.Count)
            return false;

        for (int i = 0; i < this.additional.Count; i++)
        {
            AdditionalRequirement req = this.additional[i];
            if (!req.Matches(additional[i]) || additional[i].Count < req.RequiredCount)
                return false;
        }

        this.Starts.Add((main.QualifiedItemId, main.Count, additional.Select(a => (a.QualifiedItemId, a.Count)).ToArray()));

        var mainItem = (FakeItem)main;
        if (this.IsRepeating)
            this.LoadedInput = new FakeItem(mainItem.QualifiedItemId, 1, mainItem.Quality, mainItem.MaxStack, mainItem.Category);
        this.produced = this.produce(main);
        mainItem.Count -= required.Value;
        this.Consumed += required.Value;
        for (int i = 0; i < this.additional.Count; i++)
        {
            ((FakeItem)additional[i]).Count -= this.additional[i].RequiredCount;
            this.Consumed += this.additional[i].RequiredCount;
        }

        this.Begin();
        return true;
    }

    /// <summary>Um tick de processamento.</summary>
    public void Advance()
    {
        if (!this.Processing)
            return;

        if (--this.remainingTicks <= 0)
            this.Finish();
    }

    private void Begin()
    {
        this.Processing = true;
        this.remainingTicks = this.ProcessingTicks;
        if (this.remainingTicks <= 0)
            this.Finish();
    }

    private void Finish()
    {
        FakeItem template = this.produced!;
        this.Output = new FakeItem(template.QualifiedItemId, template.Count, template.Quality, template.MaxStack, template.Category);
        this.Produced += template.Count;
        this.Processing = false;
    }

    /// <summary>IDs usados nos cenários (do jogo, para ficar legível).</summary>
    public static class Ids
    {
        public const string CopperOre = "(O)378";
        public const string IronOre = "(O)380";
        public const string Coal = "(O)382";
        public const string CopperBar = "(O)334";
        public const string IronBar = "(O)335";
        public const string Wine = "(O)348";
        public const string Diamond = "(O)72";
        public const int FruitCategory = -79;
        public const int GemCategory = -2;
    }
}
