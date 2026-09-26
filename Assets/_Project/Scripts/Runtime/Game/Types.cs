using System.Collections.Generic;
using Squishy.Runtime.Models;
using Squishy.Runtime.Three;
using Squishy.Simulation.Game;
using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>A piece in the room: its saved state, model and the springy animation state.</summary>
    public sealed class Item
    {
        public PieceState st;
        public ItemTypeData a;
        public Transform g;
        public ItemParts parts = new ItemParts();
        public float lift, bx, bv, vx, vz;
        public bool atWall;
        public float? grow;

        public string key { get { return st.key; } }
        public string arch { get { return st.Arch; } }
        public string style { get { return st.Style; } }
        public float tx { get { return st.x; } set { st.x = value; } }
        public float tz { get { return st.z; } set { st.z = value; } }
        public float ry { get { return st.ry; } set { st.ry = value; } }

        /// <summary>World position of the model (three space).</summary>
        public Vector3 Pos { get { return g.localPosition; } }
    }

    public struct Obstacle
    {
        public float x, z, r;
        public Item it;
    }

    public sealed class PathPt
    {
        public float x, z, y;
        public bool big, chase;
    }

    public sealed class Seg
    {
        public float fx, fz, fy, tx, tz, ty, d, len;
        public bool big, chase;
    }

    public sealed class Spot
    {
        public Vector2? approach, face;
        public Vector2 stand;
        public float y;
    }

    public sealed class Activity
    {
        public Item it;
        public string role;
        public ActivityData act;
        public RecipeData recipe;
        public bool scrubbed, tipped;
    }

    /// <summary>The squishy's brain state (the prototype's `ai`).</summary>
    public sealed class Ai
    {
        public float x, z = .3f, y, idleT = 3, hopPh, actT, warnT = 4;
        public string mode = "idle";
        public List<PathPt> path = new List<PathPt>();
        public Seg seg;
        public Activity act;
        public int kicks;
        public Vector2? perch;
        public Spot spot;
        public bool self;
        public Item target;
    }

    /// <summary>Home/arrange camera state (the prototype's `cam`).</summary>
    public sealed class CamState
    {
        public float yaw, yawV, zoom, zoomT, edit, ez = 1, ezT = 1, tilt = 62, tiltT = 62, htilt;
        public Vector3 target = new Vector3(0, .3f, .3f);
        public Vector3 panT;
        public Vector2? panGoal, mid, edge;
    }

    public sealed class Pointer
    {
        public Vector2 pos;
        public float t;
    }

    public sealed class Drag
    {
        public Vector2 start;
        public bool moved, touch, squish, scrub, wand;
        public Item item, ball;
        public float ox, oz;
        public readonly List<Vector3> hist = new List<Vector3>(); // x, z, time
    }

    public sealed class Snapshot
    {
        public List<(Item it, float x, float z, float r)> list = new List<(Item, float, float, float)>();
        public List<string> store;
    }

    public static class Ease
    {
        public static float InOutCubic(float t) { return t < .5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2; }
        public static float Sstep(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3 - 2 * x); }
        public static float Rnd(float a, float b) { return Random.Range(a, b); }
        public static T Pick<T>(IList<T> a) { return a[Random.Range(0, a.Count)]; }
    }
}
