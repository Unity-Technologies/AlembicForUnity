#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine.Formats.Alembic.Sdk;
using UnityEngine.Rendering;
using static UnityEngine.Formats.Alembic.Importer.RuntimeUtils;

namespace UnityEngine.Formats.Alembic.Importer
{
    /// <summary>
    /// This component allows data streaming from Alembic files. It updates children nodes (Meshes, Transforms, Cameras, etc.) to reflect the Alembic data at the given time.
    /// </summary>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class AlembicStreamPlayer : MonoBehaviour, ISerializationCallbackReceiver
    {
        internal enum AlembicStreamSource
        {
            Internal = 0,
            External = 1
        }

        [SerializeField] AlembicStreamSource streamSource = AlembicStreamSource.External;
        internal AlembicStreamSource StreamSource
        {
            get => streamSource;
            set => streamSource = value;
        }

        // "m_" prefix is intentionally missing and expose fields as public just to keep asset compatibility...
        internal AlembicStream abcStream { get; set; }
        [SerializeField] AlembicStreamDescriptor streamDescriptor;
        [SerializeField] EmbeddedAlembicStreamDescriptor embeddedStreamDescriptor = new EmbeddedAlembicStreamDescriptor();
        /// <summary>
        /// Gives access to the stream description.
        /// </summary>
        internal IStreamDescriptor StreamDescriptor
        {
            get => StreamSource == AlembicStreamSource.External ? embeddedStreamDescriptor : (IStreamDescriptor)streamDescriptor;
            set
            {
                if (StreamSource == AlembicStreamSource.External)
                {
                    embeddedStreamDescriptor = (EmbeddedAlembicStreamDescriptor)value;
                }
                else
                {
                    streamDescriptor = (AlembicStreamDescriptor)value;
                }
            }
        }

        [SerializeField]
        float startTime = float.MinValue;
        /// <summary>
        /// Get or set the start timestamp of the streaming time window (scale in seconds). This is clamped to the time range of the Alembic source file.
        /// </summary>
        public float StartTime
        {
            get => startTime;
            set
            {
                startTime = value;
                if (StreamDescriptor != null)
                {
                    startTime = Mathf.Clamp(startTime, StreamDescriptor.MediaStartTime, StreamDescriptor.MediaEndTime);
                }
            }
        }

        [SerializeField]
        float endTime = float.MaxValue;
        /// <summary>
        /// Get or set the end timestamp of the streaming time window (scale in seconds). This is clamped to the time range of the Alembic source file.
        /// </summary>
        public float EndTime
        {
            get => endTime;
            set
            {
                endTime = value;
                if (StreamDescriptor != null)
                {
                    endTime = Mathf.Clamp(endTime, StartTime, StreamDescriptor.MediaEndTime);
                }
            }
        }

        [SerializeField]
        float currentTime;
        /// <summary>
        /// Get or set the current time relative to the Alembic file time range (scale in seconds). This is clamped between 0 and the alembic time duration.
        /// </summary>
        public float CurrentTime
        {
            get => currentTime;
            set => currentTime = Mathf.Clamp(value, 0.0f, Duration);
        }

        /// <summary>
        /// Get the duration of the Alembic file (in seconds).
        /// </summary>
        public float Duration => EndTime - StartTime;

        [SerializeField]
        float vertexMotionScale = 1.0f;
        /// <summary>
        /// Get or set the scalar multiplier to the Alembic vertex speed (magnification factor for velocity). Default value is 1.
        /// </summary>
        public float VertexMotionScale
        {
            get => vertexMotionScale;
            set => vertexMotionScale = value;
        }

        /// <summary>
        /// The start timestamp of the Alembic file (scale in seconds).
        /// </summary>
        public float MediaStartTime => StreamDescriptor != null ? StreamDescriptor.MediaStartTime : 0;
        /// <summary>
        /// The end timestamp of the Alembic file (scale in seconds).
        /// </summary>
        public float MediaEndTime => StreamDescriptor != null ? StreamDescriptor.MediaEndTime : 0;

        /// <summary>
        /// The duration of the Alembic file (in seconds).
        /// </summary>
        public float MediaDuration => MediaEndTime - MediaStartTime;

        /// <summary>
        /// The path to the Alembic asset. When in a standalone build, the returned path is prepended by the streamingAssets path.
        /// </summary>
        public string PathToAbc => StreamDescriptor != null ? StreamDescriptor.PathToAbc : "";

        /// <summary>
        /// The stream import options. NOTE: these options are shared between all instances of this asset.
        /// </summary>
        public AlembicStreamSettings Settings
        {
            get { return StreamDescriptor != null ? StreamDescriptor.Settings : null; }
            set
            {
                if (StreamDescriptor == null)
                {
                    StreamDescriptor = ScriptableObject.CreateInstance<AlembicStreamDescriptor>();
                }

                StreamDescriptor.Settings = value;
                ReloadStream();
            }
        }

        float lastUpdateTime;
        bool forceUpdate = false;
        bool updateStarted = false;


        /// <summary>
        /// Update the child GameObject's data to the CurrentTime (The regular update happens during the LateUpdate phase).
        /// </summary>
        /// <param name="time">The timestamp to stream from the asset file.</param>
        public void UpdateImmediately(float time)
        {
            CurrentTime = time;
            Update();
            LateUpdate();
        }

        /// <summary>
        /// Loads a different Alembic file.
        /// </summary>
        /// <param name="newPath">Path to the new file.</param>
        /// <returns>True if the load succeeded, false otherwise.</returns>
        public bool LoadFromFile(string newPath)
        {
            if (StreamDescriptor == null)
            {
                StreamDescriptor = ScriptableObject.CreateInstance<AlembicStreamDescriptor>();
            }

            StreamDescriptor.PathToAbc = newPath;
            return InitializeAfterLoad();
        }

        /// <summary>
        /// Closes and reopens the Alembic stream. Use this method to apply the new stream settings.
        /// </summary>
        /// <param name="createMissingNodes">If true, it also recreates the missing GameObjects for the Alembic nodes. </param>>
        /// <returns>True if the stream was successfully reopened, false otherwise.</returns>>
        public bool ReloadStream(bool createMissingNodes = false)
        {
            if (abcStream != null)
            {
                abcStream?.Dispose();
                return LoadStream(createMissingNodes);
            }

            return true;
        }

        /// <summary>
        /// This function removes all child game objects that don't have a corresponding alembic node. Note that is the object is a part of a prefab, this call will fail. Please note that GameObjects that are a part of a Prefab cannot be deleted.
        /// </summary>
        public void RemoveObsoleteGameObjects()
        {
            ReloadStream(true);
            RemoveObsoleteGameObject(gameObject);
        }

        void RemoveObsoleteGameObject(GameObject root)
        {
            var nChildren = root.transform.childCount;
            for (var i = nChildren - 1; i >= 0; --i) // need to iterate backwards because deleting an object changes the child count
            {
                RemoveObsoleteGameObject(root.transform.GetChild(i).gameObject);
            }

            if (abcStream.abcTreeRoot.FindNode(root) == null) // no alembic node means not driven by ABC
            {
#if UNITY_EDITOR
                var prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(root);
                if (prefabInstanceStatus == PrefabInstanceStatus.Connected)
                {
                    Debug.LogError($"Cannot Remove GameObject: {root.name} because it is a part of a Prefab. Please delete in Prefab Isolation Mode");
                    return;
                }
#endif

                DestroyUnityObject(root);
            }
        }

        bool InitializeAfterLoad()
        {
            var ret = LoadStream(true, true);
            if (!ret)
                return false;

            abcStream.GetTimeRange(out var start, out var end);
            startTime = (float)start;
            endTime = (float)end;

            StreamDescriptor.MediaStartTime = (float)start;
            StreamDescriptor.MediaEndTime = (float)end;

            var defaultMat = AlembicMesh.GetDefaultMaterial();

            gameObject.DepthFirstVisitor(go =>
            {
                var filter = go.GetComponent<MeshFilter>();
                var meshRenderer = go.GetComponent<MeshRenderer>();
                if (filter == null || meshRenderer == null)
                {
                    return;
                }

                var mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    return;
                }

                var subMesh = mesh.subMeshCount;
                var mats = new Material[subMesh];
                for (var i = 0; i < subMesh; ++i)
                {
                    mats[i] = defaultMat;
                }

                meshRenderer.sharedMaterials = mats;
            });

            return true;
        }

        internal void CloseStream()
        {
            abcStream?.Dispose();
            abcStream = null;
        }

        void ClampTime()
        {
            CurrentTime = Mathf.Clamp(CurrentTime, 0.0f, Duration);
        }

        internal bool LoadStream(bool createMissingNodes, bool serializeMesh = false)
        {
            if (StreamDescriptor == null || string.IsNullOrEmpty(StreamDescriptor.PathToAbc))
                return false;
            CloseStream();

            abcStream = new AlembicStream(gameObject, StreamDescriptor);
            var ret = abcStream.AbcLoad(createMissingNodes, serializeMesh);
            forceUpdate = true;
            return ret;
        }

        void Start()
        {
            OnValidate();
        }

        void OnValidate()
        {
            if (StreamDescriptor == null || abcStream == null)
                return;
            if (StreamDescriptor.MediaStartTime == double.MinValue || StreamDescriptor.MediaEndTime == double.MaxValue)
            {
                double start, end;
                abcStream.GetTimeRange(out start, out end);
                StreamDescriptor.MediaStartTime = (float)start;
                StreamDescriptor.MediaEndTime = (float)end;
            }

            StartTime = Mathf.Clamp(StartTime, StreamDescriptor.MediaStartTime, StreamDescriptor.MediaEndTime);
            EndTime = Mathf.Clamp(EndTime, StartTime, StreamDescriptor.MediaEndTime);
            ClampTime();
            forceUpdate = true;
        }

        internal void Update()
        {
            if (abcStream == null || StreamDescriptor == null)
                return;

            ClampTime();
            if (lastUpdateTime != CurrentTime || forceUpdate)
            {
                abcStream.SetVertexMotionScale(VertexMotionScale);

                if (abcStream.AbcUpdateBegin(StartTime + CurrentTime))
                {
                    lastUpdateTime = CurrentTime;
                    forceUpdate = false;
                    updateStarted = true;
                }
                else
                {
                    CloseStream();
                    LoadStream(false);
                }
            }
        }

        void LateUpdate()
        {
            // currentTime maybe updated after Update() by other GameObjects
            if (!updateStarted && lastUpdateTime != currentTime)
                Update();

            if (!updateStarted && abcStream != null)
            {
                // If the model did not move this frame, we need to clear the motion vectors to avoid post processing artefacts.
                abcStream.ClearMotionVectors();
                return;
            }

            updateStarted = false;
            if (abcStream != null)
            {
                abcStream.AbcUpdateEnd();
            }
        }

        void OnEnable()
        {
            if (abcStream == null)
                LoadStream(false);
        }

        void OnDisable()
        {
            CloseStream();
        }

        void OnApplicationQuit()
        {
            NativeMethods.aiCleanup();
        }

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (streamDescriptor != null && streamDescriptor.GetType() == typeof(AlembicStreamDescriptor))
            {
                streamSource = AlembicStreamSource.Internal;
            }
        }
    }
}
