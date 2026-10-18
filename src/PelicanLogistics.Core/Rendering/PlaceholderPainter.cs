using System;
using PelicanLogistics.Core.Geometry;
using PelicanLogistics.Core.Nodes;

namespace PelicanLogistics.Core.Rendering;

/// <summary>
/// Pinta os placeholders da alpha (README "Placeholders visuais"). Os sprites de bloco têm 16×16 e
/// apontam para o NORTE; o adaptador gira ao desenhar.
/// </summary>
public static class PlaceholderPainter
{
    public const int SpriteSize = 16;

    /// <summary>Quadrado cinza (claro/médio/escuro conforme o tier) com 1, 2 ou 3 setas brancas para cima.</summary>
    public static PixelBuffer PaintBelt(BeltTier tier)
    {
        (Rgba color, int[] arrowRows) = tier switch
        {
            BeltTier.Basic => (PlaceholderColors.BeltBasic, new[] { 8 }),
            BeltTier.Fast => (PlaceholderColors.BeltFast, new[] { 5, 11 }),
            BeltTier.Express => (PlaceholderColors.BeltExpress, new[] { 3, 8, 13 }),
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Tier de esteira inválido."),
        };

        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillRect(0, 0, SpriteSize, SpriteSize, color);
        buffer.DrawRect(0, 0, SpriteSize, SpriteSize, PlaceholderColors.Outline);
        foreach (int row in arrowRows)
            buffer.DrawArrow(SpriteSize / 2, row, Direction.North, PlaceholderColors.Mark);
        return buffer;
    }

    /// <summary>
    /// Esteira em curva, apontando para o NORTE e recebendo pela lateral: oeste se <paramref name="fromLeft"/>, senão
    /// leste. Mesmo quadrado cinza da reta, com um traço escuro em L da lateral ao centro e do centro à borda de cima,
    /// e 1, 2 ou 3 setas brancas ao longo do caminho (a última sempre para cima).
    /// </summary>
    public static PixelBuffer PaintBeltCurve(BeltTier tier, bool fromLeft)
    {
        (Rgba color, (int X, int Y, Direction Dir)[] arrows) = tier switch
        {
            BeltTier.Basic => (PlaceholderColors.BeltBasic, new[] { (8, 4, Direction.North) }),
            BeltTier.Fast => (PlaceholderColors.BeltFast, new[] { (4, 8, Direction.East), (8, 4, Direction.North) }),
            BeltTier.Express => (PlaceholderColors.BeltExpress, new[] { (3, 8, Direction.East), (6, 8, Direction.East), (8, 4, Direction.North) }),
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Tier de esteira inválido."),
        };

        // A perna vertical fica na coluna 8, como a seta da reta; a horizontal vai até a borda da entrada.
        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillRect(0, 0, SpriteSize, SpriteSize, color);
        buffer.DrawRect(0, 0, SpriteSize, SpriteSize, PlaceholderColors.Outline);
        buffer.FillRect(fromLeft ? 1 : 8, 8, fromLeft ? 8 : SpriteSize - 9, 1, PlaceholderColors.Outline);
        buffer.FillRect(8, 1, 1, 8, PlaceholderColors.Outline);
        foreach ((int x, int y, Direction dir) in arrows)
        {
            // Pela direita, as setas da perna horizontal ficam espelhadas em torno da coluna 8 e apontam para o oeste.
            if (fromLeft || dir == Direction.North)
                buffer.DrawArrow(x, y, dir, PlaceholderColors.Mark);
            else
                buffer.DrawArrow(SpriteSize - x, y, Direction.West, PlaceholderColors.Mark);
        }

        return buffer;
    }

    /// <summary>
    /// Quadrado azul com um triângulo pequeno na borda de cima (a frente, o destino) e 2 ou 3 pontos no canto para
    /// Pilha/Configurável. O braço não faz parte do sprite: o adaptador desenha por cima.
    /// </summary>
    public static PixelBuffer PaintInserter(TransferTier tier)
    {
        int dots = tier switch
        {
            TransferTier.Single => 0,
            TransferTier.Stack => 2,
            TransferTier.Configurable => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Tier inválido."),
        };

        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillRect(0, 0, SpriteSize, SpriteSize, PlaceholderColors.Inserter);
        buffer.DrawRect(0, 0, SpriteSize, SpriteSize, PlaceholderColors.Outline);
        buffer.FillTriangle(8, 1, 4, 5, 12, 5, PlaceholderColors.Outline);

        // Pontos 2×2 no canto superior esquerdo, longe do triângulo.
        int[][] dotPositions = { new[] { 2, 2 }, new[] { 2, 5 }, new[] { 2, 8 } };
        for (int i = 0; i < dots; i++)
            buffer.FillRect(dotPositions[i][0], dotPositions[i][1], 2, 2, PlaceholderColors.Mark);
        return buffer;
    }

    /// <summary>
    /// Filtro: base cinza de esteira com um losango verde e uma seta branca para cima. O Filtro Completo é verde escuro
    /// com um losango duplo (um contorno escuro dentro). O ícone do item do Filtro Comum é desenhado pelo adaptador.
    /// </summary>
    public static PixelBuffer PaintFilter(bool complete)
    {
        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillRect(0, 0, SpriteSize, SpriteSize, PlaceholderColors.BeltBasic);
        buffer.DrawRect(0, 0, SpriteSize, SpriteSize, PlaceholderColors.Outline);

        Rgba green = complete ? PlaceholderColors.FilterComplete : PlaceholderColors.FilterCommon;
        buffer.FillDiamond(8, 8, 6, green);
        if (complete)
        {
            buffer.FillDiamond(8, 8, 4, PlaceholderColors.Outline);
            buffer.FillDiamond(8, 8, 3, green);
        }

        buffer.DrawArrow(8, 4, Direction.North, PlaceholderColors.Mark);
        return buffer;
    }

    /// <summary>Divisor: base cinza de esteira com um círculo roxo e 4 saídas (uma faixa roxa até o meio de cada borda).</summary>
    public static PixelBuffer PaintSplitter()
    {
        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillRect(0, 0, SpriteSize, SpriteSize, PlaceholderColors.BeltBasic);
        buffer.DrawRect(0, 0, SpriteSize, SpriteSize, PlaceholderColors.Outline);

        buffer.FillRect(6, 1, 4, SpriteSize - 2, PlaceholderColors.Splitter);
        buffer.FillRect(1, 6, SpriteSize - 2, 4, PlaceholderColors.Splitter);
        buffer.FillCircle(8, 8, 5.5f, PlaceholderColors.Splitter);
        buffer.FillRect(7, 7, 2, 2, PlaceholderColors.Mark);
        return buffer;
    }

    /// <summary>Seta grande branca apontando para cima, em fundo transparente (seta fantasma de colocação).</summary>
    public static PixelBuffer PaintGhostArrow()
    {
        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillTriangle(8, 1, 2, 8, 14, 8, PlaceholderColors.Mark);
        buffer.FillRect(6, 8, 4, 7, PlaceholderColors.Mark);
        return buffer;
    }

    /// <summary>Ícone de Big Craftable (16×32) com o sprite centralizado na metade de baixo.</summary>
    public static PixelBuffer PaintIcon(PixelBuffer sprite16)
    {
        var icon = new PixelBuffer(SpriteSize, SpriteSize * 2);
        icon.Blit(sprite16, (SpriteSize - sprite16.Width) / 2, SpriteSize + ((SpriteSize - sprite16.Height) / 2));
        return icon;
    }
}
