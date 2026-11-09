namespace SolTK.Spatials;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using fennecs;

/// <summary>
/// Hashed uniform grid. Broad phase only — returns AABB-overlap candidates.
/// Rebuild each tick: Clear() then Insert() each entity once.
/// Deterministic: cells iterate by insertion order; slots are insertion order.
/// </summary>
public sealed class SpatialGrid
{
    private readonly float _cellSize;
    private readonly float _invCellSize;
    private readonly int _maxCellsPerEntity;

    // Parallel arrays indexed by internal slot (= insertion order).
    private Entity[] _entities = [];
    private AABB[] _aabbs = [];
    private bool[] _isOversized = [];
    private int _count;

    // Per-slot query stamp — dedup when an entity spans multiple cells.
    private int[] _stamps = [];
    private int _queryStamp;

    // cell key -> slots in that cell
    private readonly Dictionary<long, List<int>> _buckets = [];
    private readonly List<long> _activeCells = []; // insertion order = determinism
    private readonly Stack<List<int>> _bucketPool = new();

    // Entities that would span too many cells; scanned linearly instead.
    private readonly List<int> _oversized = [];

    // Pair-dedup scratch (reused across calls).
    private readonly HashSet<long> _pairSet = [];

    public SpatialGrid(float cellSize, int maxCellsPerEntity = 16)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cellSize, 0f);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCellsPerEntity, 1);
        _cellSize = cellSize;
        _invCellSize = 1f / cellSize;
        _maxCellsPerEntity = maxCellsPerEntity;
    }

    public int Count => _count;

    public void Clear()
    {
        for (int i = 0; i < _activeCells.Count; i++)
        {
            var bucket = _buckets[_activeCells[i]];
            bucket.Clear();
            _bucketPool.Push(bucket);
        }
        _buckets.Clear();
        _activeCells.Clear();
        _oversized.Clear();
        _count = 0;
    }

    public void Insert(Entity entity, in AABB aabb)
    {
        Debug.Assert(aabb.Min.X <= aabb.Max.X && aabb.Min.Y <= aabb.Max.Y,
            "SpatialGrid.Insert: inverted or NaN AABB.");

        if (_count == _entities.Length) GrowStorage();

        int slot = _count++;
        _entities[slot] = entity;
        _aabbs[slot] = aabb;
        _isOversized[slot] = false;   // FIX: reset stale flag from previous tick

        int minCx = FloorToCell(aabb.Min.X);
        int minCy = FloorToCell(aabb.Min.Y);
        int maxCx = FloorToCell(aabb.Max.X);
        int maxCy = FloorToCell(aabb.Max.Y);

        long spanX = (long)maxCx - minCx + 1;
        long spanY = (long)maxCy - minCy + 1;
        long cells  = spanX * spanY;

        if (cells > _maxCellsPerEntity)
        {
            _oversized.Add(slot);
            _isOversized[slot] = true;
            return;
        }

        for (int cy = minCy; cy <= maxCy; cy++)
        {
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                long key = PackCell(cx, cy);
                if (!_buckets.TryGetValue(key, out var bucket))
                {
                    bucket = _bucketPool.Count > 0 ? _bucketPool.Pop() : new List<int>(8);
                    _buckets[key] = bucket;
                    _activeCells.Add(key);
                }
                bucket.Add(slot);
            }
        }
    }

    /// <summary>
    /// Returns entities whose AABB overlaps <paramref name="query"/>.
    /// Clears <paramref name="results"/> first. Deterministic order.
    /// </summary>
    public void QueryAABB(in AABB query, List<Entity> results, int? maxResults = null)
    {
        results.Clear();
        int cap = maxResults ?? int.MaxValue;
        if (cap < 0) throw new ArgumentOutOfRangeException(nameof(maxResults));
        if (cap == 0) return;                      // FIX: honour maxResults == 0

        int stamp = NextStamp();

        int minCx = FloorToCell(query.Min.X);
        int minCy = FloorToCell(query.Min.Y);
        int maxCx = FloorToCell(query.Max.X);
        int maxCy = FloorToCell(query.Max.Y);

        for (int cy = minCy; cy <= maxCy; cy++)
        {
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                if (!_buckets.TryGetValue(PackCell(cx, cy), out var bucket)) continue;

                for (int i = 0; i < bucket.Count; i++)
                {
                    int slot = bucket[i];
                    if (_stamps[slot] == stamp) continue;
                    _stamps[slot] = stamp;

                    if (!_aabbs[slot].Overlaps(query)) continue;
                    if (results.Count >= cap) return;  // FIX: check before adding
                    results.Add(_entities[slot]);
                }
            }
        }

        for (int i = 0; i < _oversized.Count; i++)
        {
            int slot = _oversized[i];
            if (_stamps[slot] == stamp) continue;
            _stamps[slot] = stamp;

            if (!_aabbs[slot].Overlaps(query)) continue;
            if (results.Count >= cap) return;          // FIX
            results.Add(_entities[slot]);
        }
    }

    /// <summary>
    /// Broad-phase circle query: returns anything whose AABB overlaps the circle's bounding AABB.
    /// Callers should narrow-test (circle-vs-shape) if precision is required.
    /// </summary>
    public void QueryCircle(Vec2 center, float radius, List<Entity> results, int? maxResults = null)
        => QueryAABB(AABB.FromCenterRadius(center, radius), results, maxResults);

    /// <summary>
    /// Emits all candidate pairs (AABB-overlap only) for narrow phase.
    /// Each unordered pair appears at most once; canonical order is (lower slot, higher slot).
    /// Deterministic order.
    /// </summary>
    public void GetPairs(List<(Entity A, Entity B)> results)
    {
        results.Clear();
        _pairSet.Clear();

        for (int c = 0; c < _activeCells.Count; c++)
        {
            var bucket = _buckets[_activeCells[c]];
            for (int i = 0; i < bucket.Count; i++)
            {
                for (int j = i + 1; j < bucket.Count; j++)
                {
                    int a = bucket[i], b = bucket[j];
                    if (a == b) continue;

                    int lo = a < b ? a : b;
                    int hi = a < b ? b : a;
                    long key = ((long)lo << 32) | (uint)hi;
                    if (!_pairSet.Add(key)) continue;

                    if (!_aabbs[lo].Overlaps(_aabbs[hi])) continue;
                    results.Add((_entities[lo], _entities[hi]));
                }
            }
        }

        // Oversized entities aren't in any bucket; check them against every slot.
        for (int oi = 0; oi < _oversized.Count; oi++)
        {
            int o = _oversized[oi];
            for (int j = 0; j < _count; j++)
            {
                if (j == o) continue;
                // Both oversized: process once, when o < j.
                if (_isOversized[j] && j < o) continue;

                int lo = o < j ? o : j;
                int hi = o < j ? j : o;
                long key = ((long)lo << 32) | (uint)hi;
                if (!_pairSet.Add(key)) continue;

                if (!_aabbs[lo].Overlaps(_aabbs[hi])) continue;
                results.Add((_entities[lo], _entities[hi]));
            }
        }
    }

    // ── internals ──────────────────────────────────────────────

    private int NextStamp()
    {
        if (_queryStamp >= int.MaxValue - 1)
        {
            Array.Clear(_stamps);
            _queryStamp = 0;
        }
        return ++_queryStamp;
    }

    private void GrowStorage()
    {
        int newCap = Math.Max(16, _entities.Length * 2);
        Array.Resize(ref _entities, newCap);
        Array.Resize(ref _aabbs, newCap);
        Array.Resize(ref _isOversized, newCap);
        Array.Resize(ref _stamps, newCap);
    }

    private int FloorToCell(float worldCoord) =>
        (int)MathF.Floor(worldCoord * _invCellSize);

    private static long PackCell(int cx, int cy) =>
        ((long)cx << 32) | (uint)cy;
}