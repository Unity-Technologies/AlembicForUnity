using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace UnityEngine.Formats.Alembic.Importer
{
    static class VPMatrices
    {
        static Dictionary<Camera, Matrix4x4> s_currentVPMatrix = new Dictionary<Camera, Matrix4x4>();
        static Dictionary<Camera, Matrix4x4> s_previousVPMatrix = new Dictionary<Camera, Matrix4x4>();
        static int s_frameCount;

        public static Matrix4x4 Get(Camera camera)
        {
            if (Time.frameCount != s_frameCount) SwapMatrixMap();

            if (!camera)
            {
                return new Matrix4x4();
            }

            Matrix4x4 m;
            if (!s_currentVPMatrix.TryGetValue(camera, out m))
            {
                m = camera.nonJitteredProjectionMatrix * camera.worldToCameraMatrix;
                s_currentVPMatrix.Add(camera, m);
            }

            return m;
        }

        public static Matrix4x4 GetPrevious(Camera camera)
        {
            if (Time.frameCount != s_frameCount) SwapMatrixMap();

            Matrix4x4 m;
            if (s_previousVPMatrix.TryGetValue(camera, out m))
                return m;
            else
                return Get(camera);
        }

        static void SwapMatrixMap()
        {
            var temp = s_previousVPMatrix;
            s_previousVPMatrix = s_currentVPMatrix;
            temp.Clear();
            s_currentVPMatrix = temp;
            s_frameCount = Time.frameCount;
        }
    }

    /// <summary>
    /// Component that renders point clouds by instancing a mesh.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(AlembicPointsCloud))]
    public class AlembicPointsRenderer : MonoBehaviour
    {
        [SerializeField] Mesh m_mesh;
        [SerializeField] Material[] m_materials;
        [SerializeField] Material m_motionVectorMaterial;
        [SerializeField] ShadowCastingMode m_castShadows = ShadowCastingMode.On;
        [SerializeField] bool m_applyTransform = true;
        [SerializeField] bool m_receiveShadows = true;
        [SerializeField] bool m_generateMotionVector = true;
        [SerializeField] float m_pointSize = 0.2f;

        Mesh m_prevMesh;
        ComputeBuffer m_cbPoints;
        ComputeBuffer m_cbVelocities;
        ComputeBuffer m_cbIDs;
        ComputeBuffer[] m_cbArgs;
        CommandBuffer m_cmdMotionVector;
        int[] m_args = new int[5] { 0, 0, 0, 0, 0 };
        Bounds m_bounds;
        MaterialPropertyBlock m_mpb;

        Vector3 m_position, m_positionOld;
        Quaternion m_rotation, m_rotationOld;
        Vector3 m_scale, m_scaleOld;


        /// <summary>
        /// Get or set the reference to the Mesh instanced for every point cloud.
        /// </summary>
        public Mesh InstancedMesh
        {
            get { return m_mesh; }
            set { m_mesh = value; }
        }

        /// <summary>
        /// Get or set the array of Materials used for the rendering of the instanced Mesh. Only one Material per sub-Mesh will be used.
        /// </summary>
        public List<Material> Materials
        {
            get
            {
                var ret = new List<Material>(m_materials.Length);
                foreach (var t in m_materials)
                {
                    ret[0] = t;
                }
                return ret;
            }
            set { m_materials = value.ToArray(); }
        }

        /// <summary>
        /// Get or set the reference to the Material used for the motion vector computation.
        /// </summary>
        public Material MotionVectorMaterial
        {
            get { return m_motionVectorMaterial; }
            set { m_motionVectorMaterial = value; }
        }


        void Flush()
        {
            var apc = GetComponent<AlembicPointsCloud>();

            var points = apc.Positions;
            int numInstances = points.Count;
            if (numInstances == 0) { return; }
            var velocities = apc.Velocities;
            var ids = apc.Ids;

            var materials = m_materials;
            var mesh = m_mesh;
            if (mesh == null || materials == null) { return; }

            int submeshCount = System.Math.Min(mesh.subMeshCount, materials.Length);
            int layer = gameObject.layer;

            bool supportsInstancing = SystemInfo.supportsInstancing && SystemInfo.supportsComputeShaders;
            if (!supportsInstancing)
            {
                Debug.LogWarning("AlembicPointsRenderer: Instancing is not supported on this system.");
                return;
            }

            // check if mesh changed
            if (m_prevMesh != mesh)
            {
                m_prevMesh = mesh;
                if (m_cbArgs != null)
                {
                    foreach (var cb in m_cbArgs) { cb.Release(); }
                    m_cbArgs = null;
                }
            }


            bool abcHasVelocities = false;
            bool abcHasIDs = false;

            // update points buffer
            if (m_cbPoints != null && m_cbPoints.count < numInstances)
            {
                m_cbPoints.Release();
                m_cbPoints = null;
            }
            if (m_cbPoints == null)
            {
                m_cbPoints = new ComputeBuffer(numInstances, 12);
            }

            m_cbPoints.SetData(points);

            // update velocity buffer
            if (velocities.Count == numInstances)
            {
                abcHasVelocities = true;
                if (m_cbVelocities != null && m_cbVelocities.count < numInstances)
                {
                    m_cbVelocities.Release();
                    m_cbVelocities = null;
                }
                if (m_cbVelocities == null)
                {
                    m_cbVelocities = new ComputeBuffer(numInstances, 12);
                }
                m_cbVelocities.SetData(velocities);
            }

            // update ID buffer
            if (ids.Count == numInstances)
            {
                abcHasIDs = true;
                if (m_cbIDs != null && m_cbIDs.count < numInstances)
                {
                    m_cbIDs.Release();
                    m_cbIDs = null;
                }
                if (m_cbIDs == null)
                {
                    m_cbIDs = new ComputeBuffer(numInstances, 4);
                }

                m_cbIDs.SetData(ids);
            }

            // build bounds
            m_bounds = new Bounds(apc.BoundsCenter, apc.BoundsExtents + mesh.bounds.extents);


            // update materials
            if (m_mpb == null)
                m_mpb = new MaterialPropertyBlock();
            if (m_applyTransform)
            {
                m_mpb.SetVector("_Position", m_position);
                m_mpb.SetVector("_PositionOld", m_positionOld);
                m_mpb.SetVector("_Rotation", new Vector4(m_rotation.x, m_rotation.y, m_rotation.z, m_rotation.w));
                m_mpb.SetVector("_RotationOld", new Vector4(m_rotationOld.x, m_rotationOld.y, m_rotationOld.z, m_rotationOld.w));
                m_mpb.SetVector("_Scale", m_scale);
                m_mpb.SetVector("_ScaleOld", m_scaleOld);
            }
            else
            {
                m_mpb.SetVector("_Position", Vector3.zero);
                m_mpb.SetVector("_PositionOld", Vector3.zero);
                m_mpb.SetVector("_Rotation", new Vector4(0, 0, 0, 1));
                m_mpb.SetVector("_RotationOld", new Vector4(0, 0, 0, 1));
                m_mpb.SetVector("_Scale", Vector3.one);
                m_mpb.SetVector("_ScaleOld", Vector3.one);
            }
            m_mpb.SetFloat("_PointSize", m_pointSize);
            m_mpb.SetBuffer("_AlembicPoints", m_cbPoints);
            if (abcHasIDs)
            {
                m_mpb.SetFloat("_AlembicHasIDs", 1);
                m_mpb.SetBuffer("_AlembicIDs", m_cbIDs);
            }
            if (abcHasVelocities)
            {
                m_mpb.SetFloat("_AlembicHasVelocities", 1);
                m_mpb.SetBuffer("_AlembicVelocities", m_cbVelocities);
            }

            // update argument buffer
            if (m_cbArgs == null || m_cbArgs.Length != submeshCount)
            {
                if (m_cbArgs != null)
                {
                    foreach (var cb in m_cbArgs)
                        cb.Release();
                    m_cbArgs = null;
                }

                m_cbArgs = new ComputeBuffer[submeshCount];
                for (int si = 0; si < submeshCount; ++si)
                    m_cbArgs[si] = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            }
            for (int si = 0; si < submeshCount; ++si)
            {
                m_args[0] = (int)mesh.GetIndexCount(si);
                m_args[1] = numInstances;
                m_cbArgs[si].SetData(m_args);
            }

            // issue drawcalls
            int n = Math.Min(submeshCount, materials.Length);
            for (int si = 0; si < n; ++si)
            {
                var args = m_cbArgs[si];
                var material = materials[si];
                if (material == null)
                    continue;
                Graphics.DrawMeshInstancedIndirect(mesh, si, material,
                    m_bounds, args, 0, m_mpb, m_castShadows, m_receiveShadows, layer);
            }
        }

        void FlushMotionVector()
        {
            if (!m_generateMotionVector || Camera.current == null || (Camera.current.depthTextureMode & DepthTextureMode.MotionVectors) == 0)
                return;

            // assume setup is already done in Flush()

            var material = m_motionVectorMaterial;
            var mesh = m_mesh;
            var apc = GetComponent<AlembicPointsCloud>();
            if (mesh == null || material == null || apc.Velocities.Count == 0)
                return;

            material.SetMatrix("_PreviousVP", VPMatrices.GetPrevious(Camera.current));
            material.SetMatrix("_NonJitteredVP", VPMatrices.Get(Camera.current));

            int layer = gameObject.layer;

            if (m_cmdMotionVector == null)
            {
                m_cmdMotionVector = new CommandBuffer();
                m_cmdMotionVector.name = "AlembicPointsRenderer";
            }
            m_cmdMotionVector.Clear();
            m_cmdMotionVector.SetRenderTarget(BuiltinRenderTextureType.MotionVectors, BuiltinRenderTextureType.CameraTarget);
            for (int si = 0; si < mesh.subMeshCount; ++si)
                m_cmdMotionVector.DrawMeshInstancedIndirect(mesh, si, material, 0, m_cbArgs[si], 0, m_mpb);
            Graphics.ExecuteCommandBuffer(m_cmdMotionVector);
        }

        void Release()
        {
            if (m_cbArgs != null)
            {
                foreach (var cb in m_cbArgs) { cb.Release(); }
                m_cbArgs = null;
            }
            if (m_cbPoints != null) { m_cbPoints.Release(); m_cbPoints = null; }
            if (m_cbVelocities != null) { m_cbVelocities.Release(); m_cbVelocities = null; }
            if (m_cbIDs != null) { m_cbIDs.Release(); m_cbIDs = null; }
            if (m_cmdMotionVector != null) { m_cmdMotionVector.Release(); m_cmdMotionVector = null; }
        }

        void OnDisable()
        {
            Release();
        }

        void LateUpdate()
        {
            m_positionOld = m_position;
            m_rotationOld = m_rotation;
            m_scaleOld = m_scale;

            var trans = GetComponent<Transform>();
            m_position = trans.position;
            m_rotation = trans.rotation;
            m_scale = trans.lossyScale;

            Flush();
        }

        void OnRenderObject()
        {
            FlushMotionVector();
        }

        void Start()
        {
            var trans = GetComponent<Transform>();
            m_position = m_positionOld = trans.position;
            m_rotation = m_rotationOld = trans.rotation;
            m_scale = m_scaleOld = trans.lossyScale;
        }

        void OnDestroy()
        {
            if (m_cbPoints != null)
            {
                m_cbPoints.Dispose();
            }
            if (m_cbVelocities != null)
            {
                m_cbVelocities.Dispose();
            }
            if (m_cbIDs != null)
            {
                m_cbIDs.Dispose();
            }
            if (m_cmdMotionVector != null)
            {
                m_cmdMotionVector.Dispose();
            }
            if (m_cbArgs != null)
            {
                Array.ForEach(m_cbArgs, cb => { if (cb != null) cb.Dispose(); });
            }
        }
    }
}
