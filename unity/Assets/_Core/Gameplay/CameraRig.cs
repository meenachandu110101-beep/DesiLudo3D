using DesiLudo3D.Rules;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Camera rig: fits the whole board in BOTH orientations
    /// (D12 adaptive portrait/landscape) and rotates so the active player's
    /// corner faces them (pass-and-play, spec §4.3; toggleable).</summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Camera { get; private set; }

        const float FieldOfView = 40f;
        const float BoardExtent = 16.6f;     // frame slab size
        const float PitchDeg = 55f;
        const float RotateSpeed = 220f;      // degrees/second (smooth tween)

        float _azimuth = 225f;               // start facing Red's corner
        float _targetAzimuth = 225f;

        public void Attach(Camera camera)
        {
            Camera = camera;
            Camera.fieldOfView = FieldOfView;
        }

        public void SetSeat(Seat seat, bool instant = false)
        {
            _targetAzimuth = DesiLudo3D.Core.BoardGridMap.SeatAzimuthDeg(seat);
            if (instant) _azimuth = _targetAzimuth;
        }

        void LateUpdate()
        {
            if (Camera == null) return;

            _azimuth = Mathf.MoveTowardsAngle(_azimuth, _targetAzimuth, RotateSpeed * Time.deltaTime);

            float aspect = Camera.aspect;
            float vFov = FieldOfView * Mathf.Deg2Rad;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * aspect);

            // fit the board both horizontally and on screen (board foreshortened by pitch)
            float halfW = BoardExtent * 0.5f;
            float halfScreenH = BoardExtent * 0.62f;  // tuned gray-box factor
            float distW = halfW / Mathf.Tan(hFov * 0.5f);
            float distH = halfScreenH / Mathf.Tan(vFov * 0.5f);
            float dist = 1.12f * Mathf.Max(distW, distH);

            float pitch = PitchDeg * Mathf.Deg2Rad;
            float horiz = dist * Mathf.Cos(pitch);
            float height = dist * Mathf.Sin(pitch);

            float rad = _azimuth * Mathf.Deg2Rad;
            Vector3 centre = new Vector3(0f, 0.4f, 0f);
            Camera.transform.position = centre + new Vector3(Mathf.Sin(rad) * horiz, height, Mathf.Cos(rad) * horiz);
            Camera.transform.LookAt(centre);
        }
    }
}
