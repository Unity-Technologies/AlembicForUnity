using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace UnityEngine.Formats.Alembic.Sdk
{
    enum AspectRatioMode
    {
        CurrentResolution,
        DefaultResolution,
        CameraAperture
    };

    /// <summary>
    /// The normals processing mode on Alembic file import.
    /// </summary>
    public enum NormalsMode
    {
        //Import,
        /// <summary>
        /// Use Alembic file normals if they exist, otherwise compute them.
        /// </summary>
        CalculateIfMissing = 1,
        /// <summary>
        /// Ignore normals from the Alembic file and always recompute them.
        /// </summary>
        AlwaysCalculate = 2,
        //None
    }

    /// <summary>
    /// The tangents processing mode on Alembic file import.
    /// </summary>
    public enum TangentsMode
    {
        /// <summary>
        /// Do not compute tangents. As tangents are not stored in Alembic, there will be no tangent data.
        /// </summary>
        None,
        /// <summary>
        /// Compute and set mesh tangents. Requires normals and UV data.
        /// </summary>
        Calculate,
    }

    enum aiTopologyVariance
    {
        Constant,
        Homogeneous, // vertices are variant, topology is constant
        Heterogeneous, // both vertices and topology are variant
    }

    enum aiTopology
    {
        Points,
        Lines,
        Triangles,
        Quads,
    };

    enum aiPropertyType
    {
        Unknown,

        // scalar types
        Bool,
        Int,
        UInt,
        Float,
        Float2,
        Float3,
        Float4,
        Float4x4,

        // array types
        BoolArray,
        IntArray,
        UIntArray,
        FloatArray,
        Float2Array,
        Float3Array,
        Float4Array,
        Float4x4Array,

        ScalarTypeBegin = Bool,
        ScalarTypeEnd = Float4x4,

        ArrayTypeBegin = BoolArray,
        ArrayTypeEnd = Float4x4Array,
    };

    [StructLayout(LayoutKind.Sequential)]
    struct aiConfig
    {
        public NormalsMode normalsMode { get; set; }
        public TangentsMode tangentsMode { get; set; }
        public float scaleFactor { get; set; }
        public float aspectRatio { get; set; } // Broken/Unimplemented , not connected to any code path.
        public float vertexMotionScale { get; set; }
        public int splitUnit { get; set; }
        public Bool swapHandedness { get; set; }
        public Bool flipFaces { get; set; }
        public Bool interpolateSamples { get; set; }
        public Bool importPointPolygon { get; set; }
        public Bool importLinePolygon { get; set; }
        public Bool importTrianglePolygon { get; set; }

        public void SetDefaults()
        {
            normalsMode = NormalsMode.CalculateIfMissing;
            tangentsMode = TangentsMode.None;
            scaleFactor = 0.01f;
            aspectRatio = -1.0f;
            vertexMotionScale = 1.0f;
#if UNITY_2017_3_OR_NEWER
            splitUnit = 0x7fffffff;
#else
            splitUnit = 65000;
#endif
            swapHandedness = true;
            flipFaces = false;
            interpolateSamples = true;
            importPointPolygon = true;
            importLinePolygon = true;
            importTrianglePolygon = true;
        }
    }

    struct aiSampleSelector
    {
        public ulong requestedIndex { get; set; }
        public double requestedTime { get; set; }
        public int requestedTimeIndexType { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct aiMeshSummary
    {
        public aiTopologyVariance topologyVariance { get; set; }
        public Bool hasCounts { get; set; }
        public Bool hasIndsices { get; set; }
        public Bool hasPoints { get; set; }
        public Bool hasVelocities { get; set; }
        public Bool hasNormals { get; set; }
        public Bool hasTangents { get; set; }
        public Bool hasUV0 { get; set; }
        public Bool hasUV1 { get; set; }
        public Bool hasRgba { get; set; }
        public Bool hasRgb { get; set; }
        public Bool constantPoints { get; set; }
        public Bool constantVelocities { get; set; }
        public Bool constantNormals { get; set; }
        public Bool constantTangents { get; set; }
        public Bool constantUV0 { get; set; }
        public Bool constantUV1 { get; set; }
        public Bool constantRgba { get; set; }
        public Bool constantRgb { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct aiMeshSampleSummary
    {
        public Bool visibility { get; set; }

        public int splitCount { get; set; }
        public int submeshCount { get; set; }
        public int vertexCount { get; set; }
        public int indexCount { get; set; }
        public Bool topologyChanged { get; set; }
    }

    internal struct aiMeshSplitSummary
    {
        public int submeshCount { get; set; }
        public int submeshOffset { get; set; }
        public int vertexCount { get; set; }
        public int vertexOffset { get; set; }
        public int indexCount { get; set; }
        public int indexOffset { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct aiSubmeshSummary
    {
        public int splitIndex { get; set; }
        public int submeshIndex { get; set; }
        public int indexCount { get; set; }
        public aiTopology topology { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct aiPolyMeshData
    {
        public void* positions;
        public void* velocities;
        public void* normals;
        public void* tangents;
        public void* uv0;
        public void* uv1;
        public void* rgba;
        public void* rgb;

        public IntPtr indices;

        public int vertexCount;
        public int indexCount;

        public Vector3 center;
        public Vector3 extents;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct aiSubmeshData
    {
        public IntPtr indexes;
        public unsafe char* facesetNames;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct aiXformData
    {
        public Bool visibility { get; set; }

        public Vector3 translation { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 scale { get; set; }
        public Bool inherits { get; set; }
    }


    [StructLayout(LayoutKind.Sequential)]
    struct aiPointsSummary
    {
        public Bool hasVelocities { get; set; }
        public Bool hasIDs { get; set; }
        public Bool constantPoints { get; set; }
        public Bool constantVelocities { get; set; }
        public Bool constantIDs { get; set; }
    };

    struct aiPointsSampleSummary
    {
        public int count { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct aiPointsData
    {
        public Bool visibility;

        public IntPtr points;
        public IntPtr velocities;
        public IntPtr ids;
        public int count;

        public Vector3 boundsCenter;
        public Vector3 boundsExtents;
    }

    /*
     *
     */
    [StructLayout(LayoutKind.Sequential)]
    struct aiCurvesSummary
    {
        public Bool hasPositions { get; set; }
        public Bool hasUVs { get; set; }
        public Bool hasWidths { get; set; }
        // public Bool constantVelocities { get; set; }
        // public Bool constantIDs { get; set; }
    };

    struct aiCurvesSampleSummary
    {
        public int positionCount { get; set; }
        public int numVerticesCount { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct aiCurvesData
    {
        public Bool visibility;

        public IntPtr positions;
        public IntPtr numVertices;
        public IntPtr uvs;
        public IntPtr widths;
        public IntPtr velocities;
        public int count;
        /*
         public Vector3 boundsCenter;
         public Vector3 boundsExtents;*/
    }
    //

    [StructLayout(LayoutKind.Sequential)]
    struct aiPropertyData
    {
        public IntPtr data;
        public int size;
        public aiPropertyType type;
    }

    internal static class Abci
    {
#if UNITY_EDITOR_OSX
        internal const string Lib = "Packages/com.unity.formats.alembic/Runtime/Plugins/x86_64/abci.bundle/Contents/MacOS/abci";
#elif UNITY_EDITOR_LINUX
        internal const string Lib = "Packages/com.unity.formats.alembic/Runtime/Plugins/x86_64/abci.so";
#elif UNITY_EDITOR_WIN
        internal const string Lib = "Packages/com.unity.formats.alembic/Runtime/Plugins/x86_64/abci.dll";
#elif UNITY_STANDALONE
        internal const string Lib = "abci";
#endif
    }

    struct aiContext
    {
        [NativeDisableUnsafePtrRestriction]
        internal IntPtr self;
        public static implicit operator bool(aiContext v) { return v.self != IntPtr.Zero; }
        public static bool ToBool(aiContext v) { return v; }

        public static aiContext Create(int uid) { return NativeMethods.aiContextCreate(uid); }

        public static void DestroyByPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            NativeMethods.aiClearContextsWithPath(fullPath);
        }

        public void Destroy() { NativeMethods.aiContextDestroy(self); self = IntPtr.Zero; }

        public bool Load(string path)
        {
            var fullPath = Path.GetFullPath(path);
            return NativeMethods.aiContextLoad(self, fullPath);
        }

        public bool IsHDF5()
        {
            return NativeMethods.aiContextGetIsHDF5(self);
        }

        internal void SetConfig(ref aiConfig conf) { NativeMethods.aiContextSetConfig(self, ref conf); }
        public void UpdateSamples(double time) { NativeMethods.aiContextUpdateSamples(self, time); }

        internal aiObject topObject { get { return NativeMethods.aiContextGetTopObject(self); } }
        public int timeSamplingCount { get { return NativeMethods.aiContextGetTimeSamplingCount(self); } }
        public aiTimeSampling GetTimeSampling(int i) { return NativeMethods.aiContextGetTimeSampling(self, i); }
        internal void GetTimeRange(out double begin, out double end) { NativeMethods.aiContextGetTimeRange(self, out begin, out end); }
    }

    struct aiTimeSampling
    {
        internal IntPtr self;

        public int sampleCount { get { return NativeMethods.aiTimeSamplingGetSampleCount(self); } }
        public double GetTime(int index) { return NativeMethods.aiTimeSamplingGetTime(self, index); }
    }

    struct aiObject
    {
        internal IntPtr self;
        public static implicit operator bool(aiObject v) { return v.self != IntPtr.Zero; }
        public aiContext context { get { return NativeMethods.aiObjectGetContext(self); } }
        public string name { get { return Marshal.PtrToStringAnsi(NativeMethods.aiObjectGetName(self)); } }
        public string fullname { get { return Marshal.PtrToStringAnsi(NativeMethods.aiObjectGetFullName(self)); } }
        public aiObject parent { get { return NativeMethods.aiObjectGetParent(self); } }

        public void SetEnabled(bool value) { NativeMethods.aiObjectSetEnabled(self, value); }
        public int childCount { get { return NativeMethods.aiObjectGetNumChildren(self); } }
        public aiObject GetChild(int i) { return NativeMethods.aiObjectGetChild(self, i); }

        internal aiXform AsXform() { return NativeMethods.aiObjectAsXform(self); }
        internal aiCamera AsCamera() { return NativeMethods.aiObjectAsCamera(self); }
        internal aiPoints AsPoints() { return NativeMethods.aiObjectAsPoints(self); }
        internal aiCurves AsCurves() { return NativeMethods.aiObjectAsCurves(self); }
        internal aiPolyMesh AsPolyMesh() { return NativeMethods.aiObjectAsPolyMesh(self); }
        internal aiSubD AsSubD() { return NativeMethods.aiObjectAsSubD(self); }

        public void EachChild(Action<aiObject> act)
        {
            if (act == null)
            {
                return;
            }
            int n = childCount;
            for (int ci = 0; ci < n; ++ci)
                act.Invoke(GetChild(ci));
        }
    }

    struct aiSchema
    {
        public IntPtr self;

        public static implicit operator bool(aiSchema v) { return v.self != IntPtr.Zero; }
        public static explicit operator aiXform(aiSchema v) { var tmp = default(aiXform); tmp.self = v.self; return tmp; }
        public static explicit operator aiCamera(aiSchema v) { var tmp = default(aiCamera); tmp.self = v.self; return tmp; }
        public static explicit operator aiPolyMesh(aiSchema v) { var tmp = default(aiPolyMesh); tmp.self = v.self; return tmp; }
        public static explicit operator aiSubD(aiSchema v) { var tmp = default(aiSubD); tmp.self = v.self; return tmp; }
        public static explicit operator aiPoints(aiSchema v) { var tmp = default(aiPoints); tmp.self = v.self; return tmp; }
        public static explicit operator aiCurves(aiSchema v) { var tmp = default(aiCurves); tmp.self = v.self; return tmp; }
        public bool isDataUpdated { get { NativeMethods.aiSchemaSync(self); return NativeMethods.aiSchemaIsDataUpdated(self); } }
        public void UpdateSample(ref aiSampleSelector ss) { NativeMethods.aiSchemaUpdateSample(self, ref ss); }
    }

    [StructLayout(LayoutKind.Explicit)]
    struct aiXform
    {
        [FieldOffset(0)] public IntPtr self;
        [FieldOffset(0)] public aiSchema schema;
        public static implicit operator bool(aiXform v) { return v.self != IntPtr.Zero; }
        public static implicit operator aiSchema(aiXform v) { return v.schema; }

        public aiXformSample sample { get { return NativeMethods.aiXform.aiSchemaGetSample(self); } }
    }

    [StructLayout(LayoutKind.Explicit)]
    struct aiCamera
    {
        [FieldOffset(0)] public IntPtr self;
        [FieldOffset(0)] public aiSchema schema;
        public static implicit operator bool(aiCamera v) { return v.self != IntPtr.Zero; }
        public static implicit operator aiSchema(aiCamera v) { return v.schema; }

        public aiCameraSample sample { get { return NativeMethods.aiCamera.aiSchemaGetSample(self); } }
    }

    [StructLayout(LayoutKind.Explicit)]
    struct aiPolyMesh
    {
        [FieldOffset(0)] public IntPtr self;
        [FieldOffset(0)] public aiSchema schema;
        public static implicit operator bool(aiPolyMesh v) { return v.self != IntPtr.Zero; }
        public static implicit operator aiSchema(aiPolyMesh v) { return v.schema; }

        public aiPolyMeshSample sample { get { return NativeMethods.aiPolyMesh.aiSchemaGetSample(self); } }
        public void GetSummary(ref aiMeshSummary dst) { NativeMethods.aiPolyMeshGetSummary(self, ref dst); }
    }

    [StructLayout(LayoutKind.Explicit)]
    struct aiSubD
    {
        [FieldOffset(0)] public IntPtr self;
        [FieldOffset(0)] public aiSchema schema;
        public static implicit operator bool(aiSubD v) { return v.self != IntPtr.Zero; }
        public static implicit operator aiSchema(aiSubD v) { return v.schema; }

        public aiPolyMeshSample sample { get { return NativeMethods.aiSubD.aiSchemaGetSample(self); } }
        public void GetSummary(ref aiMeshSummary dst) { NativeMethods.aiSubDGetSummary(self, ref dst); }
    }


    [StructLayout(LayoutKind.Explicit)]
    struct aiPoints
    {
        [FieldOffset(0)] public IntPtr self;
        [FieldOffset(0)] public aiSchema schema;
        public static implicit operator bool(aiPoints v) { return v.self != IntPtr.Zero; }
        public static implicit operator aiSchema(aiPoints v) { return v.schema; }

        internal aiPointsSample sample { get { return NativeMethods.aiPoints.aiSchemaGetSample(self); } }
        public bool sort { set { NativeMethods.aiPointsSetSort(self, value); } }
        public Vector3 sortBasePosition { set { NativeMethods.aiPointsSetSortBasePosition(self, value); } }

        public void GetSummary(ref aiPointsSummary dst) { NativeMethods.aiPointsGetSummary(self, ref dst); }
    }

    [StructLayout(LayoutKind.Explicit)]
    struct aiCurves
    {
        [FieldOffset(0)] public IntPtr self;
        [FieldOffset(0)] public aiSchema schema;
        public static implicit operator bool(aiCurves v) { return v.self != IntPtr.Zero; }
        public static implicit operator aiSchema(aiCurves v) { return v.schema; }

        internal aiCurvesSample sample { get { return NativeMethods.aiCurves.aiSchemaGetSample(self); } }
        public bool sort { set { NativeMethods.aiPointsSetSort(self, value); } }
        public Vector3 sortBasePosition { set { NativeMethods.aiPointsSetSortBasePosition(self, value); } }

        public void GetSummary(ref aiCurvesSummary dst) { NativeMethods.aiCurvesGetSummary(self, ref dst); }
    }

    struct aiSample
    {
        public IntPtr self;
        public static implicit operator bool(aiSample v) { return v.self != IntPtr.Zero; }
        public static explicit operator aiXformSample(aiSample v) { aiXformSample tmp; tmp.self = v.self; return tmp; }
        public static explicit operator aiCameraSample(aiSample v) { aiCameraSample tmp; tmp.self = v.self; return tmp; }
        public static explicit operator aiPolyMeshSample(aiSample v) { aiPolyMeshSample tmp; tmp.self = v.self; return tmp; }
        public static explicit operator aiPointsSample(aiSample v) { aiPointsSample tmp; tmp.self = v.self; return tmp; }
    }

    struct aiXformSample
    {
        public IntPtr self;
        public static implicit operator bool(aiXformSample v) { return v.self != IntPtr.Zero; }
        public static implicit operator aiSample(aiXformSample v) { aiSample tmp; tmp.self = v.self; return tmp; }

        public void GetData(ref aiXformData dst) { NativeMethods.aiXformGetData(self, ref dst); }
    }

    struct aiCameraSample
    {
        public IntPtr self;
        public static implicit operator bool(aiCameraSample v) { return v.self != IntPtr.Zero; }
        public static implicit operator aiSample(aiCameraSample v) { aiSample tmp; tmp.self = v.self; return tmp; }

        public void GetData(ref CameraData dst) { NativeMethods.aiCameraGetData(self, ref dst); }
    }

    struct aiPolyMeshSample
    {
        [NativeDisableUnsafePtrRestriction]
        public IntPtr self;
        public static implicit operator bool(aiPolyMeshSample v) { return v.self != IntPtr.Zero; }
        public static implicit operator aiSample(aiPolyMeshSample v) { aiSample tmp; tmp.self = v.self; return tmp; }

        public void GetSummary(ref aiMeshSampleSummary dst) { NativeMethods.aiPolyMeshGetSampleSummary(self, ref dst); }

        public void GetSplitSummaries(NativeArray<aiMeshSplitSummary> dst)
        {
            unsafe
            {
                NativeMethods.aiPolyMeshGetSplitSummaries(self, new IntPtr(dst.GetUnsafePtr()));
            }
        }

        public void GetSubmeshSummaries(NativeArray<aiSubmeshSummary> dst)
        {
            unsafe
            {
                NativeMethods.aiPolyMeshGetSubmeshSummaries(self, new IntPtr(dst.GetUnsafePtr()));
            }
        }

        internal void FillVertexBuffer(NativeArray<aiPolyMeshData> vbs, NativeArray<aiSubmeshData> ibs)
        {
            unsafe
            {
                NativeMethods.aiPolyMeshFillVertexBuffer(self, new IntPtr(vbs.GetUnsafePtr()), new IntPtr(ibs.GetUnsafePtr()));
            }
        }
    }

    struct aiPointsSample
    {
        public IntPtr self;
        public static implicit operator bool(aiPointsSample v) { return v.self != IntPtr.Zero; }
        public static implicit operator aiSample(aiPointsSample v) { aiSample tmp; tmp.self = v.self; return tmp; }

        public void GetSummary(ref aiPointsSampleSummary dst) { NativeMethods.aiPointsGetSampleSummary(self, ref dst); }
        public void FillData(PinnedList<aiPointsData> dst) { NativeMethods.aiPointsFillData(self, dst); }
    }

    struct aiCurvesSample
    {
        public IntPtr self;
        public static implicit operator bool(aiCurvesSample v) { return v.self != IntPtr.Zero; }
        public static implicit operator aiSample(aiCurvesSample v) { aiSample tmp; tmp.self = v.self; return tmp; }

        public void GetSummary(ref aiCurvesSampleSummary dst) { NativeMethods.aiCurvesGetSampleSummary(self, ref dst); }
        public void FillData(PinnedList<aiCurvesData> dst) { NativeMethods.aiCurvesFillData(self, dst); }
    }

    struct aiProperty
    {
        public IntPtr self;
        public static implicit operator bool(aiProperty v) { return v.self != IntPtr.Zero; }
    }
}
