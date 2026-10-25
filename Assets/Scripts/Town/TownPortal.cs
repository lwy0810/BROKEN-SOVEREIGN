using System;
using BrokenSovereign.Sound;
using UnityEngine;

namespace BrokenSovereign.Town
{
    /// <summary>
    /// 귀환 포탈 한 개 (10/25). 세워진 푸른 원판 + 빛. 플레이어가 Radius 안으로 들어오면 Entered 이벤트.
    /// 처음 생길 때 플레이어가 이미 안에 있으면 한 번 나갔다 와야 들어감 (생성·도착 직후 바로 타는 것 방지).
    /// 모형은 런타임에 만든 임시 모양 → 전용 VFX·모형이 생기면 Visual만 교체.
    /// </summary>
    public class TownPortal : MonoBehaviour
    {
        private const float RADIUS = 1.1f;
        private const float HEIGHT = 1.4f;
        private const float SIZE = 1.8f;
        private static readonly Color OUTER = new Color(0.3f, 0.6f, 1f);
        private static readonly Color INNER = new Color(0.78f, 0.92f, 1f);

        private Transform _player;
        private Transform _visual;
        private bool _armed;
        private float _born;

        public event Action<TownPortal> Entered;

        public static TownPortal Create(string name, Vector3 position, Vector3 facing, Transform player)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            facing.y = 0f;
            go.transform.rotation = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing.normalized) : Quaternion.identity;
            TownPortal portal = go.AddComponent<TownPortal>();
            portal._player = player;
            portal.BuildVisual();
            SoundPlayer.PlayAt(SoundId.PORTAL, position);
            return portal;
        }

        private void BuildVisual()
        {
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            _visual.localPosition = Vector3.up * HEIGHT;
            Disc("Ring", OUTER, SIZE, 0f);
            Disc("Core", INNER, SIZE * 0.72f, 0.02f);

            Light light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(_visual, false);
            light.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            light.type = LightType.Point;
            light.color = OUTER;
            light.range = 6f;
            light.intensity = 2.5f;
        }

        // 원기둥을 납작하게 세워 원판으로 (축 Y → Z). 충돌 없음
        private void Disc(string name, Color color, float size, float forward)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(_visual, false);
            disc.transform.localPosition = new Vector3(0f, 0f, forward);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(size, 0.02f, size);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { color = color };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            disc.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void Update()
        {
            // 열리는 연출 0.4초 + 맥동
            float open = Mathf.Clamp01((Time.time - _born) / 0.4f);
            float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.04f;
            _visual.localScale = Vector3.one * (open * pulse);

            if (_player == null)
            {
                return;
            }

            Vector3 offset = _player.position - transform.position;
            bool inside = Mathf.Abs(offset.y) < 2f && new Vector2(offset.x, offset.z).sqrMagnitude < RADIUS * RADIUS;

            if (!inside)
            {
                _armed = true;
            }
            else if (_armed)
            {
                _armed = false;
                Entered?.Invoke(this);
            }
        }

        private void Awake()
        {
            _born = Time.time;
        }

        private void OnDestroy()
        {
            if (_visual == null)
            {
                return;
            }

            foreach (Renderer renderer in _visual.GetComponentsInChildren<Renderer>())
            {
                Destroy(renderer.sharedMaterial);
            }
        }
    }
}
