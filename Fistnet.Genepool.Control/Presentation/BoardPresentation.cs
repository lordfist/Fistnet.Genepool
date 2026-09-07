#nullable enable
using System;
using System.Collections.Generic;

namespace Fistnet.Genepool.Control.Presentation;

public enum BoardLayer { Combined, Organisms, Food }

/// <summary>Engine-independent presentation rules over detached observation values.</summary>
public static class BoardPresentation
{
    public const int InstanceStride = 12;
    public const int OrganismsBackgroundArgb = unchecked((int)0xff0e151f);
    public const int BlackOrganismFallbackArgb = unchecked((int)0xff5f7085);

    public static int FoodArgb(int food)
    {
        float t = Math.Clamp(food, 0, 10) / 10f;
        return unchecked((int)0xff000000) | (int)(17 + 99 * t) << 16 |
            (int)(28 + 160 * t) << 8 | (int)(37 + 99 * t);
    }

    public static int OrganismArgb(int colorArgb, string? pattern, string? highlightedPattern)
    {
        int color = colorArgb | unchecked((int)0xff000000);
        if ((color & 0xffffff) == 0) color = BlackOrganismFallbackArgb;
        if (highlightedPattern == null || pattern == highlightedPattern) return color;
        int red = ((color >> 16) & 255) / 4 + 16;
        int green = ((color >> 8) & 255) / 4 + 16;
        int blue = (color & 255) / 4 + 20;
        return unchecked((int)0xff000000) | red << 16 | green << 8 | blue;
    }

    /// <summary>Godot 2D MultiMesh layout: two four-float transform rows, then RGBA.</summary>
    public static void WriteInstance(Span<float> buffer, int instance, float centerX, float centerY,
        float side, int colorArgb)
    {
        Span<float> item = buffer.Slice(checked(instance * InstanceStride), InstanceStride);
        item[0] = side; item[1] = 0; item[2] = 0; item[3] = centerX;
        item[4] = 0; item[5] = side; item[6] = 0; item[7] = centerY;
        item[8] = ((colorArgb >> 16) & 255) / 255f;
        item[9] = ((colorArgb >> 8) & 255) / 255f;
        item[10] = (colorArgb & 255) / 255f;
        item[11] = 1;
    }

    public static bool ContainsCell(int x, int y, int width, int height) =>
        x >= 0 && y >= 0 && x < width && y < height;

    /// <summary>Only successful observed movement has a trail; attempted targets do not.</summary>
    public static IEnumerable<ViewAction> MovementTrail(ViewOrganism selected, int width, int height)
    {
        int skip = 0, movementCount = 0;
        foreach (ViewAction action in selected.Trace)
            if (IsMovement(action, width, height)) movementCount++;
        skip = Math.Max(0, movementCount - 16);
        foreach (ViewAction action in selected.Trace)
        {
            if (!IsMovement(action, width, height)) continue;
            if (skip > 0) { skip--; continue; }
            yield return action;
        }
    }

    private static bool IsMovement(ViewAction action, int width, int height) => action.Moved &&
        (action.SourceX != action.DestinationX || action.SourceY != action.DestinationY) &&
        ContainsCell(action.SourceX, action.SourceY, width, height) &&
        ContainsCell(action.DestinationX, action.DestinationY, width, height);
}

/// <summary>Uniform board projection in viewport pixels. Right/bottom edges are exclusive.</summary>
public readonly record struct BoardProjection(float ViewportWidth, float ViewportHeight,
    int BoardWidth, int BoardHeight, float Zoom = 1, float PanX = 0, float PanY = 0)
{
    public float PixelsPerCell => BoardWidth <= 0 || BoardHeight <= 0 ? 0 :
        Math.Max(.001f, Math.Min(Math.Max(1, ViewportWidth - 28) / BoardWidth,
            Math.Max(1, ViewportHeight - 28) / BoardHeight)) * Zoom;
    public float Left => (ViewportWidth - BoardWidth * PixelsPerCell) / 2 + PanX;
    public float Top => (ViewportHeight - BoardHeight * PixelsPerCell) / 2 + PanY;

    public (int X, int Y)? CellAt(float screenX, float screenY)
    {
        float scale = PixelsPerCell;
        if (!float.IsFinite(screenX) || !float.IsFinite(screenY) || !float.IsFinite(scale) || scale <= 0 ||
            screenX < 0 || screenY < 0 || screenX >= ViewportWidth || screenY >= ViewportHeight)
            return null;
        float x = (screenX - Left) / scale, y = (screenY - Top) / scale;
        if (x < 0 || y < 0 || x >= BoardWidth || y >= BoardHeight) return null;
        return ((int)Math.Floor(x), (int)Math.Floor(y));
    }

    public (float X, float Y) CellCenter(int x, int y) =>
        (Left + (x + .5f) * PixelsPerCell, Top + (y + .5f) * PixelsPerCell);

    public (float X, float Y) ClampPan(float x, float y)
    {
        if (Zoom <= 1) return (0, 0);
        float maxX = Math.Max(0, (BoardWidth * PixelsPerCell - ViewportWidth) / 2 + 14);
        float maxY = Math.Max(0, (BoardHeight * PixelsPerCell - ViewportHeight) / 2 + 14);
        return (Math.Clamp(x, -maxX, maxX), Math.Clamp(y, -maxY, maxY));
    }
}
