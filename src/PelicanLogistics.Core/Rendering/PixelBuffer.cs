using System;
using PelicanLogistics.Core.Geometry;

namespace PelicanLogistics.Core.Rendering;

/// <summary>Imagem simples em memória, usada para gerar os placeholders. Origem no canto superior esquerdo.</summary>
public sealed class PixelBuffer
{
    public PixelBuffer(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));

        this.Width = width;
        this.Height = height;
        this.Pixels = new Rgba[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Pixels linha a linha (índice = y * Width + x). Começa todo transparente.</summary>
    public Rgba[] Pixels { get; }

    public Rgba Get(int x, int y)
    {
        return this.Contains(x, y) ? this.Pixels[(y * this.Width) + x] : Rgba.Transparent;
    }

    /// <summary>Pinta um pixel. Coordenadas fora da imagem são ignoradas.</summary>
    public void Set(int x, int y, Rgba c)
    {
        if (this.Contains(x, y))
            this.Pixels[(y * this.Width) + x] = c;
    }

    public void FillRect(int x, int y, int width, int height, Rgba c)
    {
        for (int py = y; py < y + height; py++)
        {
            for (int px = x; px < x + width; px++)
                this.Set(px, py, c);
        }
    }

    /// <summary>Contorno de 1 pixel de um retângulo.</summary>
    public void DrawRect(int x, int y, int width, int height, Rgba c)
    {
        this.FillRect(x, y, width, 1, c);
        this.FillRect(x, y + height - 1, width, 1, c);
        this.FillRect(x, y, 1, height, c);
        this.FillRect(x + width - 1, y, 1, height, c);
    }

    /// <summary>Preenche os pixels cujo centro está dentro do triângulo (vértices em coordenadas de pixel).</summary>
    public void FillTriangle(float x0, float y0, float x1, float y1, float x2, float y2, Rgba c)
    {
        int minX = (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2)));
        int maxX = (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2)));
        int minY = (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2)));
        int maxY = (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2)));

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                float cx = px + 0.5f;
                float cy = py + 0.5f;
                float d0 = Edge(x0, y0, x1, y1, cx, cy);
                float d1 = Edge(x1, y1, x2, y2, cx, cy);
                float d2 = Edge(x2, y2, x0, y0, cx, cy);
                bool hasNegative = d0 < 0 || d1 < 0 || d2 < 0;
                bool hasPositive = d0 > 0 || d1 > 0 || d2 > 0;
                if (!(hasNegative && hasPositive))
                    this.Set(px, py, c);
            }
        }
    }

    /// <summary>Losango centrado em (cx, cy): pixels com |dx| + |dy| &lt;= radius.</summary>
    public void FillDiamond(int cx, int cy, int radius, Rgba c)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            int span = radius - Math.Abs(dy);
            for (int dx = -span; dx <= span; dx++)
                this.Set(cx + dx, cy + dy, c);
        }
    }

    /// <summary>Círculo centrado em (cx, cy), em coordenadas de pixel (use x,5 para o centro de um pixel).</summary>
    public void FillCircle(float cx, float cy, float radius, Rgba c)
    {
        int minX = (int)Math.Floor(cx - radius);
        int maxX = (int)Math.Ceiling(cx + radius);
        int minY = (int)Math.Floor(cy - radius);
        int maxY = (int)Math.Ceiling(cy + radius);
        float r2 = radius * radius;

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                float dx = px + 0.5f - cx;
                float dy = py + 0.5f - cy;
                if ((dx * dx) + (dy * dy) <= r2)
                    this.Set(px, py, c);
            }
        }
    }

    /// <summary>
    /// Seta pequena (ponta de 5×3 pixels) centrada no pixel (cx, cy), apontando para 'dir'.
    /// </summary>
    public void DrawArrow(int cx, int cy, Direction dir, Rgba c)
    {
        // Desenhada apontando para o norte e girada: linhas de largura 1, 3 e 5.
        for (int row = -1; row <= 1; row++)
        {
            int half = row + 1;
            for (int col = -half; col <= half; col++)
            {
                (int dx, int dy) = RotateFromNorth(col, row, dir);
                this.Set(cx + dx, cy + dy, c);
            }
        }
    }

    /// <summary>Copia 'src' para esta imagem na posição (dx, dy), ignorando pixels transparentes.</summary>
    public void Blit(PixelBuffer src, int dx, int dy)
    {
        for (int y = 0; y < src.Height; y++)
        {
            for (int x = 0; x < src.Width; x++)
            {
                Rgba c = src.Get(x, y);
                if (!c.IsTransparent)
                    this.Set(dx + x, dy + y, c);
            }
        }
    }

    /// <summary>Quantidade de pixels não transparentes.</summary>
    public int CountOpaque()
    {
        int count = 0;
        foreach (Rgba c in this.Pixels)
        {
            if (!c.IsTransparent)
                count++;
        }

        return count;
    }

    private bool Contains(int x, int y)
    {
        return x >= 0 && y >= 0 && x < this.Width && y < this.Height;
    }

    private static float Edge(float ax, float ay, float bx, float by, float px, float py)
    {
        return ((bx - ax) * (py - ay)) - ((by - ay) * (px - ax));
    }

    /// <summary>Gira um deslocamento desenhado para o norte até a direção indicada (sentido horário, Y para baixo).</summary>
    private static (int Dx, int Dy) RotateFromNorth(int dx, int dy, Direction dir)
    {
        return dir switch
        {
            Direction.North => (dx, dy),
            Direction.East => (-dy, dx),
            Direction.South => (-dx, -dy),
            Direction.West => (dy, -dx),
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, "Direção inválida."),
        };
    }
}
