using UnityEngine;

namespace EndlessPrison
{
    /// <summary>斜上方俯視的跟隨鏡頭。Q/E 旋轉 90°，滾輪縮放。</summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform Target;
        public float Pitch = 52f;
        public float Distance = 11f;
        public float MinDistance = 6f;
        public float MaxDistance = 17f;

        float yaw = 45f;
        float targetYaw = 45f;
        Vector3 focus;
        Camera cam;

        public Camera Cam { get { return cam; } }

        public static CameraRig Create(Transform target)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.AsphaltBlue;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 80f;

            var rig = cam.GetComponent<CameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
            rig.cam = cam;
            rig.Target = target;
            rig.focus = target != null ? target.position : Vector3.zero;
            rig.Apply();
            return rig;
        }

        public void SnapTo(Transform t)
        {
            Target = t;
        }

        void LateUpdate()
        {
            if (InputCompat.KeyDown(KeyCode.Q)) targetYaw -= 90f;
            if (InputCompat.KeyDown(KeyCode.E)) targetYaw += 90f;
            float s = InputCompat.Scroll;
            if (Mathf.Abs(s) > 0.01f) Distance = Mathf.Clamp(Distance - s * 1.2f, MinDistance, MaxDistance);

            yaw = Mathf.LerpAngle(yaw, targetYaw, Time.deltaTime * 8f);
            if (Target != null) focus = Vector3.Lerp(focus, Target.position, Time.deltaTime * 5f);
            Apply();
        }

        void Apply()
        {
            Quaternion rot = Quaternion.Euler(Pitch, yaw, 0f);
            transform.position = focus + Vector3.up * 0.8f - rot * Vector3.forward * Distance;
            transform.rotation = rot;
        }
    }
}
