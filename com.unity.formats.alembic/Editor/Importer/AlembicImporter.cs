using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;
using UnityEngine.Formats.Alembic.Sdk;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
using static UnityEditor.AssetDatabase;
#else
using UnityEditor.Experimental.AssetImporters;
using static UnityEditor.Experimental.AssetDatabaseExperimental;
#endif

namespace UnityEditor.Formats.Alembic.Importer
{
    class AlembicAssetModificationProcessor : AssetModificationProcessor
    {
        public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions rao)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return AssetDeleteResult.DidNotDelete;
            }

            if (Path.GetExtension(assetPath.ToLower()) != ".abc")
                return AssetDeleteResult.DidNotDelete;

            AlembicStream.DisconnectStreamsWithPath(assetPath);

            return AssetDeleteResult.DidNotDelete;
        }

        public static AssetMoveResult OnWillMoveAsset(string from, string to)
        {
            if (string.IsNullOrEmpty(from))
            {
                return AssetMoveResult.DidNotMove;
            }

            if (Path.GetExtension(from.ToLower()) != ".abc")
                return AssetMoveResult.DidNotMove;

            var importer = AssetImporter.GetAtPath(from) as AlembicImporter;
            if (importer != null)
            {
                var so = new SerializedObject(importer);
                var prop = so.FindProperty("rootGameObjectName");
                if (prop != null && string.IsNullOrEmpty(prop.stringValue))
                {
                    prop.stringValue = Path.GetFileNameWithoutExtension(from);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                prop = so.FindProperty("rootGameObjectId");
                if (prop != null && string.IsNullOrEmpty(prop.stringValue))
                {
                    prop.stringValue = Path.GetFileNameWithoutExtension(from);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                AssetDatabase.WriteImportSettingsIfDirty(from);
            }

            AlembicStream.DisconnectStreamsWithPath(from);
            AlembicStream.RemapStreamsWithPath(from, to);

            AssetDatabase.Refresh(ImportAssetOptions.Default);
            AlembicStream.ReconnectStreamsWithPath(to);

            return AssetMoveResult.DidNotMove;
        }
    }

    [ScriptedImporter(9, "abc")]
    internal class AlembicImporter : ScriptedImporter
    {
        [SerializeField]
#pragma warning disable 0649
        private string rootGameObjectId;
        [SerializeField]
        private string rootGameObjectName;
#pragma warning restore 0649
        [SerializeField]
        private AlembicStreamSettings streamSettings = new AlembicStreamSettings();
        public AlembicStreamSettings StreamSettings
        {
            get { return streamSettings; }
            set { streamSettings = value; }
        }
        [SerializeField]
        private double abcStartTime; // read only
        public double AbcStartTime
        {
            get { return abcStartTime; }
        }
        [SerializeField]
        private double abcEndTime; // read only
        public double AbcEndTime
        {
            get { return abcEndTime; }
        }
        [SerializeField]
        private double startTime = double.MinValue;
        public double StartTime
        {
            get { return startTime; }
            set { startTime = value; }
        }
        [SerializeField]
        private double endTime = double.MaxValue;
        public double EndTime
        {
            get { return endTime; }
            set { endTime = value; }
        }
        [SerializeField]
        private string importWarning;
        public string ImportWarning
        {
            get { return importWarning; }
            set { importWarning = value; }
        }

        [SerializeField] bool firstImport = true;

        internal bool IsHDF5
        {
            get { return isHDF5;}
        }
        [SerializeField] bool isHDF5;

        void OnValidate()
        {
            if (!firstImport)
            {
                if (startTime < abcStartTime)
                    startTime = abcStartTime;
                if (endTime > abcEndTime)
                    endTime = abcEndTime;
            }
        }

        const string renderPipepineDependency = "AlembicRenderPipelineDependency";

        internal struct MaterialEntry
        {
            public AlembicCustomData component;
            public string path;
            public int index;
            public Material material;

            public SourceAssetIdentifier ToSourceAssetIdentifier()
            {
                return new SourceAssetIdentifier(typeof(Material), path + $":{index}");
            }

            public static MaterialEntry FromSourceAssetIdentifier(SourceAssetIdentifier identifier, GameObject rootGO)
            {
                var toks = identifier.name.Split(':');
                var go = AlembicImporter.GetGameObjectFromPath(rootGO, toks[0]);
                return new MaterialEntry
                {
                    component = go.GetComponent<AlembicCustomData>(),
                    path = toks[0],
                    index = int.Parse(toks[1])
                };
            }
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (ctx == null)
            {
                return;
            }

            var path = ctx.assetPath;
            AlembicStream.DisconnectStreamsWithPath(path);

            var fileName = Path.GetFileNameWithoutExtension(path);
            var previousGoName = fileName;

            if (!string.IsNullOrEmpty(rootGameObjectName))
            {
                previousGoName = rootGameObjectName;
            }
            var go = new GameObject(previousGoName);

            var streamDescriptor = ScriptableObject.CreateInstance<AlembicStreamDescriptor>();
            streamDescriptor.name = go.name + "_ABCDesc";
            streamDescriptor.PathToAbc = path;
            streamDescriptor.Settings = StreamSettings;

            using (new RuntimeUtils.DisableUndoGuard(true))
            {
                using (var abcStream = new AlembicStream(go, streamDescriptor))
                {
                    abcStream.AbcLoad(true, true);
                    abcStream.GetTimeRange(out abcStartTime, out abcEndTime);
                    if (firstImport)
                    {
                        startTime = abcStartTime;
                        endTime = abcEndTime;
                    }

                    streamDescriptor.MediaStartTime = (float)abcStartTime;
                    streamDescriptor.MediaEndTime = (float)abcEndTime;

                    var streamPlayer = go.AddComponent<AlembicStreamPlayer>();
                    streamPlayer.StreamSource = AlembicStreamPlayer.AlembicStreamSource.Internal;
                    streamPlayer.StreamDescriptor = streamDescriptor;
                    streamPlayer.StartTime = (float)StartTime;
                    streamPlayer.EndTime = (float)EndTime;

                    var subassets = new Subassets(ctx);
                    subassets.Add(streamDescriptor.name, streamDescriptor);
                    GenerateSubAssets(subassets, abcStream.abcTreeRoot, streamDescriptor);

                    AlembicStream.ReconnectStreamsWithPath(path);

                    var prevIdName = fileName;
                    if (!string.IsNullOrEmpty(rootGameObjectId))
                    {
                        prevIdName = rootGameObjectId;
                    }

                    ctx.AddObjectToAsset(prevIdName, go);
                    ctx.SetMainObject(go);

                    isHDF5 = abcStream.IsHDF5();
                    if (IsHDF5)
                    {
                        Debug.LogError(path + ": Unsupported HDF5 file format detected. Please convert to Ogawa.");
                    }

                    ApplyMaterialAssignments(go, subassets);
                }
            }

            firstImport = false;
        }

        public override bool SupportsRemappedAssetType(Type type)
        {
            return type == typeof(Material);
        }

        void ApplyMaterialAssignments(GameObject go, Subassets subs)
        {
            var remap = GetExternalObjectMap();

            foreach (var r in remap)
            {
                if (r.Value == null) // Null means default material
                {
                    continue;
                }

                var pathFaceId = r.Key.name.Split(':');
                var path = pathFaceId[0];
                var materialId = Int32.Parse(pathFaceId[1]);

                var meshGO = GetGameObjectFromPath(go, path);
                var renderer = meshGO.GetComponent<MeshRenderer>();
                var mats = renderer.sharedMaterials;
                mats[materialId] = r.Value as Material;
                renderer.sharedMaterials = mats;
            }
        }

        internal static List<MaterialEntry> GenMaterialSlots(AlembicImporter importer, GameObject go)
        {
            var ret = new List<MaterialEntry>();
            var remap = importer.GetExternalObjectMap();
            foreach (var customData in go.GetComponentsInChildren<AlembicCustomData>())
            {
                var path = GetGameObjectPath(customData.gameObject);
                for (var i = 0; i < customData.FacesetNames.Count; ++i)
                {
                    var entry = new MaterialEntry {component = customData, index = i, path = path};
                    if (remap.TryGetValue(entry.ToSourceAssetIdentifier(), out var material))
                    {
                        entry.material = (Material)material;
                    }

                    ret.Add(entry);
                }
            }

            return ret;
        }

        static string GetGameObjectPath(GameObject go)
        {
            var reversePath = new List<string>();
            var parent = go.transform;
            while (parent != null)
            {
                reversePath.Add(parent.name);
                parent = parent.parent;
            }

            var sb = new StringBuilder();
            for (var i = reversePath.Count - 2; i >= 0; --i) // We don't want the root
            {
                sb.Append(reversePath[i]);
                sb.Append('/');
            }
            return sb.ToString().TrimEnd('/');
        }

        internal static GameObject GetGameObjectFromPath(GameObject root, string path)
        {
            var go = root;
            foreach (var name in path.Split('/'))
            {
                var found = false;
                for (var i = 0; i < go.transform.childCount; ++i)
                {
                    var ch = go.transform.GetChild(i);
                    if (ch.name == name)
                    {
                        go = ch.gameObject;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new Exception($"Cannot find path:{path} from GameObject: {root.name}");
                }
            }

            return go;
        }

        [InitializeOnLoadMethod]
        static void InitializeEditorCallback()
        {
            EditorApplication.update += DirtyCustomDependencies; //
            pipelineHash = ComputeHash();
        }

        static ulong pipelineHash;
        static readonly TimeSpan checkDependencyFrequency = TimeSpan.FromSeconds(5);
        static DateTime lastCheck;

        static ulong ComputeHash()
        {
            var newPipelineHash = 0UL;
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                newPipelineHash = 0;
            }
            else
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(GraphicsSettings.currentRenderPipeline, out var guid,
                    out long fileId))
                {
                    newPipelineHash =
                        RuntimeUtils.CombineHash((ulong)guid.GetHashCode(), (ulong)fileId);
                }
            }

            return newPipelineHash;
        }

        static void DirtyCustomDependencies()
        {
            var now = DateTime.Now;
            if (Application.isPlaying || now - lastCheck < checkDependencyFrequency)
            {
                return;
            }

            lastCheck = now;

            var newPipelineHash = ComputeHash();
            if (pipelineHash != newPipelineHash)
            {
                pipelineHash = newPipelineHash;
                RegisterCustomDependency(renderPipepineDependency, new Hash128(pipelineHash, 0));
                AssetDatabase.Refresh();
            }
        }

        class Subassets
        {
            AssetImportContext m_ctx;
            Material m_defaultMaterial;
            Material m_defaultPointsMaterial;
            Material m_defaultPointsMotionVectorMaterial;
            int addPrecomputedVelocityProperty = Shader.PropertyToID("_AddPrecomputedVelocity");

            public Subassets(AssetImportContext ctx)
            {
                m_ctx = ctx;
            }

            public Material defaultMaterial
            {
                get
                {
                    if (m_defaultMaterial == null)
                    {
                        if (GraphicsSettings.currentRenderPipeline == null)
                        {
                            m_defaultMaterial = GetMaterial("Standard.shader");
                        }
                        else
                        {
                            m_defaultMaterial = Instantiate(GraphicsSettings.currentRenderPipeline.defaultMaterial);
                            // Enable the HDRP Custom Motion Vector Pass
                            if (m_defaultMaterial.HasProperty(addPrecomputedVelocityProperty))
                            {
                                m_defaultMaterial.SetFloat(addPrecomputedVelocityProperty, 1);
                                m_defaultMaterial.EnableKeyword("_ADD_PRECOMPUTED_VELOCITY");
                            }
                        }

                        Add("Default Material", m_defaultMaterial);
                        m_defaultMaterial.hideFlags = HideFlags.NotEditable;
                        m_defaultMaterial.name = "Default Material";
                        m_ctx.DependsOnCustomDependency(renderPipepineDependency);
                    }
                    return m_defaultMaterial;
                }
            }

            public Material pointsMaterial
            {
                get
                {
                    if (m_defaultPointsMaterial == null)
                    {
                        m_defaultPointsMaterial = GetMaterial("StandardInstanced.shader");
                        m_defaultPointsMaterial.hideFlags = HideFlags.NotEditable;
                        m_defaultPointsMaterial.name = "Default Points";
                        Add("Default Points", m_defaultPointsMaterial);
                    }
                    return m_defaultPointsMaterial;
                }
            }

            public Material pointsMotionVectorMaterial
            {
                get
                {
                    if (m_defaultPointsMotionVectorMaterial == null)
                    {
                        m_defaultPointsMotionVectorMaterial = GetMaterial("AlembicPointsMotionVectors.shader");
                        m_defaultPointsMotionVectorMaterial.hideFlags = HideFlags.NotEditable;
                        m_defaultPointsMotionVectorMaterial.name = "Points Motion Vector";
                        Add("Points Motion Vector", m_defaultPointsMotionVectorMaterial);
                    }
                    return m_defaultPointsMotionVectorMaterial;
                }
            }

            public void Add(string identifier, Object asset)
            {
                m_ctx.AddObjectToAsset(identifier, asset);
            }

            Material GetMaterial(string shaderFile)
            {
                var path = Path.Combine("Packages/com.unity.formats.alembic/Runtime/Shaders", shaderFile);
                m_ctx.DependsOnSourceAsset(path);
                var shader =
                    AssetDatabase.LoadAssetAtPath<Shader>(path);
                return new Material(shader);
            }
        }

        void GenerateSubAssets(Subassets subassets, AlembicTreeNode root, AlembicStreamDescriptor streamDescr)
        {
            if (streamDescr.MediaDuration > 0)
            {
                // AnimationClip for time
                {
                    var frames = new Keyframe[2];
                    frames[0].value = 0.0f;
                    frames[0].time = 0.0f;
                    frames[0].outTangent = 1.0f;
                    frames[1].value = streamDescr.MediaDuration;
                    frames[1].time = streamDescr.MediaDuration;
                    frames[1].inTangent = 1.0f;

                    var curve = new AnimationCurve(frames);
                    AnimationUtility.SetKeyLeftTangentMode(curve, 0, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, 1, AnimationUtility.TangentMode.Linear);

                    var clip = new AnimationClip();
                    clip.SetCurve("", typeof(AlembicStreamPlayer), "currentTime", curve);
                    clip.name = root.gameObject.name + "_Time";
                    clip.hideFlags = HideFlags.NotEditable;

                    subassets.Add("Default Animation", clip);
                }

                // AnimationClip for frame events
                {
                    var abc = root.stream.abcContext;
                    var n = abc.timeSamplingCount;
                    for (int i = 1; i < n; ++i)
                    {
                        var clip = new AnimationClip();
                        if (AddFrameEvents(clip, abc.GetTimeSampling(i)))
                        {
                            var name = root.gameObject.name + "_Frames";
                            if (n > 2)
                                name += i.ToString();
                            clip.name = name;
                            subassets.Add(clip.name, clip);
                        }
                    }
                }
            }

            CollectSubAssets(subassets, root);
        }

        void CollectSubAssets(Subassets subassets, AlembicTreeNode node)
        {
            int submeshCount = 0;
            var meshFilter = node.gameObject.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                var m = meshFilter.sharedMesh;
                submeshCount = m.subMeshCount;
                m.name = node.gameObject.name;
                subassets.Add(node.abcObject.abcObject.fullname, m);
            }

            var renderer = node.gameObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mats = new Material[submeshCount];
                for (int i = 0; i < submeshCount; ++i)
                    mats[i] = subassets.defaultMaterial;
                renderer.sharedMaterials = mats;
            }

            var apr = node.gameObject.GetComponent<AlembicPointsRenderer>();
            if (apr != null)
            {
                var cubeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                apr.InstancedMesh = cubeGO.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(cubeGO);

                apr.Materials = new List<Material> { subassets.pointsMaterial };
                apr.MotionVectorMaterial = subassets.pointsMotionVectorMaterial;
            }

            foreach (var child in node.Children)
                CollectSubAssets(subassets, child);
        }

        bool AddFrameEvents(AnimationClip clip, aiTimeSampling ts)
        {
            int n = ts.sampleCount;
            if (n <= 0)
                return false;

            var events = new AnimationEvent[n];
            for (int i = 0; i < n; ++i)
            {
                var ev = new AnimationEvent();
                ev.time = (float)ts.GetTime(i);
                ev.intParameter = i;
                ev.functionName = "AbcOnFrameChange";
                events[i] = ev;
            }
            AnimationUtility.SetAnimationEvents(clip, events);
            return true;
        }
    }
}
