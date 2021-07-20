#pragma once
#include "aiMeshOps.h"

namespace
{
    void copyCharsWithStride(void* target, const std::string& source, size_t maxLength)
    {
        const auto span = std::min(maxLength, source.size());
        for (int i = 0; i < span; ++i)
        {
            *(static_cast<char*>(target) + 2 * i) = source[i];
            *(static_cast<char*>(target) + 2 * i + 1) = '\0';
        }
    }


}
using abcFaceSetSchemas = std::vector<AbcGeom::IFaceSetSchema>;
using abcFaceSetSamples = std::vector<AbcGeom::IFaceSetSchema::Sample>;

struct aiMeshSummaryInternal : public aiMeshSummary
{
    bool has_velocities_prop = false;
    bool has_normals_prop = false;
    bool has_uv0_prop = false;
    bool has_uv1_prop = false;
    bool has_rgba_prop = false;
    bool has_rgb_prop = false;

    bool interpolate_points = false;
    bool interpolate_normals = false;
    bool interpolate_uv0 = false;
    bool interpolate_uv1 = false;
    bool interpolate_rgba = false;
    bool interpolate_rgb = false;
    bool compute_normals = false;
    bool compute_tangents = false;
    bool compute_velocities = false;
};

class aiMeshTopology
{
public:
    aiMeshTopology();
    void clear();

    int getSplitCount() const;
    int getVertexCount() const;
    int getIndexCount() const;

    int getSplitVertexCount(int split_index) const;
    int getSubmeshCount() const;
    int getSubmeshCount(int split_index) const;

public:
    Abc::Int32ArraySamplePtr m_indices_sp;
    Abc::Int32ArraySamplePtr m_counts_sp;
    abcFaceSetSamples m_faceset_sps;
    RawVector<int> m_material_ids;
    std::vector<std::string> m_faceset_names;

    MeshRefiner m_refiner;
    RawVector<int> m_remap_points;
    RawVector<int> m_remap_normals;
    RawVector<int> m_remap_uv0, m_remap_uv1;
    RawVector<int> m_remap_rgba;
    RawVector<int> m_remap_rgb;

    int m_vertex_count = 0;
    int m_index_count = 0; // triangulated
};
using TopologyPtr = std::shared_ptr<aiMeshTopology>;

template <typename T>
class aiMeshSample : public aiSample
{
public:
    aiMeshSample(T* schema, TopologyPtr);
    ~aiMeshSample();
    void reset();

    void getSummary(aiMeshSampleSummary &dst) const;
    void getSplitSummaries(aiMeshSplitSummary  *dst) const;
    void getSubmeshSummaries(aiSubmeshSummary *dst) const;

    void fillSplitVertices(int split_index, aiPolyMeshData &data) const;
    void fillSubmeshIndices(int submesh_index, aiSubmeshData &data) const;
    void fillVertexBuffer(aiPolyMeshData* vbs, aiSubmeshData* ibs);

public:
    Abc::P3fArraySamplePtr m_points_sp, m_points_sp2;
    Abc::V3fArraySamplePtr m_velocities_sp;
    AbcGeom::IN3fGeomParam::Sample m_normals_sp, m_normals_sp2;
    AbcGeom::IV2fGeomParam::Sample m_uv0_sp, m_uv0_sp2;
    AbcGeom::IV2fGeomParam::Sample m_uv1_sp, m_uv1_sp2;
    AbcGeom::IC4fGeomParam::Sample m_rgba_sp, m_rgba_sp2;
    AbcGeom::IC3fGeomParam::Sample m_rgb_sp, m_rgb_sp2;
    Abc::Box3d m_bounds;

    IArray<abcV3> m_points_ref;
    IArray<abcV3> m_velocities_ref;
    IArray<abcV2> m_uv0_ref, m_uv1_ref;
    IArray<abcV3> m_normals_ref;
    IArray<abcV4> m_tangents_ref;
    IArray<abcC4> m_rgba_ref;
    IArray<abcC3> m_rgb_ref;

    RawVector<abcV3> m_points, m_points2, m_points_int, m_points_prev;
    RawVector<abcV3> m_velocities;
    RawVector<abcV2> m_uv0, m_uv02, m_uv0_int;
    RawVector<abcV2> m_uv1, m_uv12, m_uv1_int;
    RawVector<abcV3> m_normals, m_normals2, m_normals_int;
    RawVector<abcV4> m_tangents;
    RawVector<abcC4> m_rgba, m_rgba2, m_rgba_int;
    RawVector<abcC3> m_rgb, m_rgb2, m_rgb_int;

    TopologyPtr m_topology;
    bool m_topology_changed = false;

    std::future<void> m_async_copy;
};

template <typename T, typename U>
class aiMeshSchema : public aiTSchema<T>
{
public:
    aiMeshSchema(aiObject* parent, const abcObject& abc);
    ~aiMeshSchema();
    void updateSummary();
    const aiMeshSummaryInternal& getSummary() const;

    void readSampleBody(U& sample, uint64_t idx) override;
    void cookSampleBody(U& sample) override;

    void onTopologyChange(U& sample);
    void onTopologyDetermined();

public:

    RawVector<abcV3> m_constant_points;
    RawVector<abcV3> m_constant_velocities;
    RawVector<abcV3> m_constant_normals;
    RawVector<abcV4> m_constant_tangents;
    RawVector<abcV2> m_constant_uv0;
    RawVector<abcV2> m_constant_uv1;
    RawVector<abcC4> m_constant_rgba;
    RawVector<abcC3> m_constant_rgb;

protected:
    virtual AbcGeom::IN3fGeomParam readNormalsParam();
    aiMeshSummaryInternal m_summary;
    AbcGeom::IV2fGeomParam m_uv1_param;
    AbcGeom::IC4fGeomParam m_rgba_param;
    AbcGeom::IC3fGeomParam m_rgb_param;

    TopologyPtr m_shared_topology;
    abcFaceSetSchemas m_facesets;
    bool m_varying_topology = false;
};

template <typename T, typename U>
AbcGeom::IN3fGeomParam aiMeshSchema<T, U>::readNormalsParam()
{
    auto& summary = m_summary;
    auto param = this->m_schema.getNormalsParam();
    return param;
}

template<typename T, typename U>
inline aiMeshSchema<T, U>::aiMeshSchema(aiObject * parent, const abcObject & abc)
    : aiTSchema<T>(parent, abc)
{
    // find vertex color and additional uv params
    auto geom_params = this->m_schema.getArbGeomParams();
    if (geom_params.valid())
    {
        size_t num_geom_params = geom_params.getNumProperties();
        for (size_t i = 0; i < num_geom_params; ++i)
        {
            auto& header = geom_params.getPropertyHeader(i);

            // vertex color
            if (AbcGeom::IC4fGeomParam::matches(header))
            {
                m_rgba_param = AbcGeom::IC4fGeomParam(geom_params, header.getName());
            }
            if (AbcGeom::IC3fGeomParam::matches(header)) {

                m_rgb_param = AbcGeom::IC3fGeomParam(geom_params, header.getName());
            }

            // uv
            if (AbcGeom::IV2fGeomParam::matches(header))
            {
                m_uv1_param = AbcGeom::IV2fGeomParam(geom_params, header.getName());
            }
        }
    }

    // find face set schema in children
    size_t num_children = this->getAbcObject().getNumChildren();
    for (size_t i = 0; i < num_children; ++i)
    {
        auto child = this->getAbcObject().getChild(i);
        if (child.valid() && AbcGeom::IFaceSetSchema::matches(child.getMetaData()))
        {
            auto so = Abc::ISchemaObject<AbcGeom::IFaceSetSchema>(child, Abc::kWrapExisting);
            auto& fs = so.getSchema();
            if (fs.valid() && fs.getNumSamples() > 0)
                m_facesets.push_back(fs);
        }
    }

    updateSummary();
}

template<class T>
static inline void copy_or_clear(T* dst, const IArray<T>& src, const MeshRefiner::Split& split)
{
    if (dst)
    {
        if (!src.empty())
            src.copy_to(dst, split.vertex_count, split.vertex_offset);
        else
            memset(dst, 0, split.vertex_count * sizeof(T));
    }
}

template<class T1, class T2>
static inline void copy_or_clear_3_to_4(T1* dst, const IArray<T2>& src, const MeshRefiner::Split& split)
{
    if (dst)
    {
        if (!src.empty()) {
            std::vector<T1> abc4(split.vertex_count);
            std::transform(src.begin(), src.end(), abc4.begin(), [](const T2& c){ return T1{c.x, c.y, c.z, 1.f}; });
            memcpy(dst, abc4.data() + split.vertex_offset, sizeof(T1) * split.vertex_count);
        } else {
            memset(dst, 0, split.vertex_count * sizeof(T1));
        }
    }
}

template <typename T, typename U>
void aiMeshSchema<T, U>::updateSummary()
{
    m_varying_topology = (this->m_schema.getTopologyVariance() == AbcGeom::kHeterogeneousTopology);
    auto& summary = m_summary;
    auto& config = this->getConfig();

    summary = {};
    this->m_constant = this->m_schema.isConstant();

    // m_schema.isConstant() doesn't consider custom properties. check them
    if (this->m_visibility_prop.valid() && !this->m_visibility_prop.isConstant())
    {
        this->m_constant = false;
    }

    summary.topology_variance = (aiTopologyVariance)this->m_schema.getTopologyVariance();

    // counts
    {
        auto prop = this->m_schema.getFaceCountsProperty();
        if (prop.valid() && prop.getNumSamples() > 0)
        {
            summary.has_counts = true;
        }
    }

    // indices
    {
        auto prop = this->m_schema.getFaceIndicesProperty();
        if (prop.valid() && prop.getNumSamples() > 0)
        {
            summary.has_indices = true;
        }
    }

    // points
    {
        auto prop = this->m_schema.getPositionsProperty();
        if (prop.valid() && prop.getNumSamples() > 0 )
        {
            Alembic::Util::Dimensions dim;
            prop.getDimensions(dim);
            if (dim.numPoints() > 0)
            {
                summary.has_points = true;
                summary.constant_points = prop.isConstant();
                if (!summary.constant_points)
                    this->m_constant = false;
            }
        }
    }

    // normals
    {
        auto param = readNormalsParam();
        if (param.valid() && param.getNumSamples() > 0 && param.getScope() != AbcGeom::kUnknownScope)
        {
            summary.has_normals_prop = true;
            summary.has_normals = true;
            summary.constant_normals = param.isConstant() && config.normals_mode != NormalsMode::AlwaysCompute;
            if (!summary.constant_normals)
                this->m_constant = false;
        }
    }

    // uv0
    {
        auto param = this->m_schema.getUVsParam();
        if (param.valid() && param.getNumSamples() > 0 && param.getScope() != AbcGeom::kUnknownScope)
        {
            summary.has_uv0_prop = true;
            summary.has_uv0 = true;
            summary.constant_uv0 = param.isConstant();
            if (!summary.constant_uv0)
                this->m_constant = false;
        }
    }

    // uv1
    {
        auto& param = m_uv1_param;
        if (param.valid() && param.getNumSamples() > 0 && param.getScope() != AbcGeom::kUnknownScope)
        {
            summary.has_uv1_prop = true;
            summary.has_uv1 = true;
            summary.constant_uv1 = param.isConstant();
            if (!summary.constant_uv1)
                this->m_constant = false;
        }
    }

    // colors
    {
        auto& param = m_rgba_param;
        if (param.valid() && param.getNumSamples() > 0 && param.getScope() != AbcGeom::kUnknownScope)
        {
            summary.has_rgba_prop = true;
            summary.has_rgba = true;
            summary.constant_rgba = param.isConstant();
            if (!summary.constant_rgba)
                this->m_constant = false;
        }
    }

    // rgb colors
    {
        auto& param = m_rgb_param;
        if (param.valid() && param.getNumSamples() > 0 && param.getScope() != AbcGeom::kUnknownScope)
        {
            summary.has_rgb_prop = true;
            summary.has_rgb = true;
            summary.constant_rgb = param.isConstant();
            if (!summary.constant_rgb)
                this->m_constant = false;
        }
    }

    bool interpolate = config.interpolate_samples && !this->m_constant && !m_varying_topology;
    summary.interpolate_points = interpolate && !summary.constant_points;

    // velocities
    if (interpolate)
    {
        summary.has_velocities = true;
        summary.compute_velocities = true;
    }
    else
    {
        auto velocities = this->m_schema.getVelocitiesProperty();
        if (velocities.valid() && velocities.getNumSamples() > 0)
        {
            summary.has_velocities_prop = true;
            summary.has_velocities = true;
            summary.constant_velocities = velocities.isConstant();
        }
    }

    // normals - interpolate or compute?
    if (!summary.constant_normals)
    {
        if (summary.has_normals && config.normals_mode != NormalsMode::AlwaysCompute)
        {
            summary.interpolate_normals = interpolate;
        }
        else
        {
            summary.compute_normals =
                config.normals_mode <= NormalsMode::AlwaysCompute ||
                (!summary.has_normals && config.normals_mode == NormalsMode::ComputeIfMissing);
            if (summary.compute_normals)
            {
                summary.has_normals = true;
                summary.constant_normals = summary.constant_points;
            }
        }
    }

    // tangents
    if (config.tangents_mode == TangentsMode::Compute && summary.has_normals && summary.has_uv0)
    {
        summary.has_tangents = true;
        summary.compute_tangents = true;
        if (summary.constant_points && summary.constant_normals && summary.constant_uv0)
        {
            summary.constant_tangents = true;
        }
    }

    if (interpolate)
    {
        if (summary.has_uv0_prop && !summary.constant_uv0)
            summary.interpolate_uv0 = true;
        if (summary.has_uv1_prop && !summary.constant_uv1)
            summary.interpolate_uv1 = true;
        if (summary.has_rgba_prop && !summary.constant_rgba)
            summary.interpolate_rgba = true;
        if (summary.has_rgb_prop && !summary.constant_rgb)
            summary.interpolate_rgb = true;
    }
}

template <typename T, typename U>
const aiMeshSummaryInternal& aiMeshSchema<T, U>::getSummary() const
{
    return m_summary;
}

template <typename T, typename U>
void aiMeshSchema<T, U>::readSampleBody(U& sample, uint64_t idx)
{
    auto ss = aiIndexToSampleSelector(idx);
    auto ss2 = aiIndexToSampleSelector(idx + 1);

    auto& topology = *sample.m_topology;
    auto& refiner = topology.m_refiner;
    auto& summary = m_summary;

    bool topology_changed = m_varying_topology || this->m_force_update_local;

    if (topology_changed)
        topology.clear();

    // topology
    if (summary.has_counts && (!topology.m_counts_sp || topology_changed))
    {
        this->m_schema.getFaceCountsProperty().get(topology.m_counts_sp, ss);
        topology_changed = true;
    }
    if (summary.has_indices && (!topology.m_indices_sp || topology_changed))
    {
        this->m_schema.getFaceIndicesProperty().get(topology.m_indices_sp, ss);
        topology_changed = true;
    }

    // face sets
    if (!m_facesets.empty() && topology_changed)
    {
        topology.m_faceset_sps.resize(m_facesets.size());
        topology.m_faceset_names.resize(m_facesets.size());
        for (size_t fi = 0; fi < m_facesets.size(); ++fi)
        {
            m_facesets[fi].get(topology.m_faceset_sps[fi], ss);
            topology.m_faceset_names[fi] = m_facesets[fi].getObject().getName();
        }
    }

    // points
    if (summary.has_points && m_constant_points.empty())
    {
        auto param = this->m_schema.getPositionsProperty();
        param.get(sample.m_points_sp, ss);
        if (summary.interpolate_points)
        {
            param.get(sample.m_points_sp2, ss2);
        }
        else
        {
            if (summary.has_velocities_prop)
            {
                this->m_schema.getVelocitiesProperty().get(sample.m_velocities_sp, ss);
            }
        }
    }

    // normals
    if (m_constant_normals.empty() && summary.has_normals_prop && !summary.compute_normals)
    {
        auto param = readNormalsParam();
        param.getIndexed(sample.m_normals_sp, ss);
        if (summary.interpolate_normals)
        {
            param.getIndexed(sample.m_normals_sp2, ss2);
        }
    }

    // uv0
    if (m_constant_uv0.empty() && summary.has_uv0_prop)
    {
        auto param = this->m_schema.getUVsParam();
        param.getIndexed(sample.m_uv0_sp, ss);
        if (summary.interpolate_uv0)
        {
            param.getIndexed(sample.m_uv0_sp2, ss2);
        }
    }

    // uv1
    if (m_constant_uv1.empty() && summary.has_uv1_prop)
    {
        m_uv1_param.getIndexed(sample.m_uv1_sp, ss);
        if (summary.interpolate_uv1)
        {
            m_uv1_param.getIndexed(sample.m_uv1_sp2, ss2);
        }
    }

    // colors
    if (m_constant_rgba.empty() && summary.has_rgba_prop)
    {
        m_rgba_param.getIndexed(sample.m_rgba_sp, ss);
        if (summary.interpolate_rgba)
        {
            m_rgba_param.getIndexed(sample.m_rgba_sp2, ss2);
        }
    }

    // rgb
    if (m_constant_rgb.empty() && summary.has_rgb_prop)
    {
        m_rgb_param.getIndexed(sample.m_rgb_sp, ss);
        if (summary.interpolate_rgb)
        {
            m_rgb_param.getIndexed(sample.m_rgb_sp2, ss2);
        }
    }

    auto bounds_param = this->m_schema.getSelfBoundsProperty();
    if (bounds_param && bounds_param.getNumSamples() > 0)
        bounds_param.get(sample.m_bounds, ss);

    sample.m_topology_changed = topology_changed;
}

template <typename T, typename U>
void aiMeshSchema<T, U>::cookSampleBody(U& sample)
{
    auto& topology = *sample.m_topology;
    auto& refiner = topology.m_refiner;
    auto& config = this->getConfig();
    auto& summary = getSummary();

    // interpolation can't work with varying topology
    if (m_varying_topology && !this->m_sample_index_changed)
        return;

    if (sample.m_topology_changed)
    {
        onTopologyChange(sample);
    }
    else if (this->m_sample_index_changed)
    {
        onTopologyDetermined();

        // make remapped vertex buffer
        if (!m_constant_points.empty())
        {
            sample.m_points_ref = m_constant_points;
        }
        else
        {
            Remap(sample.m_points, *sample.m_points_sp, topology.m_remap_points);
            if (config.swap_handedness)
                SwapHandedness(sample.m_points.data(), (int)sample.m_points.size());
            if (config.scale_factor != 1.0f)
                ApplyScale(sample.m_points.data(), (int)sample.m_points.size(), config.scale_factor);
            sample.m_points_ref = sample.m_points;
        }

        if (!m_constant_normals.empty())
        {
            sample.m_normals_ref = m_constant_normals;
        }
        else if (!summary.compute_normals && summary.has_normals_prop)
        {
            Remap(sample.m_normals, *sample.m_normals_sp.getVals(), topology.m_remap_normals);
            if (config.swap_handedness)
                SwapHandedness(sample.m_normals.data(), (int)sample.m_normals.size());
            sample.m_normals_ref = sample.m_normals;
        }

        if (!m_constant_tangents.empty())
        {
            sample.m_tangents_ref = m_constant_tangents;
        }

        if (!m_constant_uv0.empty())
        {
            sample.m_uv0_ref = m_constant_uv0;
        }
        else if (summary.has_uv0_prop)
        {
            Remap(sample.m_uv0, *sample.m_uv0_sp.getVals(), topology.m_remap_uv0);
            sample.m_uv0_ref = sample.m_uv0;
        }

        if (!m_constant_uv1.empty())
        {
            sample.m_uv1_ref = m_constant_uv1;
        }
        else if (summary.has_uv1_prop)
        {
            Remap(sample.m_uv1, *sample.m_uv1_sp.getVals(), topology.m_remap_uv1);
            sample.m_uv1_ref = sample.m_uv1;
        }

        if (!m_constant_rgba.empty())
        {
            sample.m_rgba_ref = m_constant_rgba;
        }
        else if (summary.has_rgba_prop)
        {
            Remap(sample.m_rgba, *sample.m_rgba_sp.getVals(), topology.m_remap_rgba);
            sample.m_rgba_ref = sample.m_rgba;
        }

        if (!m_constant_rgb.empty())
        {
            sample.m_rgb_ref = m_constant_rgb;
        }
        else if (summary.has_rgb_prop)
        {
            Remap(sample.m_rgb, *sample.m_rgb_sp.getVals(), topology.m_remap_rgb);
            sample.m_rgb_ref = sample.m_rgb;
        }
    }
    else
    {
        onTopologyDetermined();
    }

    if (this->m_sample_index_changed)
    {
        // both in the case of topology changed or sample index changed

        if (summary.interpolate_points)
        {
            Remap(sample.m_points2, *sample.m_points_sp2, topology.m_remap_points);
            if (config.swap_handedness)
                SwapHandedness(sample.m_points2.data(), (int)sample.m_points2.size());
            if (config.scale_factor != 1.0f)
                ApplyScale(sample.m_points2.data(), (int)sample.m_points2.size(), config.scale_factor);
        }

        if (summary.interpolate_normals)
        {
            Remap(sample.m_normals2, *sample.m_normals_sp2.getVals(), topology.m_remap_normals);
            if (config.swap_handedness)
                SwapHandedness(sample.m_normals2.data(), (int)sample.m_normals2.size());
        }

        if (summary.interpolate_uv0)
        {
            Remap(sample.m_uv02, *sample.m_uv0_sp2.getVals(), topology.m_remap_uv0);
        }

        if (summary.interpolate_uv1)
        {
            Remap(sample.m_uv12, *sample.m_uv1_sp2.getVals(), topology.m_remap_uv1);
        }

        if (summary.interpolate_rgba)
        {
            Remap(sample.m_rgba2, *sample.m_rgba_sp2.getVals(), topology.m_remap_rgba);
        }

        if (summary.interpolate_rgb)
        {
            Remap(sample.m_rgb2, *sample.m_rgb_sp2.getVals(), topology.m_remap_rgb);
        }

        if (!m_constant_velocities.empty())
        {
            sample.m_velocities_ref = m_constant_velocities;
        }
        else if (!summary.compute_velocities && summary.has_velocities_prop)
        {
            auto& dst = summary.constant_velocities ? m_constant_velocities : sample.m_velocities;
            Remap(dst, *sample.m_velocities_sp, topology.m_remap_points);
            if (config.swap_handedness)
                SwapHandedness(dst.data(), (int)dst.size());
            if (config.scale_factor != 1.0f)
                ApplyScale(dst.data(), (int)dst.size(), config.scale_factor);
            sample.m_velocities_ref = dst;
        }
    }

    // interpolate or compute data

    // points
    if (summary.interpolate_points)
    {
        if (summary.compute_velocities)
            sample.m_points_int.swap(sample.m_points_prev);

        Lerp(sample.m_points_int, sample.m_points, sample.m_points2, this->m_current_time_offset);
        sample.m_points_ref = sample.m_points_int;

        if (summary.compute_velocities)
        {
            sample.m_velocities.resize_discard(sample.m_points.size());
            if (sample.m_points_int.size() == sample.m_points_prev.size())
            {
                GenerateVelocities(sample.m_velocities.data(), sample.m_points_int.data(), sample.m_points_prev.data(),
                    (int)sample.m_points_int.size(), config.vertex_motion_scale);
            }
            else
            {
                sample.m_velocities.zeroclear();
            }
            sample.m_velocities_ref = sample.m_velocities;
        }
    }

    // normals
    if (!m_constant_normals.empty())
    {
        // do nothing
    }
    else if (summary.interpolate_normals)
    {
        Lerp(sample.m_normals_int, sample.m_normals, sample.m_normals2, (float)this->m_current_time_offset);
        Normalize(sample.m_normals_int.data(), (int)sample.m_normals.size());
        sample.m_normals_ref = sample.m_normals_int;
    }
    else if (summary.compute_normals && (this->m_sample_index_changed || summary.interpolate_points))
    {
        if (sample.m_points_ref.empty())
        {
            DebugError("something is wrong!!");
            sample.m_normals_ref.reset();
        }
        else
        {
            const auto &indices = topology.m_refiner.new_indices_tri;
            sample.m_normals.resize_discard(sample.m_points_ref.size());
            GeneratePointNormals(topology.m_counts_sp->get(), topology.m_indices_sp->get(), sample.m_points_sp->get(),
                    sample.m_normals.data(), topology.m_remap_points.data(),
                    static_cast<int>(topology.m_counts_sp->size()),
                    static_cast<int>(topology.m_remap_points.size()),
                    static_cast<int>(sample.m_points_sp->size()));
            sample.m_normals_ref = sample.m_normals;
        }
    }

    // tangents
    if (!m_constant_tangents.empty())
    {
        // do nothing
    }
    else if (summary.compute_tangents && (this->m_sample_index_changed || summary.interpolate_points || summary.interpolate_normals))
    {
        if (sample.m_points_ref.empty() || sample.m_uv0_ref.empty() || sample.m_normals_ref.empty())
        {
            DebugError("something is wrong!!");
            sample.m_tangents_ref.reset();
        }
        else
        {
            const auto &indices = topology.m_refiner.new_indices_tri;
            sample.m_tangents.resize_discard(sample.m_points_ref.size());
            GenerateTangents(sample.m_tangents.data(), sample.m_points_ref.data(), sample.m_uv0_ref.data(), sample.m_normals_ref.data(),
                indices.data(), (int)sample.m_points_ref.size(), (int)indices.size() / 3);
            sample.m_tangents_ref = sample.m_tangents;
        }
    }

    // uv0
    if (summary.interpolate_uv0)
    {
        Lerp(sample.m_uv0_int, sample.m_uv0, sample.m_uv02, this->m_current_time_offset);
        sample.m_uv0_ref = sample.m_uv0_int;
    }

    // uv1
    if (summary.interpolate_uv1)
    {
        Lerp(sample.m_uv1_int, sample.m_uv1, sample.m_uv12, this->m_current_time_offset);
        sample.m_uv1_ref = sample.m_uv1_int;
    }

    // colors
    if (summary.interpolate_rgba)
    {
        Lerp(sample.m_rgba_int, sample.m_rgba, sample.m_rgba2, this->m_current_time_offset);
        sample.m_rgba_ref = sample.m_rgba_int;
    }

    // rgb
    if (summary.interpolate_rgb)
    {
        Lerp(sample.m_rgb_int, sample.m_rgb, sample.m_rgb2, this->m_current_time_offset);
        sample.m_rgb_ref = sample.m_rgb_int;
    }
}

template <typename T, typename U>
void aiMeshSchema<T, U>::onTopologyChange(U& sample)
{
    auto& summary = m_summary;
    auto& topology = *sample.m_topology;
    auto& refiner = topology.m_refiner;
    auto& config = this->getConfig();

    if (!topology.m_counts_sp || !topology.m_indices_sp || !sample.m_points_sp)
        return;

    refiner.clear();
    refiner.split_unit = config.split_unit;
    refiner.gen_points = config.import_point_polygon;
    refiner.gen_lines = config.import_line_polygon;
    refiner.gen_triangles = config.import_triangle_polygon;

    refiner.counts = { topology.m_counts_sp->get(), topology.m_counts_sp->size() };
    refiner.indices = { topology.m_indices_sp->get(), topology.m_indices_sp->size() };
    refiner.points = { (float3*)sample.m_points_sp->get(), sample.m_points_sp->size() };

    bool has_valid_normals = false;
    bool has_valid_uv0 = false;
    bool has_valid_uv1 = false;
    bool has_valid_rgba = false;
    bool has_valid_rgb = false;

    if (sample.m_normals_sp.valid() && !summary.compute_normals)
    {
        IArray<abcV3> src{ sample.m_normals_sp.getVals()->get(), sample.m_normals_sp.getVals()->size() };
        auto& dst = summary.constant_normals ? m_constant_normals : sample.m_normals;

        has_valid_normals = true;
        if (sample.m_normals_sp.isIndexed() && sample.m_normals_sp.getIndices()->size() == refiner.indices.size())
        {
            IArray<int> indices{ (int*)sample.m_normals_sp.getIndices()->get(), sample.m_normals_sp.getIndices()->size() };
            refiner.template addIndexedAttribute<abcV3>(src, indices, dst, topology.m_remap_normals);
        }
        else if (src.size() == refiner.indices.size())
        {
            refiner.template addExpandedAttribute<abcV3>(src, dst, topology.m_remap_normals);
        }
        else if (src.size() == refiner.points.size())
        {
            refiner.template addIndexedAttribute<abcV3>(src, refiner.indices, dst, topology.m_remap_normals);
        }
        else
        {
            DebugLog("Invalid attribute");
            has_valid_normals = false;
        }
    }

    if (sample.m_uv0_sp.valid())
    {
        IArray<abcV2> src{ sample.m_uv0_sp.getVals()->get(), sample.m_uv0_sp.getVals()->size() };
        auto& dst = summary.constant_uv0 ? m_constant_uv0 : sample.m_uv0;

        has_valid_uv0 = true;
        if (sample.m_uv0_sp.isIndexed() && sample.m_uv0_sp.getIndices()->size() == refiner.indices.size())
        {
            IArray<int> indices{ (int*)sample.m_uv0_sp.getIndices()->get(), sample.m_uv0_sp.getIndices()->size() };
            refiner.template addIndexedAttribute<abcV2>(src, indices, dst, topology.m_remap_uv0);
        }
        else if (src.size() == refiner.indices.size())
        {
            refiner.template addExpandedAttribute<abcV2>(src, dst, topology.m_remap_uv0);
        }
        else if (src.size() == refiner.points.size())
        {
            refiner.template addIndexedAttribute<abcV2>(src, refiner.indices, dst, topology.m_remap_uv0);
        }
        else
        {
            DebugLog("Invalid attribute");
            has_valid_uv0 = false;
        }
    }

    if (sample.m_uv1_sp.valid())
    {
        IArray<abcV2> src{ sample.m_uv1_sp.getVals()->get(), sample.m_uv1_sp.getVals()->size() };
        auto& dst = summary.constant_uv1 ? m_constant_uv1 : sample.m_uv1;

        has_valid_uv1 = true;
        if (sample.m_uv1_sp.isIndexed() && sample.m_uv1_sp.getIndices()->size() == refiner.indices.size())
        {
            IArray<int> uv1_indices{ (int*)sample.m_uv1_sp.getIndices()->get(), sample.m_uv1_sp.getIndices()->size() };
            refiner.template addIndexedAttribute<abcV2>(src, uv1_indices, dst, topology.m_remap_uv1);
        }
        else if (src.size() == refiner.indices.size())
        {
            refiner.template addExpandedAttribute<abcV2>(src, dst, topology.m_remap_uv1);
        }
        else if (src.size() == refiner.points.size())
        {
            refiner.template addIndexedAttribute<abcV2>(src, refiner.indices, dst, topology.m_remap_uv1);
        }
        else
        {
            DebugLog("Invalid attribute");
            has_valid_uv1 = false;
        }
    }

    if (sample.m_rgba_sp.valid())
    {
        IArray<abcC4> src{ sample.m_rgba_sp.getVals()->get(), sample.m_rgba_sp.getVals()->size() };
        auto& dst = summary.constant_rgba ? m_constant_rgba : sample.m_rgba;

        has_valid_rgba = true;
        if (sample.m_rgba_sp.isIndexed() && sample.m_rgba_sp.getIndices()->size() == refiner.indices.size())
        {
            IArray<int> colors_indices{ (int*)sample.m_rgba_sp.getIndices()->get(), sample.m_rgba_sp.getIndices()->size() };
            refiner.template addIndexedAttribute<abcC4>(src, colors_indices, dst, topology.m_remap_rgba);
        }
        else if (src.size() == refiner.indices.size())
        {
            refiner.template addExpandedAttribute<abcC4>(src, dst, topology.m_remap_rgba);
        }
        else if (src.size() == refiner.points.size())
        {
            refiner.template addIndexedAttribute<abcC4>(src, refiner.indices, dst, topology.m_remap_rgba);
        }
        else
        {
            DebugLog("Invalid attribute");
            has_valid_rgba = false;
        }
    }

    if (sample.m_rgb_sp.valid())
    {
        IArray<abcC3> src{ sample.m_rgb_sp.getVals()->get(), sample.m_rgb_sp.getVals()->size() };
        auto& dst = summary.constant_rgb ? m_constant_rgb : sample.m_rgb;

        has_valid_rgb = true;
        if (sample.m_rgb_sp.isIndexed() && sample.m_rgb_sp.getIndices()->size() == refiner.indices.size())
        {
            IArray<int> rgb_indices{ (int*)sample.m_rgb_sp.getIndices()->get(), sample.m_rgb_sp.getIndices()->size() };
            refiner.template addIndexedAttribute<abcC3>(src, rgb_indices, dst, topology.m_remap_rgb);
        }
        else if (src.size() == refiner.indices.size())
        {
            refiner.template addExpandedAttribute<abcC3>(src, dst, topology.m_remap_rgb);
        }
        else if (src.size() == refiner.points.size())
        {
            refiner.template addIndexedAttribute<abcC3>(src, refiner.indices, dst, topology.m_remap_rgb);
        }
        else
        {
            DebugLog("Invalid rgb attribute");
            has_valid_rgb = false;
        }
    }


    refiner.refine();
    refiner.retopology(config.swap_face_winding);

    // generate submeshes
    if (!topology.m_faceset_sps.empty())
    {
        // use face set index as material id
        topology.m_material_ids.resize(refiner.counts.size(), -1);
        for (size_t fsi = 0; fsi < topology.m_faceset_sps.size(); ++fsi)
        {
            auto& fsp = topology.m_faceset_sps[fsi];
            if (fsp.valid())
            {
                auto& faces = *fsp.getFaces();
                size_t num_faces = std::min(topology.m_material_ids.size(), faces.size());
                for (size_t fi = 0; fi < num_faces; ++fi)
                {
                    topology.m_material_ids[faces[fi]] = (int)fsi;
                }
            }
        }
        refiner.genSubmeshes(topology.m_material_ids, topology.m_faceset_names);
    }
    else
    {
        // no face sets present. one split == one submesh
        refiner.genSubmeshes();
    }

    topology.m_index_count = (int)refiner.new_indices_tri.size();
    topology.m_vertex_count = (int)refiner.new_points.size();
    onTopologyDetermined();

    topology.m_remap_points.swap(refiner.new2old_points);
    {
        auto& points = summary.constant_points ? m_constant_points : sample.m_points;
        points.swap((RawVector<abcV3>&)refiner.new_points);
        if (config.swap_handedness)
            SwapHandedness(points.data(), (int)points.size());
        if (config.scale_factor != 1.0f)
            ApplyScale(points.data(), (int)points.size(), config.scale_factor);
        sample.m_points_ref = points;
    }

    if (has_valid_normals)
    {
        sample.m_normals_ref = !m_constant_normals.empty() ? m_constant_normals : sample.m_normals;
        if (config.swap_handedness)
            SwapHandedness(sample.m_normals_ref.data(), (int)sample.m_normals_ref.size());
    }
    else
    {
        sample.m_normals_ref.reset();
    }

    if (has_valid_uv0)
        sample.m_uv0_ref = !m_constant_uv0.empty() ? m_constant_uv0 : sample.m_uv0;
    else
        sample.m_uv0_ref.reset();

    if (has_valid_uv1)
        sample.m_uv1_ref = !m_constant_uv1.empty() ? m_constant_uv1 : sample.m_uv1;
    else
        sample.m_uv1_ref.reset();

    if (has_valid_rgba)
        sample.m_rgba_ref = !m_constant_rgba.empty() ? m_constant_rgba : sample.m_rgba;
    else
        sample.m_rgba_ref.reset();

    if (has_valid_rgb)
        sample.m_rgb_ref = !m_constant_rgb.empty() ? m_constant_rgb : sample.m_rgb;
    else
        sample.m_rgb_ref.reset();

    if (summary.constant_normals && summary.compute_normals)
    {
        const auto &indices = topology.m_refiner.new_indices_tri;
        m_constant_normals.resize_discard(m_constant_points.size());
        GeneratePointNormals(topology.m_counts_sp->get(), topology.m_indices_sp->get(), sample.m_points_sp->get(),
                m_constant_normals.data(), topology.m_remap_points.data(),
                static_cast<int>(topology.m_counts_sp->size()),
                static_cast<int>(topology.m_remap_points.size()),
                static_cast<int>(sample.m_points_sp->size()));
        sample.m_normals_ref = m_constant_normals;
    }
    if (summary.constant_tangents && summary.compute_tangents)
    {
        const auto &indices = topology.m_refiner.new_indices_tri;
        m_constant_tangents.resize_discard(m_constant_points.size());
        GenerateTangents(m_constant_tangents.data(), m_constant_points.data(), m_constant_uv0.data(), m_constant_normals.data(),
            indices.data(), (int)m_constant_points.size(), (int)indices.size() / 3);
        sample.m_tangents_ref = m_constant_tangents;
    }

    // velocities are done in later part of cookSampleBody()
}

template <typename T, typename U>
void aiMeshSchema<T, U>::onTopologyDetermined()
{
    // nothing to do for now
    // maybe I will need to notify C# side for optimization
}

template<typename T, typename U>
inline aiMeshSchema<T, U>::~aiMeshSchema()
{
}

template<typename T>
inline aiMeshSample<T>::aiMeshSample(T* schema, TopologyPtr topo)
    : aiSample(schema)
    , m_topology(topo)
{
}

template<typename T>
inline aiMeshSample<T>::~aiMeshSample()
{
}

template<typename T>
void aiMeshSample<T>::reset()
{
    m_points_sp.reset(); m_points_sp2.reset();
    m_velocities_sp.reset();
    m_normals_sp.reset(); m_normals_sp2.reset();
    m_uv0_sp.reset(); m_uv1_sp.reset();
    m_rgba_sp.reset();
    m_rgb_sp.reset();

    m_points_ref.reset();
    m_velocities_ref.reset();
    m_uv0_ref.reset();
    m_uv1_ref.reset();
    m_normals_ref.reset();
    m_tangents_ref.reset();
    m_rgba_ref.reset();
    m_rgb_ref.reset();
}

template<typename T>
void aiMeshSample<T>::getSummary(aiMeshSampleSummary &dst) const
{
    dst.visibility = visibility;
    dst.split_count   = m_topology->getSplitCount();
    dst.submesh_count = m_topology->getSubmeshCount();
    dst.vertex_count  = m_topology->getVertexCount();
    dst.index_count   = m_topology->getIndexCount();
    dst.topology_changed = m_topology_changed;
}

template<typename T>
void aiMeshSample<T>::getSplitSummaries(aiMeshSplitSummary  *dst) const
{
    auto& refiner = m_topology->m_refiner;
    for (int i = 0; i < (int)refiner.splits.size(); ++i)
    {
        auto& src = refiner.splits[i];
        dst[i].submesh_count  = src.submesh_count;
        dst[i].submesh_offset = src.submesh_offset;
        dst[i].vertex_count   = src.vertex_count;
        dst[i].vertex_offset  = src.vertex_offset;
        dst[i].index_count    = src.index_count;
        dst[i].index_offset   = src.index_offset;
    }
}

template<typename T>
void aiMeshSample<T>::getSubmeshSummaries(aiSubmeshSummary *dst) const
{
    auto& refiner = m_topology->m_refiner;
    for (int i = 0; i < (int)refiner.submeshes.size(); ++i)
    {
        auto& src = refiner.submeshes[i];
        dst[i].split_index   = src.split_index;
        dst[i].submesh_index = src.submesh_index;
        dst[i].index_count   = src.index_count;
        dst[i].topology      = (aiTopology)src.topology;
    }
}

template<typename T>
void aiMeshSample<T>::fillSplitVertices(int split_index, aiPolyMeshData &data) const
{
    auto& schema = *dynamic_cast<T*>(getSchema());
    auto& summary = schema.getSummary();
    auto& splits = m_topology->m_refiner.splits;
    if (split_index < 0 || size_t(split_index) >= splits.size() || splits[split_index].vertex_count == 0)
        return;

    auto& refiner = m_topology->m_refiner;
    auto& split = refiner.splits[split_index];

    if (data.points)
    {
        m_points_ref.copy_to(data.points, split.vertex_count, split.vertex_offset);

        // bounds
        abcV3 bbmin, bbmax;
        MinMax(bbmin, bbmax, data.points, split.vertex_count);
        data.center = (bbmin + bbmax) * 0.5f;
        data.extents = bbmax - bbmin;
    }

    // note: velocity can be empty even if summary.has_velocities is true (compute is enabled & first frame)
    copy_or_clear(data.velocities, m_velocities_ref, split);
    copy_or_clear(data.normals, m_normals_ref, split);
    copy_or_clear(data.tangents, m_tangents_ref, split);
    copy_or_clear(data.uv0, m_uv0_ref, split);
    copy_or_clear(data.uv1, m_uv1_ref, split);
    copy_or_clear((abcC4*)data.rgba, m_rgba_ref, split);
    copy_or_clear_3_to_4<abcC4, abcC3>((abcC4*)data.rgb, m_rgb_ref, split);
}

template<typename T>
void aiMeshSample<T>::fillSubmeshIndices(int submesh_index, aiSubmeshData &data) const
{
    if (!data.indices)
        return;

    auto& refiner = m_topology->m_refiner;
    auto& submesh = refiner.submeshes[submesh_index];
    refiner.new_indices_submeshes.copy_to(data.indices, submesh.index_count, submesh.index_offset);
    copyCharsWithStride(data.faceset_names, submesh.facesetName, 255); // c# strings are 2 bytes. This function sets the low byte to the char and the high byte to \0
}

template<typename T>
void aiMeshSample<T>::fillVertexBuffer(aiPolyMeshData * vbs, aiSubmeshData * ibs)
{
    auto &refiner = m_topology->m_refiner;
    for (int spi = 0; spi < (int) refiner.splits.size(); ++spi)
        fillSplitVertices(spi, vbs[spi]);
    for (int smi = 0; smi < (int) refiner.submeshes.size(); ++smi)
        fillSubmeshIndices(smi, ibs[smi]);
}
