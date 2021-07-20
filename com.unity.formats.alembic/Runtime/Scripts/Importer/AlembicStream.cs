using System;
using System.Collections.Generic;
using System.IO;
using Unity.Jobs;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Formats.Alembic.Sdk;

namespace UnityEngine.Formats.Alembic.Importer
{
    sealed class AlembicStream : IDisposable
    {
        /// <summary>
        /// This class hides the context. The reason for that it that there are Jobs depending on it and we need
        /// to ensure they get completed before any set happens on the context
        /// </summary>
        public struct SafeContext
        {
            public SafeContext(aiContext c)
            {
                context = c;
                updateJobHandle = new JobHandle();
            }

            aiContext context;
            public JobHandle updateJobHandle { get; private set; }
            public bool isValid => context;
            public aiObject root => context.topObject;
            public int timeSamplingCount => context.timeSamplingCount;

            public aiTimeSampling GetTimeSampling(int i)
            {
                return context.GetTimeSampling(i);
            }

            public bool IsHDF5()
            {
                return context.IsHDF5();
            }

            public void GetTimeRange(out double begin, out double end)
            {
                context.GetTimeRange(out begin, out end);
            }

            public void SetConfig(ref aiConfig conf)
            {
                updateJobHandle.Complete();
                context.SetConfig(ref conf);
            }

            public bool Load(string path)
            {
                updateJobHandle.Complete();
                return context.Load(path);
            }

            public void Destroy()
            {
                updateJobHandle.Complete();
                context.Destroy();
            }

            public void ScheduleUpdateSamples(double time)
            {
                var updateJob = new UpdateSamplesJob {context = context, time = time};
                updateJobHandle = updateJob.Schedule();
            }

            struct UpdateSamplesJob : IJob
            {
                public aiContext context;
                public double time;
                public void Execute()
                {
                    context.UpdateSamples(time);
                }
            }
        }

        static List<AlembicStream> s_streams = new List<AlembicStream>();

        public static void DisconnectStreamsWithPath(string path)
        {
            aiContext.DestroyByPath(path);
            s_streams.ForEach(s => {
                if (s.m_streamDesc.PathToAbc == path)
                {
                    s.m_streamInterupted = true;
                    s.m_context = new SafeContext(default);
                    s.m_loaded = false;
                }
            });
        }

        public static void RemapStreamsWithPath(string oldPath, string newPath)
        {
            s_streams.ForEach(s =>
            {
                if (s.m_streamDesc.PathToAbc == oldPath)
                {
                    s.m_streamInterupted = true;
                    s.m_streamDesc.PathToAbc = newPath;
                }
            });
        }

        public static void ReconnectStreamsWithPath(string path)
        {
            s_streams.ForEach(s =>
            {
                if (s.m_streamDesc.PathToAbc == path)
                {
                    s.m_streamInterupted = false;
                }
            });
        }

        IStreamDescriptor m_streamDesc;
        AlembicTreeNode m_abcTreeRoot;
        aiConfig m_config;
        SafeContext m_context;
        double m_time;
        bool m_loaded;
        bool m_streamInterupted;

        internal IStreamDescriptor streamDescriptor { get { return m_streamDesc; } }
        public AlembicTreeNode abcTreeRoot { get { return m_abcTreeRoot; } }
        internal SafeContext abcContext { get { return m_context; } }
        public bool abcIsValid { get { return m_context.isValid; } }
        internal aiConfig config { get { return m_config; } }

        internal bool IsHDF5()
        {
            return m_context.IsHDF5();
        }

        public void SetVertexMotionScale(float value) { m_config.vertexMotionScale = value; }

        public void GetTimeRange(out double begin, out double end) { m_context.GetTimeRange(out begin, out end); }


        internal AlembicStream(GameObject rootGo, IStreamDescriptor streamDesc)
        {
            m_config.SetDefaults();
            m_abcTreeRoot = new AlembicTreeNode() { stream = this, gameObject = rootGo };
            m_streamDesc = streamDesc;
        }

        void AbcBeforeUpdateSamples(AlembicTreeNode node)
        {
            if (node.abcObject != null && node.gameObject != null)
                node.abcObject.AbcPrepareSample();
            foreach (var child in node.Children)
                AbcBeforeUpdateSamples(child);
        }

        void AbcBeginSyncData(AlembicTreeNode node)
        {
            if (node != null && node.abcObject != null && node.gameObject != null)
                node.abcObject.AbcSyncDataBegin();
            foreach (var child in node.Children)
                AbcBeginSyncData(child);
        }

        void AbcEndSyncData(AlembicTreeNode node)
        {
            if (node.abcObject != null && node.gameObject != null)
                node.abcObject.AbcSyncDataEnd();
            foreach (var child in node.Children)
                AbcEndSyncData(child);
        }

        // returns false if the context needs to be recovered.
        public bool AbcUpdateBegin(double time)
        {
            if (m_streamInterupted) return true;
            if (!abcIsValid || !m_loaded) return false;

            m_time = time;
            m_context.SetConfig(ref m_config);
            AbcBeforeUpdateSamples(m_abcTreeRoot);
            m_context.ScheduleUpdateSamples(time);
            return true;
        }

        // returns false if the context needs to be recovered.
        public void AbcUpdateEnd()
        {
            if (m_streamInterupted)
                return;

            m_context.updateJobHandle.Complete();
            AbcBeginSyncData(m_abcTreeRoot);
            AbcEndSyncData(m_abcTreeRoot);
        }

        public void ClearMotionVectors()
        {
            ClearMotionVectors(m_abcTreeRoot);
        }

        void ClearMotionVectors(AlembicTreeNode node)
        {
            if (node.abcObject is AlembicMesh mesh)
            {
                mesh.ClearMotionVectors();
            }

            foreach (var child in node.Children)
                ClearMotionVectors(child);
        }

        public bool AbcLoad(bool createMissingNodes, bool serializeMesh)
        {
            m_time = 0.0f;
            m_context = new SafeContext(aiContext.Create(m_abcTreeRoot.gameObject.GetInstanceID()));

            var settings = m_streamDesc.Settings;
            m_config.swapHandedness = settings.SwapHandedness;
            m_config.flipFaces = settings.FlipFaces;
            m_config.aspectRatio = GetAspectRatio(settings.CameraAspectRatio);
            m_config.scaleFactor = settings.ScaleFactor;
            m_config.normalsMode = settings.Normals;
            m_config.tangentsMode = settings.Tangents;
            m_config.interpolateSamples = settings.InterpolateSamples;
            m_config.importPointPolygon = settings.ImportPointPolygon;
            m_config.importLinePolygon = settings.ImportLinePolygon;
            m_config.importTrianglePolygon = settings.ImportTrianglePolygon;

            m_context.SetConfig(ref m_config);
            m_loaded = m_context.Load(m_streamDesc.PathToAbc);

            if (m_loaded)
            {
                UpdateAbcTree(m_context.root, m_abcTreeRoot, m_time, createMissingNodes, serializeMesh);
                s_streams.Add(this);
            }
            else
            {
                if (!File.Exists(m_streamDesc.PathToAbc))
                {
                    Debug.LogError("File does not exist: " + m_streamDesc.PathToAbc);
                }
                else if (m_context.IsHDF5())
                {
                    Debug.LogError("Failed to load HDF5 alembic. Please convert to Ogawa: " + m_streamDesc.PathToAbc);
                }
                else
                {
                    Debug.LogError("File is in unknown format: " + m_streamDesc.PathToAbc);
                }
            }

            return m_loaded;
        }

        public void Dispose()
        {
            AlembicStream.s_streams.Remove(this);
            if (m_abcTreeRoot != null)
            {
                m_abcTreeRoot.Dispose();
                m_abcTreeRoot = null;
            }

            if (abcIsValid)
            {
                m_context.Destroy();
            }
        }

        class ImportContext
        {
            public AlembicTreeNode alembicTreeNode;
            public aiSampleSelector ss;
            public bool createMissingNodes;
        }

        ImportContext m_importContext;
        void UpdateAbcTree(aiObject top, AlembicTreeNode node, double time, bool createMissingNodes, bool serializeMesh)
        {
            if (!top)
                return;

            m_importContext = new ImportContext
            {
                alembicTreeNode = node,
                ss = NativeMethods.aiTimeToSampleSelector(time),
                createMissingNodes = createMissingNodes,
            };
            top.EachChild(ImportCallback);

            if (!serializeMesh)
            {
                foreach (var meshFilter in node.gameObject.GetComponentsInChildren<MeshFilter>())
                {
                    if (meshFilter.sharedMesh != null)
                    {
                        meshFilter.sharedMesh.hideFlags |= HideFlags.DontSave;
                    }
                }
            }

            m_importContext = null;
        }

        void ImportCallback(aiObject obj)
        {
            var ic = m_importContext;
            AlembicTreeNode treeNode = ic.alembicTreeNode;
            AlembicTreeNode childTreeNode = null;

            aiSchema schema = obj.AsXform();
            if (!schema) schema = obj.AsPolyMesh();
            if (!schema) schema = obj.AsSubD();
            if (!schema) schema = obj.AsCamera();
            if (!schema) schema = obj.AsPoints();
            if (!schema) schema = obj.AsCurves();

            // Get child. create if needed and allowed.
            string childName = obj.name;
            // Find targetted child GameObj
            GameObject childGO = null;

            var childTransf = treeNode.gameObject == null ? null : treeNode.gameObject.transform.Find(childName);
            if (childTransf == null)
            {
                if (!ic.createMissingNodes)
                {
                    obj.SetEnabled(false);
                    return;
                }
                else
                {
                    obj.SetEnabled(true);
                }

                childGO = RuntimeUtils.CreateGameObjectWithUndo("Create AlembicObject");
                childGO.name = childName;
                childGO.GetComponent<Transform>().SetParent(treeNode.gameObject.transform, false);
            }
            else
                childGO = childTransf.gameObject;

            childTreeNode = new AlembicTreeNode() { stream = this, gameObject = childGO };
            treeNode.Children.Add(childTreeNode);

            if (schema)
            {
                // Update
                AlembicElement elem = null;

                if (obj.AsXform() && m_streamDesc.Settings.ImportXform)
                    elem = childTreeNode.GetOrAddAlembicObj<AlembicXform>();
                else if (obj.AsCamera() && m_streamDesc.Settings.ImportCameras)
                    elem = childTreeNode.GetOrAddAlembicObj<AlembicCamera>();
                else if (obj.AsPolyMesh() && m_streamDesc.Settings.ImportMeshes)
                    elem = childTreeNode.GetOrAddAlembicObj<AlembicMesh>();
                else if (obj.AsSubD() && m_streamDesc.Settings.ImportMeshes)
                    elem = childTreeNode.GetOrAddAlembicObj<AlembicSubD>();
                else if (obj.AsPoints() && m_streamDesc.Settings.ImportPoints)
                    elem = childTreeNode.GetOrAddAlembicObj<AlembicPoints>();
                else if (obj.AsCurves() && m_streamDesc.Settings.ImportCurves)
                {
                    var curves = childTreeNode.GetOrAddAlembicObj<AlembicCurvesElement>();
                    curves.CreateRenderingComponent = m_streamDesc.Settings.CreateCurveRenderers;
                    elem = curves;
                }

                if (elem != null)
                {
                    elem.AbcSetup(obj, schema);
                    elem.AbcPrepareSample();
                    schema.UpdateSample(ref ic.ss);
                    elem.AbcSyncDataBegin();
                    elem.AbcSyncDataEnd();
                }
            }
            else
            {
                obj.SetEnabled(false);
                if (ic.createMissingNodes)
                {
                    Debug.LogWarning($"{childName} has an unsupported schema.");
                }
            }

            ic.alembicTreeNode = childTreeNode;
            obj.EachChild(ImportCallback);
            ic.alembicTreeNode = treeNode;
        }

        internal static float GetAspectRatio(AspectRatioMode mode)
        {
            if (mode == AspectRatioMode.CameraAperture)
            {
                return 0.0f;
            }
            else if (mode == AspectRatioMode.CurrentResolution)
            {
                return (float)Screen.width / (float)Screen.height;
            }
            else
            {
#if UNITY_EDITOR
                return (float)PlayerSettings.defaultScreenWidth / (float)PlayerSettings.defaultScreenHeight;
#else
                // fallback on current resoltution
                return (float)Screen.width / (float)Screen.height;
#endif
            }
        }
    }
}
