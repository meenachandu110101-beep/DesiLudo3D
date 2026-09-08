using System.Collections;
using System.Collections.Generic;
using DesiLudo3D.Rules;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>A 3D token (M1 gray-box capsule; M2 adds the photo medallion
    /// head, M3 the polished sculpt). Owns its animations: hop arcs with
    /// squash-and-stretch, capture flight home, finish pop.</summary>
    public class TokenView : MonoBehaviour
    {
        public Seat Seat { get; private set; }
        public int Index { get; private set; }

        Vector3 _baseScale;

        public void Initialize(Seat seat, int index, Transform parent)
        {
            Seat = seat;
            Index = index;
            transform.SetParent(parent, false);
            transform.localScale = new Vector3(0.34f, 0.26f, 0.34f); // capsule: h≈0.52
            _baseScale = transform.localScale;
            GetComponent<Renderer>().sharedMaterial = BoardBuilder.LitMaterial(BoardBuilder.ColorOf(seat));

            // head placeholder (M2 replaces with photo medallion)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            head.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            head.GetComponent<Renderer>().sharedMaterial =
                BoardBuilder.LitMaterial(BoardBuilder.ColorOf(seat).Tint(0.35f));
            var headCol = head.GetComponent<Collider>();
            if (headCol != null) Destroy(headCol);

            SnapToBase();
        }

        public Vector3 WorldOfBaseSlot =>
            GridPosToWorld(DesiLudo3D.Core.BoardGridMap.BaseSlots[(int)Seat][Index]);

        public void SnapToBase()
        {
            transform.position = SlotStanding(WorldOfBaseSlot);
        }

        public void SnapToProgress(int progress)
        {
            transform.position = SlotStanding(ProgressToWorld(Seat, progress));
        }

        public static Vector3 ProgressToWorld(Seat seat, int progress)
        {
            var (x, y) = DesiLudo3D.Core.BoardGridMap.Position(seat, progress);
            var (wx, wz) = DesiLudo3D.Core.BoardGridMap.GridToWorld(x, y);
            return new Vector3(wx, 0f, wz);
        }

        static Vector3 GridPosToWorld((int X, int Y) grid)
        {
            var (wx, wz) = DesiLudo3D.Core.BoardGridMap.GridToWorld(grid.X, grid.Y);
            return new Vector3(wx, 0f, wz);
        }

        static Vector3 SlotStanding(Vector3 ground) => new Vector3(ground.x, 0.24f, ground.z);

        // ------------------------------------------------------------- moves

        /// <summary>Hop along waypoints with squash-and-stretch. Release moves
        /// arc from the base; finishing moves pop at the centre.</summary>
        public IEnumerator AnimateMove(List<Vector3> waypoints, bool isRelease, bool isFinish)
        {
            Vector3 start = transform.position;
            if (isRelease)
            {
                yield return Arc(start, SlotStanding(waypoints[0]), 0.35f, 1.6f);
                start = SlotStanding(waypoints[0]);
                waypoints.RemoveAt(0);
            }

            const float stepTime = 0.14f;
            foreach (Vector3 target in waypoints)
            {
                Vector3 standing = SlotStanding(target);
                yield return Hop(start, standing, stepTime);
                start = standing;
            }

            if (isFinish)
            {
                yield return Squash(1.35f, 0.25f);   // victory pop
            }
        }

        /// <summary>Victim flight: spin + high arc back to the base slot.</summary>
        public IEnumerator AnimateCaptureHome()
        {
            Vector3 from = transform.position;
            Vector3 to = SlotStanding(WorldOfBaseSlot);
            Quaternion startRot = transform.rotation;
            const float duration = 0.7f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.position = Vector3.Lerp(from, to, k)
                                     + Vector3.up * (2.2f * k * (1f - k) * 4f); // high arc
                transform.rotation = startRot * Quaternion.Euler(0f, 720f * k, 0f);
                yield return null;
            }
            transform.position = to;
            transform.rotation = startRot;
            yield return Squash(1.3f, 0.2f);
        }

        IEnumerator Hop(Vector3 from, Vector3 to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.position = Vector3.Lerp(from, to, k)
                                     + Vector3.up * (0.35f * 4f * k * (1f - k));
                yield return null;
            }
            transform.position = to;
            yield return Squash(1.25f, 0.06f);
        }

        IEnumerator Arc(Vector3 from, Vector3 to, float duration, float height)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.position = Vector3.Lerp(from, to, k)
                                     + Vector3.up * (height * 4f * k * (1f - k));
                yield return null;
            }
            transform.position = to;
        }

        IEnumerator Squash(float amount, float duration)
        {
            float t = 0f;
            Vector3 squashed = new Vector3(_baseScale.x * amount, _baseScale.y / amount, _baseScale.z * amount);
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                transform.localScale = Vector3.Lerp(_baseScale, squashed, Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            transform.localScale = _baseScale;
        }
    }
}
