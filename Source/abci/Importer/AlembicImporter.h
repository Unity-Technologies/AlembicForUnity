#pragma once

#include <cstdint>

#include <../Foundation/AbcNodes/CameraData.h>

class aiContext;
class aiTimeSampling;
class aiObject;
#ifdef abciImpl
class aiSchema;             // : aiObject
class aiSample;
class aiXformSample;        // : aiSample
class aiCameraSample;       // : aiSample
class aiPolyMeshSample;     // : aiSample
class aiSubDSample;         // : aiSample
class aiPointsSample;       // : aiSample
class aiCurves;
#else
// force make castable
using aiSchema         = void;
using aiSample         = void;
using aiXformSample    = void;
using aiCameraSample   = void;
using aiPolyMeshSample = void;
using aiPointsSample   = void;
#endif

class aiXform;    // : aiSchema
class aiCamera;   // : aiSchema
class aiPolyMesh; // : aiSchema
class aiSubD;     // : aiSchema
class aiPoints;   // : aiSchema
class aiProperty;

enum class NormalsMode
{
    //ReadFromFile,
    ComputeIfMissing = 1,
    AlwaysCompute = 2,
    //Ignore
};

enum class TangentsMode
{
    None,
    Compute,
};

enum class aiTimeSamplingType
{
    Uniform,
    Cyclic,
    Acyclic,
};

enum class aiTopologyVariance
{
    Constant,
    Homogeneous, // vertices are variant, topology is constant
    Heterogenous, // both vertices and topology are variant
};

enum class aiTopology
{
    Points,
    Lines,
    Triangles,
    Quads,
};

enum class aiPropertyType
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

    ScalarTypeBegin  = Bool,
    ScalarTypeEnd    = Float4x4,

    ArrayTypeBegin   = BoolArray,
    ArrayTypeEnd     = Float4x4Array,
};

struct aiConfig
{
    NormalsMode normals_mode = NormalsMode::ComputeIfMissing;
    TangentsMode tangents_mode = TangentsMode::None;
    float scale_factor = 1.0f;
    float aspect_ratio = -1.0f;
    float vertex_motion_scale = 1.0f;
    int split_unit = 0x7fffffff;
    bool swap_handedness = true;
    bool swap_face_winding = false;
    bool interpolate_samples = true;
    bool import_point_polygon = true;
    bool import_line_polygon = true;
    bool import_triangle_polygon = true;
};

struct aiXformData
{
    bool visibility = true;

    abcV3 translation = { 0.0f, 0.0f, 0.0f };
    abcV4 rotation = { 0.0f, 0.0f, 0.0f, 1.0f }; // quaternion
    abcV3 scale = { 1.0f, 1.0f, 1.0f };
    bool inherits = false;
};


struct aiMeshSummary
{
    aiTopologyVariance topology_variance = aiTopologyVariance::Constant;
    bool has_counts = false;
    bool has_indices = false;
    bool has_points = false;
    bool has_velocities = false;
    bool has_normals = false;
    bool has_tangents = false;
    bool has_uv0 = false;
    bool has_uv1 = false;
    bool has_rgba = false;
    bool has_rgb = false;
    bool constant_points = false;
    bool constant_velocities = false;
    bool constant_normals = false;
    bool constant_tangents = false;
    bool constant_uv0 = false;
    bool constant_uv1 = false;
    bool constant_rgba = false;
    bool constant_rgb = false;
};

struct aiMeshSampleSummary
{
    bool visibility = true;

    int split_count = 0;
    int submesh_count = 0;
    int vertex_count = 0;
    int index_count = 0;
    bool topology_changed = false;
};

struct aiMeshSplitSummary
{
    int submesh_count = 0;
    int submesh_offset = 0;
    int vertex_count = 0;
    int vertex_offset = 0;
    int index_count = 0;
    int index_offset = 0;
};

struct aiSubmeshSummary
{
    int split_index = 0;
    int submesh_index = 0; // submesh index in split
    int index_count = 0;
    aiTopology topology = aiTopology::Triangles;
};

struct aiPolyMeshData
{
    abcV3 *points = nullptr;
    abcV3 *velocities = nullptr;
    abcV3 *normals = nullptr;
    abcV4 *tangents = nullptr;
    abcV2 *uv0 = nullptr;
    abcV2 *uv1 = nullptr;
    abcV4 *rgba = nullptr;
    abcV4 *rgb = nullptr;
    int *indices = nullptr;

    int vertex_count = 0;
    int index_count = 0;

    abcV3 center = { 0.0f, 0.0f, 0.0f };
    abcV3 extents = { 0.0f, 0.0f, 0.0f };
};

struct aiSubmeshData
{
    int* indices = nullptr;
    void* faceset_names = nullptr; // 2-bytes character
};

struct aiCurvesSummary
{
    bool has_position = false;
    bool has_UVs = false;
    bool has_widths = false;
};

struct aiPointsSummary
{
    bool has_points = false;
    bool has_velocities = false;
    bool has_ids = false;
    bool constant_points = false;
    bool constant_velocities = false;
    bool constant_ids = false;
};

struct aiPointsSampleSummary
{
    int count = 0;
};

struct aiCurvesSampleSummary
{
    int positionCount = 0;
    int numVerticesCount = 0;
};

struct aiCurvesData
{
    bool        visibility = true;

    abcV3       *positions = nullptr;
    int32_t     *numVertices = nullptr;
    abcV2       *uvs = nullptr;
    float       *widths = nullptr;
    abcV3       *velocities = nullptr;
    int32_t     count = 0;

    //abcV3       center = { 0.0f, 0.0f, 0.0f };
    //abcV3       size = { 0.0f, 0.0f, 0.0f };
};

struct aiPointsData
{
    bool        visibility = true;

    abcV3       *points = nullptr;
    abcV3       *velocities = nullptr;
    uint32_t    *ids = nullptr;
    int32_t     count = 0;

    abcV3       center = { 0.0f, 0.0f, 0.0f };
    abcV3       size = { 0.0f, 0.0f, 0.0f };
};

struct aiPropertyData
{
    void *data = nullptr;
    int size = 0;
    aiPropertyType type = aiPropertyType::Unknown;

    aiPropertyData() {}
    aiPropertyData(void *d, int s, aiPropertyType t) : data(d), size(s), type(t) {}
};


abciAPI abcSampleSelector aiTimeToSampleSelector(double time);
abciAPI abcSampleSelector aiIndexToSampleSelector(int64_t index);
abciAPI void            aiCleanup();
abciAPI void            aiClearContextsWithPath(const char *path);

abciAPI aiContext*      aiContextCreate(int uid);
abciAPI void            aiContextDestroy(aiContext* ctx);
abciAPI bool            aiContextLoad(aiContext* ctx, const char *path);
abciAPI bool            aiContextGetIsHDF5(aiContext* ctx);
abciAPI void            aiContextSetConfig(aiContext* ctx, const aiConfig* conf);
abciAPI int             aiContextGetTimeSamplingCount(aiContext* ctx);
abciAPI aiTimeSampling* aiContextGetTimeSampling(aiContext* ctx, int i);
abciAPI void            aiContextGetTimeRange(aiContext* ctx, double *begin, double *end);
abciAPI aiObject*       aiContextGetTopObject(aiContext* ctx);
abciAPI void            aiContextUpdateSamples(aiContext* ctx, double time);

abciAPI int             aiTimeSamplingGetSampleCount(aiTimeSampling *self);
abciAPI double          aiTimeSamplingGetTime(aiTimeSampling *self, int index);
abciAPI void            aiTimeSamplingGetRange(aiTimeSampling *self, double *start, double *end);

abciAPI aiContext*      aiObjectGetContext(aiObject* obj);
abciAPI const char*     aiObjectGetName(aiObject* obj);
abciAPI const char*     aiObjectGetFullName(aiObject* obj);
abciAPI int             aiObjectGetNumChildren(aiObject* obj);
abciAPI aiObject*       aiObjectGetChild(aiObject* obj, int i);
abciAPI aiObject*       aiObjectGetParent(aiObject* obj);
abciAPI void            aiObjectSetEnabled(aiObject* obj, bool v);
abciAPI aiXform*        aiObjectAsXform(aiObject* obj);
abciAPI aiPolyMesh*     aiObjectAsPolyMesh(aiObject* obj);
abciAPI aiSubD*         aiObjectAsSubD(aiObject* obj);
abciAPI aiCamera*       aiObjectAsCamera(aiObject* obj);
abciAPI aiPoints*       aiObjectAsPoints(aiObject* obj);
abciAPI aiCurves*       aiObjectAsCurves(aiObject* obj);

abciAPI aiSample*       aiSchemaGetSample(aiSchema* schema);
abciAPI void            aiSchemaUpdateSample(aiSchema* schema, const abcSampleSelector *ss);
abciAPI void            aiSchemaSync(aiSchema* schema);
abciAPI bool            aiSchemaIsConstant(aiSchema* schema);
abciAPI bool            aiSchemaIsDataUpdated(aiSchema* schema);
abciAPI int             aiSchemaGetNumProperties(aiSchema* schema);
abciAPI aiProperty*     aiSchemaGetPropertyByIndex(aiSchema* schema, int i);
abciAPI aiProperty*     aiSchemaGetPropertyByName(aiSchema* schema, const char *name);

abciAPI void            aiXformGetData(aiXformSample* sample, aiXformData *dst);

abciAPI void            aiPolyMeshGetSummary(aiPolyMesh* schema, aiMeshSummary* dst);
abciAPI void            aiPolyMeshGetSampleSummary(aiPolyMeshSample* sample, aiMeshSampleSummary* dst);
abciAPI void            aiPolyMeshGetSplitSummaries(aiPolyMeshSample* sample, aiMeshSplitSummary *dst);
abciAPI void            aiPolyMeshGetSubmeshSummaries(aiPolyMeshSample* sample, aiSubmeshSummary* dst);
abciAPI void            aiPolyMeshFillVertexBuffer(aiPolyMeshSample* sample, aiPolyMeshData* vbs, aiSubmeshData* ibs);

abciAPI void            aiSubDGetSummary(aiSubD* schema, aiMeshSummary* dst);
abciAPI void            aiSubDGetSampleSummary(aiSubDSample* sample, aiMeshSampleSummary* dst);
abciAPI void            aiSubDGetSplitSummaries(aiSubDSample* sample, aiMeshSplitSummary *dst);
abciAPI void            aiSubDGetSubmeshSummaries(aiSubDSample* sample, aiSubmeshSummary* dst);
abciAPI void            aiSubDFillVertexBuffer(aiSubDSample* sample, aiPolyMeshData* vbs, aiSubmeshData* ibs);

abciAPI void            aiCameraGetData(aiCameraSample* sample, CameraData *dst);

abciAPI void            aiPointsGetSummary(aiPoints *schema, aiPointsSummary *dst);

abciAPI void            aiCurvesGetSummary(aiCurves *schema, aiCurvesSummary *dst);

abciAPI void            aiPointsSetSort(aiPoints* schema, bool v);
abciAPI void            aiPointsSetSortBasePosition(aiPoints* schema, abcV3 v);
abciAPI void            aiPointsGetSampleSummary(aiPointsSample* sample, aiPointsSampleSummary *dst);
abciAPI void            aiPointsFillData(aiPointsSample* sample, aiPointsData *dst);

abciAPI const char*     aiPropertyGetName(aiProperty* prop);
abciAPI aiPropertyType  aiPropertyGetType(aiProperty* prop);
abciAPI void            aiPropertyCopyData(aiProperty* prop, const abcSampleSelector *ss, aiPropertyData *dst);
