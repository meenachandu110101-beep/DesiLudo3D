using System.Collections;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Gray-box dice: tumbles on roll, settles with a little bounce,
    /// shows the value via the HUD (M3 replaces with a real pip-textured,
    /// physics-tumbled die that lands on the predetermined face — spec §4.2).</summary>
    public class DiceView : MonoBehaviour
    {
        Vector3 _restPosition;
        Quaternion _restRotation;

        void Awake()
        {
            _restPosition = transform.position;
            _restRotation = transform.rotation;
            var col = GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        public IEnumerator AnimateRoll(int value)
        {
            const float tumbleTime = 0.65f;
            Vector3 axis = Random.onUnitSphere;
            float speed = Random.Range(540f, 900f);
            float t = 0f;
            while (t < tumbleTime)
            {
                t += Time.deltaTime;
                transform.Rotate(axis, speed * Time.deltaTime, Space.World);
                transform.position = _restPosition + Vector3.up * (0.25f * Mathf.Sin(Mathf.PI * t / tumbleTime));
                yield return null;
            }

            // settle
            const float settleTime = 0.2f;
            Quaternion start = transform.rotation;
            Quaternion end = _restRotation * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            t = 0f;
            while (t < settleTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / settleTime);
                transform.rotation = Quaternion.Slerp(start, end, k);
                transform.position = Vector3.Lerp(transform.position, _restPosition, k);
                yield return null;
            }
            transform.position = _restPosition;
            transform.rotation = end;
        }
    }
}
