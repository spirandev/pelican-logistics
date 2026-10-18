using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace PelicanLogistics.Framework.Menus;

/// <summary>
/// Lista de até <see cref="MaxItems"/> itens fantasma (Filtro Completo): clicar num item do inventário adiciona (sem
/// tirá-lo do inventário); clicar num item da lista remove.
/// </summary>
internal sealed class ItemListRow : InventoryRow
{
    public const int MaxItems = 12;

    /// <summary>Lado dos slots da lista (menores que os do inventário, para caber o rótulo na mesma linha).</summary>
    private const int SlotSize = 52;

    private const int Gap = 16;

    private readonly string label;
    private readonly Action<IReadOnlyList<string>> onChanged;
    private readonly List<string> ids;
    private readonly List<Item> items;
    private readonly Rectangle[] slots = new Rectangle[MaxItems];

    /// <param name="label">Rótulo à esquerda da lista.</param>
    /// <param name="currentIds">IDs qualificados atuais.</param>
    /// <param name="onChanged">Chamado a cada mudança, com a lista nova.</param>
    public ItemListRow(string label, IEnumerable<string> currentIds, Action<IReadOnlyList<string>> onChanged)
    {
        this.label = label;
        this.onChanged = onChanged;
        this.ids = currentIds.Distinct().Take(MaxItems).ToList();
        this.items = this.ids.Select(GhostSlot.Create).ToList();
    }

    protected override int TopHeight => SlotSize;

    public override int MinWidth => Math.Max(base.MinWidth, this.LabelWidth + Gap + (MaxItems * SlotSize));

    private int LabelWidth => (int)Game1.smallFont.MeasureString(this.label).X;

    protected override void DrawTop(SpriteBatch b)
    {
        Rectangle top = this.TopBounds;
        float textY = top.Y + ((top.Height - Game1.smallFont.LineSpacing) / 2f);
        Utility.drawTextWithShadow(b, this.label, Game1.smallFont, new Vector2(top.X, textY), Game1.textColor);

        for (int i = 0; i < MaxItems; i++)
            GhostSlot.Draw(b, this.slots[i], i < this.items.Count ? this.items[i] : null);
    }

    protected override bool ReceiveTopClick(int x, int y)
    {
        int index = Array.FindIndex(this.slots, s => s.Contains(x, y));
        if (index < 0 || index >= this.ids.Count)
            return false;

        this.ids.RemoveAt(index);
        this.items.RemoveAt(index);
        Game1.playSound("trashcan");
        this.onChanged(this.ids.ToList());
        return true;
    }

    protected override Item? GetTopHoveredItem(int x, int y)
    {
        int index = Array.FindIndex(this.slots, s => s.Contains(x, y));
        return index >= 0 && index < this.items.Count ? this.items[index] : null;
    }

    protected override void OnInventoryItemClicked(Item item)
    {
        if (this.ids.Contains(item.QualifiedItemId) || this.ids.Count >= MaxItems)
        {
            Game1.playSound("cancel");
            return;
        }

        this.ids.Add(item.QualifiedItemId);
        this.items.Add(GhostSlot.Create(item.QualifiedItemId));
        Game1.playSound("smallSelect");
        this.onChanged(this.ids.ToList());
    }

    /// <summary>Rótulo à esquerda e os slots alinhados à direita.</summary>
    protected override void OnBoundsChanged()
    {
        base.OnBoundsChanged();

        Rectangle top = this.TopBounds;
        int left = top.Right - (MaxItems * SlotSize);
        for (int i = 0; i < MaxItems; i++)
            this.slots[i] = new Rectangle(left + (i * SlotSize), top.Y, SlotSize, SlotSize);
    }
}
