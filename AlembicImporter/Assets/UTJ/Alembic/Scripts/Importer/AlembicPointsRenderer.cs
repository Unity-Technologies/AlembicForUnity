using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace UTJ.Alembic
{
    [ExecuteInEditMode]
    public class AlembicPointsRenderer : MonoBehaviour
    {
        public enum InstancingMode
        {
            NoInstancing,
#if UNITY_5_5_OR_NEWER
            Instancing,
#endif
#if UNITY_5_6_OR_NEWER
            Procedural,
#endif
        }

        public const int MaxInstancesParDraw = 1023;

        public Mesh m_mesh;
        public Material[] m_materials;
        public ShadowCastingMode m_shadow = ShadowCastingMode.Off;
        public bool m_receiveShadows = false;
        public LayerSelector m_layer = 0;
        public float m_pointSize = 0.2f;
        public InstancingMode m_instancingMode =
#if UNITY_5_5_OR_NEWER
            InstancingMode.Instancing;
#else
            InstancingMode.NoInstancing;
#endif
        [Tooltip("Use Alembic Points IDs as shader input")]
        public bool m_useAlembicIDs = false;

#if UNITY_5_5_OR_NEWER
        const string m_kwAlembicProceduralInstancing = "ALEMBIC_PROCEDURAL_INSTANCING_ENABLED";
        Matrix4x4[] m_matrices;
        float[] m_ids;
        List<MaterialPropertyBlock> m_mpbs;
#endif
#if UNITY_5_6_OR_NEWER
        ComputeBuffer m_cbPoints;
        ComputeBuffer m_cbIDs;
        ComputeBuffer[] m_cbArgs;
        int[] m_args = new int[5] { 0, 0, 0, 0, 0 };
#endif
#if UNITY_EDITOR
        bool m_dirty = false;
#endif


        public Mesh sharedMesh
        {
            get { return m_mesh; }
            set { m_mesh = value; }
        }

        public Material material
        {
            get { return m_materials != null && m_materials.Length > 0 ? m_materials[0] : null; }
            set { m_materials = new Material[] { value }; }
        }
        public Material[] materials
        {
            get { return m_materials; }
            set { m_materials = value; }
        }


        public void Flush()
        {
            if(m_mesh == null || m_materials == null || m_materials.Length == 0) { return; }

            var apc = GetComponent<AlembicPointsCloud>();
            var points = apc.abcPositions;
            if(points == null) { return; }

            int num_instances = points.Length;
            if(num_instances == 0) { return; }
            int num_submeshes = System.Math.Min(m_mesh.subMeshCount, m_materials.Length);

            bool supportsInstancing = SystemInfo.supportsInstancing;
#if UNITY_5_6_OR_NEWER
            int pidPointSize = Shader.PropertyToID("_PointSize");
            int pidAlembicPoints = Shader.PropertyToID("_AlembicPoints");
#endif
#if UNITY_5_5_OR_NEWER
            int pidAlembicIDs = Shader.PropertyToID("_AlembicIDs");
#endif

            if (!supportsInstancing && m_instancingMode != InstancingMode.NoInstancing)
            {
                Debug.LogWarning("AlembicPointsRenderer: Instancing is not supported on this system. fallback to InstancingMode.NoInstancing.");
                m_instancingMode = InstancingMode.NoInstancing;
            }

#if UNITY_5_6_OR_NEWER
            if (m_instancingMode == InstancingMode.Procedural && !SystemInfo.supportsComputeShaders)
            {
                Debug.LogWarning("AlembicPointsRenderer: InstancingMode.Procedural is not supported on this system. fallback to InstancingMode.Instancing.");
                m_instancingMode = InstancingMode.Instancing;
            }

            if (supportsInstancing && m_instancingMode == InstancingMode.Procedural)
            {
                // Graphics.DrawMeshInstancedIndirect() route

                // update argument buffer
                if (m_cbArgs == null || m_cbArgs.Length != num_submeshes)
                {
                    Release();
                    m_cbArgs = new ComputeBuffer[num_submeshes];
                    for (int i = 0; i < num_submeshes; ++i)
                    {
                        m_cbArgs[i] = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                    }
                }

                // update points buffer
                if (m_cbPoints != null && m_cbPoints.count != num_instances)
                {
                    m_cbPoints.Release();
                    m_cbPoints = null;
                }
                if (m_cbPoints == null)
                {
                    m_cbPoints = new ComputeBuffer(num_instances, 12);
                }
                m_cbPoints.SetData(points);

                // update ID buffer
                bool alembicIDsAvailable = false;
                if (m_useAlembicIDs)
                {
                    if (m_cbIDs != null && m_cbIDs.count != num_instances)
                    {
                        m_cbIDs.Release();
                        m_cbIDs = null;
                    }
                    if (m_cbIDs == null)
                    {
                        m_cbIDs = new ComputeBuffer(num_instances, 4);
                    }
                    ulong[] ids = apc.abcIDs;
                    if (ids != null && ids.Length == num_instances)
                    {
                        if (m_ids == null || m_ids.Length != num_instances)
                        {
                            m_ids = new float[num_instances];
                        }
                        for (int i = 0; i < num_instances; ++i)
                        {
                            m_ids[i] = ids[i];
                        }
                        m_cbIDs.SetData(m_ids);
                        alembicIDsAvailable = true;
                    }
                }

                // build bounds
                var bounds = new Bounds(apc.m_boundsCenter, apc.m_boundsExtents + m_mesh.bounds.extents);

                // issue drawcalls
                for (int si = 0; si < num_submeshes; ++si)
                {
                    var args = m_cbArgs[si];
                    m_args[0] = (int)m_mesh.GetIndexCount(0);
                    m_args[1] = num_instances;
                    args.SetData(m_args);

                    var material = m_materials[si];
                    material.EnableKeyword(m_kwAlembicProceduralInstancing);
                    material.SetFloat(pidPointSize, m_pointSize);
                    material.SetBuffer(pidAlembicPoints, m_cbPoints);
                    if (alembicIDsAvailable) { material.SetBuffer(pidAlembicIDs, m_cbIDs); }
                    Graphics.DrawMeshInstancedIndirect(m_mesh, si, material,
                        bounds, args, 0, null, m_shadow, m_receiveShadows, m_layer);
                }
            }
            else
#endif
#if UNITY_5_5_OR_NEWER
            if (supportsInstancing && m_instancingMode == InstancingMode.Instancing)
            {
                // Graphics.DrawMeshInstanced() route
                // Graphics.DrawMeshInstanced() can draw only up to 1023 instances.
                // multiple drawcalls maybe required.

                int num_batches = (num_instances + MaxInstancesParDraw - 1) / MaxInstancesParDraw;

                if (m_matrices == null || m_matrices.Length != MaxInstancesParDraw)
                {
                    m_matrices = new Matrix4x4[MaxInstancesParDraw];
                    for (int i = 0; i < MaxInstancesParDraw; ++i) { m_matrices[i] = Matrix4x4.identity; }
                }

                for (int si = 0; si < num_submeshes; ++si)
                {
                    var material = m_materials[si];
                    if (material.IsKeywordEnabled(m_kwAlembicProceduralInstancing))
                    {
                        material.DisableKeyword(m_kwAlembicProceduralInstancing);
                    }
                }

                // setup alembic point IDs
                bool alembicIDsAvailable = false;
                ulong[] ids = null;
                if (m_useAlembicIDs)
                {
                    ids = apc.abcIDs;
                    alembicIDsAvailable = ids != null && ids.Length == num_instances;
                    if (alembicIDsAvailable)
                    {
                        if (m_ids == null || m_ids.Length != MaxInstancesParDraw)
                        {
                            m_ids = new float[MaxInstancesParDraw];
                        }
                        if (m_mpbs == null)
                        {
                            m_mpbs = new List<MaterialPropertyBlock>();
                        }
                        while (m_mpbs.Count < num_batches)
                        {
                            m_mpbs.Add(new MaterialPropertyBlock());
                        }
                    }
                }

                for (int ib = 0; ib < num_batches; ++ib)
                {
                    int ibegin = ib * MaxInstancesParDraw;
                    int iend = System.Math.Min(ibegin + MaxInstancesParDraw, num_instances);
                    int n = iend - ibegin;

                    // build matrices

                    for (int ii = 0; ii < n; ++ii)
                    {
                        Vector3 rotatedPosition = apc.transform.rotation* new Vector3(points[ibegin + ii].x,points[ibegin + ii].y,points[ibegin + ii].z);
                        Vector3 finalPosition = new Vector3(rotatedPosition.x + apc.transform.position.x,rotatedPosition.y + apc.transform.position.y,rotatedPosition.z + apc.transform.position.z);
                        m_matrices[ii].SetTRS(finalPosition,Quaternion.identity,new Vector3(m_pointSize,m_pointSize,m_pointSize));
                    }

                    MaterialPropertyBlock mpb = null;
                    if (alembicIDsAvailable)
                    {
                        for (int ii = 0; ii < n; ++ii)
                        {
                            m_ids[ii] = ids[ibegin + ii];
                        }
                        mpb = m_mpbs[ib];
                        mpb.SetFloatArray(pidAlembicIDs, m_ids);
                    }

                    // issue drawcalls
                    for (int si = 0; si < num_submeshes; ++si)
                    {
                        var material = m_materials[si];
                        Graphics.DrawMeshInstanced(m_mesh, si, material, m_matrices, n, mpb, m_shadow, m_receiveShadows, m_layer);
                    }
                }
            }
            else
#endif
            {
                // Graphics.DrawMesh() route
                // not use IDs in this case because it's too expensive...

                var matrix = Matrix4x4.identity;
                matrix.m00 = matrix.m11 = matrix.m22 = m_pointSize;
                for (int ii = 0; ii < num_instances; ++ii)
                {
                    matrix.m03 = points[ii].x;
                    matrix.m13 = points[ii].y;
                    matrix.m23 = points[ii].z;

                    // issue drawcalls
                    for (int si = 0; si < num_submeshes; ++si)
                    {
                        var material = m_materials[si];
                        Graphics.DrawMesh(m_mesh, matrix, material, m_layer, null, si, null, m_shadow, m_receiveShadows);
                    }
                }
            }
        }

        public void Release()
        {
#if UNITY_5_6_OR_NEWER
            if (m_cbArgs != null) {
                foreach(var cb in m_cbArgs) { cb.Release(); }
                m_cbArgs = null;
            }
            if (m_cbPoints != null) { m_cbPoints.Release(); m_cbPoints = null; }
            if (m_cbIDs != null) { m_cbIDs.Release(); m_cbIDs = null; }
#endif
        }


        void OnDisable()
        {
            Release();
        }

        void LateUpdate()
        {
            Flush();
#if UNITY_EDITOR
            m_dirty = true;
#endif
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            // force draw particles while paused.
            // using OnDrawGizmos() is dirty workaround but I couldn't find better way...
            if (EditorApplication.isPaused && m_dirty)
            {
                Flush();
                m_dirty = false;
            }
        }
#endif
    }
}
