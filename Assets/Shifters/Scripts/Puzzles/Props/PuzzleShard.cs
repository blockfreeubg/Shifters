using UnityEngine;

namespace Shifters
{
    /// <summary>One pooled debris shard (glass/concrete). Lives 3 s, then shrinks away and returns to the pool.</summary>
    public class PuzzleShard : MonoBehaviour
    {
        public const float Life = 3f;
        public const float FadeTime = 0.5f;

        public Rigidbody Body { get; private set; }
        public MeshRenderer Renderer { get; private set; }
        public float SpawnTime { get; private set; }
        Vector3 _scale;
        float _age;

        public static PuzzleShard Create()
        {
            var go = new GameObject("PuzzleShard");
            var s = go.AddComponent<PuzzleShard>();
            s.Renderer = ProcMesh.AddRenderer(go, ProcMesh.CachedBox(Vector3.one, 0.08f), Kit.Mat(MatKey.Glass), false);
            go.AddComponent<BoxCollider>().size = Vector3.one;
            s.Body = go.AddComponent<Rigidbody>();
            s.Body.mass = 2f;
            s.Body.linearDamping = 0.2f;
            s.Body.angularDamping = 0.3f;
            go.SetActive(false);
            return s;
        }

        public void Launch(Vector3 pos, Quaternion rot, Vector3 scale, Material mat, Vector3 velocity, Vector3 spin)
        {
            transform.SetPositionAndRotation(pos, rot);
            transform.localScale = scale;
            _scale = scale;
            Renderer.sharedMaterial = mat;
            gameObject.SetActive(true);
            Body.position = pos;
            Body.rotation = rot;
            Body.linearVelocity = velocity;
            Body.angularVelocity = spin;
            SpawnTime = Time.time;
            _age = 0f;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age < Life) return;
            float t = (_age - Life) / FadeTime;
            if (t >= 1f) { gameObject.SetActive(false); return; }
            transform.localScale = _scale * (1f - t);
        }
    }

    /// <summary>Bounded pool of debris shards (max 32 live rigidbodies).</summary>
    public static class PuzzleShardPool
    {
        const int Max = 32;
        static readonly System.Collections.Generic.List<PuzzleShard> _pool = new System.Collections.Generic.List<PuzzleShard>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _pool.Clear();

        /// <summary>Spawn count shards inside a box (center world, size local to rot) flying along push.</summary>
        public static void Burst(Vector3 center, Vector3 size, Quaternion rot, Material mat, int count, float shardSize, Vector3 push)
        {
            if (!Application.isPlaying) return;
            for (int i = 0; i < count; i++)
            {
                var s = Get();
                if (s == null) return;
                var local = new Vector3(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y, Random.Range(-0.5f, 0.5f) * size.z);
                var pos = center + rot * local;
                var scale = new Vector3(Random.Range(0.5f, 1.2f), Random.Range(0.5f, 1.2f), Random.Range(0.2f, 0.6f)) * shardSize;
                var vel = push + Random.insideUnitSphere * 3f + Vector3.up * Random.Range(1f, 4f);
                s.Launch(pos, Random.rotation, scale, mat, vel, Random.insideUnitSphere * 10f);
            }
        }

        static PuzzleShard Get()
        {
            PuzzleShard oldest = null;
            for (int i = _pool.Count - 1; i >= 0; i--)
            {
                var s = _pool[i];
                if (s == null) { _pool.RemoveAt(i); continue; }
                if (!s.gameObject.activeSelf) return s;
                if (oldest == null || s.SpawnTime < oldest.SpawnTime) oldest = s;
            }
            if (_pool.Count < Max) { var n = PuzzleShard.Create(); _pool.Add(n); return n; }
            return oldest;
        }
    }
}
