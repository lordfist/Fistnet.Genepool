using Godot;

namespace Fistnet.Genepool.GodotViewer;

public partial class BoardView3D
{
    // Match the owner's front-facing trapezoid: level near/far edges and visible perspective taper.
    private const float AngledYawDegrees = 0;
    private const float AngledElevationDegrees = 34;
    private const float FitFieldOfViewDegrees = 25.5f;
    private Vector2 projectionSize = Vector2.One, projectionScale = Vector2.One;
    private Vector3 cameraRight = Vector3.Right, cameraUp = new(0, 0, -1), cameraBack = Vector3.Up;
    private float cameraDistance = 100, projectedCellSize = 1;
    private Rect2 visibleArea;
    private Rect2I visibleBounds;
    private readonly Vector2[] visiblePolygon = new Vector2[4];
    private readonly Vector2[] visibilityNormals = new Vector2[4];
    private readonly float[] visibilityLimits = new float[4];
    private IReadOnlyList<Vector2>? readOnlyVisiblePolygon;

    /// <summary>The viewport corners intersected with the ground, clockwise from screen top-left.</summary>
    public IReadOnlyList<Vector2> VisiblePolygon => readOnlyVisiblePolygon ??= Array.AsReadOnly(visiblePolygon);

    /// <summary>Projects a board X/Z point to local control pixels using the current camera.</summary>
    public Vector2 ProjectGround(Vector2 ground)
    {
        Vector3 delta = new(ground.X - center.X, 0, ground.Y - center.Y);
        float divisor = angled ? Math.Max(.0001f, cameraDistance - delta.Dot(cameraBack)) : 1;
        return projectionSize / 2 + new Vector2(delta.Dot(cameraRight) * projectionScale.X,
            -delta.Dot(cameraUp) * projectionScale.Y) / divisor;
    }

    /// <summary>Intersects the local camera ray with Y=0; no native calls or simulation state changes.</summary>
    public Vector2 GroundAt(Vector2 local)
    {
        Vector2 screen = (local - projectionSize / 2) / projectionScale;
        if (!angled) return center + new Vector2(screen.X, screen.Y);
        Vector3 direction = -cameraBack + cameraRight * screen.X - cameraUp * screen.Y;
        float distanceAlongRay = -cameraDistance * cameraBack.Y / direction.Y;
        Vector3 offset = cameraBack * cameraDistance + direction * distanceAlongRay;
        return center + new Vector2(offset.X, offset.Z);
    }

    /// <summary>The shorter projected one-cell axis at this location, including perspective depth.</summary>
    public float ProjectedCellSizeAt(Vector2 ground)
    {
        Vector2 xAxis = ProjectGround(ground + Vector2.Right * .5f) - ProjectGround(ground - Vector2.Right * .5f);
        Vector2 zAxis = ProjectGround(ground + Vector2.Down * .5f) - ProjectGround(ground - Vector2.Down * .5f);
        return Math.Max(.001f, Math.Min(xAxis.Length(), zAxis.Length()));
    }

    /// <summary>Tests the whole cell against the convex viewport footprint, including partially visible cells.</summary>
    public bool IsCellVisible(int x, int y)
    {
        if (!visibleBounds.HasPoint(new Vector2I(x, y))) return false;
        // Rectangle axes were tested by the AABB. These four edge normals complete the separating-axis test.
        for (int i = 0; i < 4; i++)
        {
            Vector2 normal = visibilityNormals[i];
            float nearest = normal.X * (normal.X >= 0 ? x : x + 1) + normal.Y * (normal.Y >= 0 ? y : y + 1);
            if (nearest > visibilityLimits[i] + .0001f) return false;
        }
        return true;
    }

    private void RefreshProjection()
    {
        if (camera == null || viewport == null) return;
        int width = frame?.Width ?? 100, height = frame?.Height ?? 100;
        projectionSize = new Vector2(Math.Max(1, Size.X), Math.Max(1, Size.Y));
        float aspect = Math.Max(1, viewport.Size.X) / (float)Math.Max(1, viewport.Size.Y);
        float nearClip, farClip;
        if (angled)
        {
            float yaw = Mathf.DegToRad(AngledYawDegrees), elevation = Mathf.DegToRad(AngledElevationDegrees);
            cameraRight = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
            cameraBack = new Vector3(MathF.Sin(yaw) * MathF.Cos(elevation), MathF.Sin(elevation),
                MathF.Cos(yaw) * MathF.Cos(elevation));
            cameraUp = cameraBack.Cross(cameraRight);
            float tangent = MathF.Tan(Mathf.DegToRad(FitFieldOfViewDegrees) / 2);
            Vector2 fitFocal = new(projectionSize.X / (aspect * 2 * tangent), projectionSize.Y / (2 * tangent));
            Vector2 usableHalf = new(Math.Max(1, projectionSize.X / 2 - 14), Math.Max(1, projectionSize.Y / 2 - 14));
            // All four corners must fit. A full-board depth margin keeps even offscreen corners in front
            // while panning. Zoom changes FOV, not the camera distance, so those edges remain projectable.
            cameraDistance = width * MathF.Abs(cameraBack.X) + height * MathF.Abs(cameraBack.Z) + 1;
            foreach (Vector2 corner in new[] { Vector2.Zero, new Vector2(width, 0), new Vector2(width, height), new Vector2(0, height) })
            {
                Vector3 delta = new(corner.X - width / 2f, 0, corner.Y - height / 2f);
                float depth = delta.Dot(cameraBack);
                cameraDistance = Math.Max(cameraDistance, depth + MathF.Abs(delta.Dot(cameraRight)) * fitFocal.X / usableHalf.X);
                cameraDistance = Math.Max(cameraDistance, depth + MathF.Abs(delta.Dot(cameraUp)) * fitFocal.Y / usableHalf.Y);
            }
            (nearClip, farClip) = BoardClipDistances(width, height);
            // SetPerspective configures the complete projection, including the sub-degree FOV at high zoom.
            // The individual Fov property rejects values below one degree and would leave the native camera stale.
            camera.SetPerspective(Mathf.RadToDeg(2 * MathF.Atan(tangent / zoom)), nearClip, farClip);
            projectionScale = fitFocal * zoom;
        }
        else
        {
            cameraRight = Vector3.Right; cameraUp = new Vector3(0, 0, -1); cameraBack = Vector3.Up;
            cameraDistance = 100;
            float fitHeight = Math.Max(height * projectionSize.Y / Math.Max(1, projectionSize.Y - 28),
                width * projectionSize.X / (aspect * Math.Max(1, projectionSize.X - 28)));
            camera.Projection = Camera3D.ProjectionType.Orthogonal;
            camera.Size = Math.Max(.01f, fitHeight / zoom);
            (nearClip, farClip) = BoardClipDistances(width, height);
            camera.Near = nearClip; camera.Far = farClip;
            projectionScale = new Vector2(projectionSize.X / (camera.Size * aspect), projectionSize.Y / camera.Size);
        }
        camera.Position = new Vector3(center.X, 0, center.Y) + cameraBack * cameraDistance;
        camera.Basis = new Basis(cameraRight, cameraUp, cameraBack);
        projectedCellSize = ProjectedCellSizeAt(center);
        UpdateVisibleFootprint();
    }

    private (float Near, float Far) BoardClipDistances(int width, int height)
    {
        float left = -center.X * cameraBack.X, right = (width - center.X) * cameraBack.X;
        float top = -center.Y * cameraBack.Z, bottom = (height - center.Y) * cameraBack.Z;
        // The complete world, even offscreen cells, lies inside these depth bounds. Reserve a full unit
        // above and below the board; the current surfaces are only at Y=0 and Y=.024. A tiny near plane
        // wastes Compatibility depth precision and makes those layers fight at our fixed camera distance.
        float heightMargin = MathF.Abs(cameraBack.Y);
        float nearest = cameraDistance - Math.Max(left, right) - Math.Max(top, bottom) - heightMargin;
        float farthest = cameraDistance - Math.Min(left, right) - Math.Min(top, bottom) + heightMargin;
        // The fit distance exceeds the full ground depth span by one unit and center remains inside
        // the board. Therefore nearest is positive in either view, including extreme pan and zoom.
        float near = Math.Max(.01f, nearest * .5f);
        return (near, Math.Max(near + 1, farthest + 1));
    }

    private void UpdateVisibleFootprint()
    {
        visiblePolygon[0] = GroundAt(Vector2.Zero);
        visiblePolygon[1] = GroundAt(new Vector2(projectionSize.X, 0));
        visiblePolygon[2] = GroundAt(projectionSize);
        visiblePolygon[3] = GroundAt(new Vector2(0, projectionSize.Y));
        Vector2 minimum = visiblePolygon[0], maximum = visiblePolygon[0];
        for (int i = 0; i < 4; i++)
        {
            minimum = minimum.Min(visiblePolygon[i]); maximum = maximum.Max(visiblePolygon[i]);
            Vector2 edge = visiblePolygon[(i + 1) % 4] - visiblePolygon[i];
            visibilityNormals[i] = new Vector2(edge.Y, -edge.X).Normalized();
            visibilityLimits[i] = visibilityNormals[i].Dot(visiblePolygon[i]);
        }
        visibleArea = new Rect2(minimum, maximum - minimum);
        if (frame == null) { visibleBounds = new Rect2I(); return; }
        int x = Math.Clamp((int)MathF.Floor(minimum.X), 0, frame.Width);
        int y = Math.Clamp((int)MathF.Floor(minimum.Y), 0, frame.Height);
        int endX = Math.Clamp((int)MathF.Ceiling(maximum.X), x, frame.Width);
        int endY = Math.Clamp((int)MathF.Ceiling(maximum.Y), y, frame.Height);
        visibleBounds = new Rect2I(x, y, endX - x, endY - y);
    }
}
