    namespace  SolTK.Spatials;
    using System;

    public readonly struct AABB(Vec2 min, Vec2 max)
    {
        public readonly Vec2 Min = min;
        public readonly Vec2 Max = max;

        public static AABB FromCenterRadius(Vec2 center, float radius) =>
            new(
                new Vec2(center.X - radius, center.Y - radius),
                new Vec2(center.X + radius, center.Y + radius)
            );

        public static AABB FromBox(Vec2 center, float rotation, Vec2 halfExtents)
        {
            var cos = MathF.Abs(MathF.Cos(rotation));
            var sin = MathF.Abs(MathF.Sin(rotation));
            var ex = cos * halfExtents.X + sin * halfExtents.Y;
            var ey = sin * halfExtents.X + cos * halfExtents.Y;
            return new(new Vec2(center.X - ex, center.Y - ey),
                    new Vec2(center.X + ex, center.Y + ey));
        }

        public bool Overlaps(in AABB other) =>
            Min.X <= other.Max.X && Max.X >= other.Min.X &&
            Min.Y <= other.Max.Y && Max.Y >= other.Min.Y;
    }