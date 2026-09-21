using UnityEngine;

namespace TD.Combat
{
    public static class CombatPhysics
    {
        private static int lastSyncedFrame = -1;
        private static bool dirty = true;

        public static void Invalidate() => dirty = true;

        public static void Synchronize()
        {
            // Enemy movement precedes tower and projectile updates. Target lifecycle
            // changes invalidate the snapshot; moving projectiles do not.
            if (Application.isPlaying && !dirty && lastSyncedFrame == Time.frameCount)
                return;

            Physics2D.SyncTransforms();
            lastSyncedFrame = Time.frameCount;
            dirty = false;
        }
    }
}
