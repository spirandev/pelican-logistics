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

    /// <summary>Lado de um quadro de chapéu no jogo (<c>Hat.widthOfTileSheetSquare</c>).</summary>
    public const int HatSize = 20;

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

        return PaintStraightConveyor(color, arrowRows);
    }

    /// <summary>
    /// Esteira Subterrânea reta: quadrado cinza escuro com 2 setas brancas para cima (velocidade do tier 2). O adaptador
    /// desenha semitransparente, só com o Capacete de Engenheiro.
    /// </summary>
    public static PixelBuffer PaintUndergroundSegment()
    {
        return PaintStraightConveyor(PlaceholderColors.Underground, new[] { 5, 11 });
    }

    /// <summary>Esteira Subterrânea em curva, como <see cref="PaintBeltCurve"/> do tier 2, em cinza escuro.</summary>
    public static PixelBuffer PaintUndergroundSegmentCurve(bool fromLeft)
    {
        return PaintCurvedConveyor(PlaceholderColors.Underground, new[] { (4, 8, Direction.East), (8, 4, Direction.North) }, fromLeft);
    }

    private static PixelBuffer PaintStraightConveyor(Rgba color, int[] arrowRows)
    {
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

        return PaintCurvedConveyor(color, arrows, fromLeft);
    }

    /// <summary>Curva apontando para o NORTE, recebendo pelo oeste (fromLeft) ou pelo leste, com as setas indicadas (na versão "pela esquerda").</summary>
    private static PixelBuffer PaintCurvedConveyor(Rgba color, (int X, int Y, Direction Dir)[] arrows, bool fromLeft)
    {
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

    /// <summary>
    /// Túnel: quadrado cinza escuro com uma seta branca pequena na borda de cima (o sentido) e um círculo branco (entrada)
    /// ou preto (saída) com contorno.
    /// </summary>
    public static PixelBuffer PaintTunnel(TunnelRole role)
    {
        Rgba circle = role switch
        {
            TunnelRole.Entrance => PlaceholderColors.Mark,
            TunnelRole.Exit => PlaceholderColors.TunnelExit,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Papel inválido."),
        };

        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillRect(0, 0, SpriteSize, SpriteSize, PlaceholderColors.Underground);
        buffer.DrawRect(0, 0, SpriteSize, SpriteSize, PlaceholderColors.Outline);
        buffer.DrawArrow(8, 2, Direction.North, PlaceholderColors.Mark);
        buffer.FillCircle(8.5f, 9f, 5f, PlaceholderColors.Outline);
        buffer.FillCircle(8.5f, 9f, 4f, circle);
        return buffer;
    }

    /// <summary>
    /// Ponte: faixa horizontal marrom (linhas 4 a 11) com juntas entre as tábuas, em fundo transparente. Desenhada por
    /// cima da esteira e do item, sem girar.
    /// </summary>
    public static PixelBuffer PaintBridge()
    {
        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillRect(0, 4, SpriteSize, 8, PlaceholderColors.Bridge);
        buffer.FillRect(0, 4, SpriteSize, 1, PlaceholderColors.BridgeJoint);
        buffer.FillRect(0, 11, SpriteSize, 1, PlaceholderColors.BridgeJoint);
        for (int x = 3; x < SpriteSize; x += 4)
            buffer.FillRect(x, 5, 1, 6, PlaceholderColors.BridgeJoint);
        return buffer;
    }

    /// <summary>Regulador sobre o Inseridor: quadrado amarelo pequeno (6×6, com contorno) no canto inferior direito, em fundo transparente.</summary>
    public static PixelBuffer PaintRegulator()
    {
        var buffer = new PixelBuffer(SpriteSize, SpriteSize);
        buffer.FillRect(9, 9, 6, 6, PlaceholderColors.Regulator);
        buffer.DrawRect(9, 9, 6, 6, PlaceholderColors.Outline);
        return buffer;
    }

    /// <summary>
    /// Ícone de inventário (16×16) de um acessório: a Ponte é a mesma faixa desenhada sobre a esteira; o Regulador, um
    /// quadrado amarelo grande com contorno e um traço de "limite" no meio.
    /// </summary>
    public static PixelBuffer PaintAccessoryIcon(AccessoryKind kind)
    {
        switch (kind)
        {
            case AccessoryKind.Bridge:
                return PaintBridge();

            case AccessoryKind.Regulator:
            {
                var buffer = new PixelBuffer(SpriteSize, SpriteSize);
                buffer.FillRect(2, 2, 12, 12, PlaceholderColors.Regulator);
                buffer.DrawRect(2, 2, 12, 12, PlaceholderColors.Outline);
                buffer.FillRect(5, 7, 6, 2, PlaceholderColors.Outline);
                return buffer;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Acessório sem ícone.");
        }
    }

    /// <summary>
    /// Capacete de Engenheiro no formato de chapéu do jogo: 20×80, quatro quadros de 20×20, de cima para baixo virado para
    /// baixo, para a direita, para a esquerda e para cima (a ordem do <c>FarmerRenderer</c>). Capacete amarelo com aba
    /// escura; a lanterna branca fica na frente (de frente, no centro; de lado, na ponta do lado para onde olha; de costas,
    /// não aparece).
    /// </summary>
    public static PixelBuffer PaintEngineerHelmet()
    {
        var sheet = new PixelBuffer(HatSize, HatSize * 4);
        for (int frame = 0; frame < 4; frame++)
        {
            int top = frame * HatSize;

            // Cúpula (linhas 4 a 10) e aba (linha 11).
            sheet.FillCircle(10f, top + 10f, 6f, PlaceholderColors.Helmet);
            sheet.FillRect(3, top + 10, 14, 6, Rgba.Transparent);
            sheet.FillRect(3, top + 10, 14, 2, PlaceholderColors.HelmetShade);

            // Lanterna: de frente (0), para a direita (1), para a esquerda (2); de costas (3), nada.
            int? lampX = frame switch
            {
                0 => 9,
                1 => 13,
                2 => 5,
                _ => null,
            };
            if (lampX is { } x)
            {
                sheet.FillRect(x, top + 6, 2, 3, PlaceholderColors.Mark);
                sheet.DrawRect(x - 1, top + 5, 4, 5, PlaceholderColors.Outline);
            }
        }

        return sheet;
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
