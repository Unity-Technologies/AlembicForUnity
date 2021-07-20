#include "pch.h"
#include "aiInternal.h"
#include "aiContext.h"
#include "aiObject.h"
#include "aiSchema.h"
#include "aiPolyMesh.h"
#include "aiMeshSchema.h"
#include "aiSubD.h"

aiMeshTopology::aiMeshTopology()
{
}

void aiMeshTopology::clear()
{
    m_indices_sp.reset();
    m_counts_sp.reset();
    m_faceset_sps.clear();
    m_material_ids.clear();
    m_faceset_names.clear();
    m_refiner.clear();
    m_remap_points.clear();
    m_remap_normals.clear();
    m_remap_uv0.clear();
    m_remap_uv1.clear();
    m_remap_rgba.clear();
    m_remap_rgb.clear();

    m_vertex_count = 0;
    m_index_count = 0;
}

int aiMeshTopology::getSplitCount() const
{
    return (int)m_refiner.splits.size();
}

int aiMeshTopology::getVertexCount() const
{
    return m_vertex_count;
}

int aiMeshTopology::getIndexCount() const
{
    return m_index_count;
}

int aiMeshTopology::getSplitVertexCount(int split_index) const
{
    return (int)m_refiner.splits[split_index].vertex_count;
}

int aiMeshTopology::getSubmeshCount() const
{
    return (int)m_refiner.submeshes.size();
}

int aiMeshTopology::getSubmeshCount(int split_index) const
{
    return (int)m_refiner.splits[split_index].submesh_count;
}

