using System.Collections.Generic;
using UnityEngine;

namespace TD.Enemies
{
    public sealed class EnemyPath
    {
        private readonly Vector3[] points;
        private readonly float[] remainingLengths;

        public int Count => points.Length;
        public Vector3 this[int index] => points[index];

        public EnemyPath(IReadOnlyList<Vector3> waypoints)
        {
            int count = waypoints?.Count ?? 0;
            points = new Vector3[count];
            remainingLengths = new float[count];
            for (int index = 0; index < count; index++)
                points[index] = waypoints[index];
            for (int index = count - 2; index >= 0; index--)
                remainingLengths[index] = remainingLengths[index + 1] + Vector3.Distance(points[index], points[index + 1]);
        }

        public Vector3 Advance(Vector3 position, ref int waypointIndex, float distance)
        {
            float remaining = Mathf.Max(0f, distance);
            while (waypointIndex < Count)
            {
                Vector3 next = points[waypointIndex];
                float length = Vector3.Distance(position, next);
                if (length > remaining)
                    return Vector3.MoveTowards(position, next, remaining);

                position = next;
                remaining -= length;
                waypointIndex++;
            }
            return position;
        }

        public float GetProgress(Vector3 position, int waypointIndex)
        {
            if (Count < 2 || remainingLengths[0] <= Mathf.Epsilon)
                return 0f;
            if (waypointIndex >= Count)
                return 1f;

            int index = Mathf.Max(0, waypointIndex);
            float remaining = Vector3.Distance(position, points[index]) + remainingLengths[index];
            return 1f - Mathf.Clamp01(remaining / remainingLengths[0]);
        }
    }
}
