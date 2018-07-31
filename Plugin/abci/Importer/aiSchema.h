#pragma once
#include "aiAsync.h"


class aiSample
{
public:
    aiSample(aiSchema *schema);
    virtual ~aiSample();

    virtual aiSchema* getSchema() const { return m_schema; }
    const aiConfig& getConfig() const;

    virtual void waitAsync() {}
    void markForceSync();

public:
    bool visibility = true;

protected:
    aiSchema *m_schema = nullptr;
    bool m_force_sync = false;
};


class aiSchema : public aiObject
{
using super = aiObject;
public:
    using aiPropertyPtr = std::unique_ptr<aiProperty>;

    aiSchema(aiObject *parent, const abcObject &abc);
    virtual ~aiSchema();

    bool isConstant() const;
    bool isDataUpdated() const;
    void markForceUpdate();
    void markForceSync();
    int getNumProperties() const;
    aiProperty* getPropertyByIndex(int i);
    aiProperty* getPropertyByName(const std::string& name);

protected:
    virtual abcProperties getAbcProperties() = 0;
    void setupProperties();
    void updateProperties(const abcSampleSelector& ss);

protected:
    bool m_constant = false;
    bool m_data_updated = false;
    bool m_force_update = false;
    bool m_force_sync = false;
    std::vector<aiPropertyPtr> m_properties; // sorted vector
};


template <class Traits>
class aiTSchema : public aiSchema
{
using super = aiSchema;
public:
    using Sample = typename Traits::SampleT;
    using SamplePtr = std::shared_ptr<Sample>;
    using SampleCont = std::map<int64_t, SamplePtr>;
    using AbcSchema = typename Traits::AbcSchemaT;
    using AbcSchemaObject = Abc::ISchemaObject<AbcSchema>;


    aiTSchema(aiObject *parent, const abcObject &abc)
        : super(parent, abc)
    {
        AbcSchemaObject abcObj(abc, Abc::kWrapExisting);
        m_schema = abcObj.getSchema();
        m_time_sampling = m_schema.getTimeSampling();
        m_num_samples = static_cast<int64_t>(m_schema.getNumSamples());

        m_visibility_prop = AbcGeom::GetVisibilityProperty(const_cast<abcObject&>(abc));
        m_constant = m_schema.isConstant() && (!m_visibility_prop.valid() || m_visibility_prop.isConstant());

        setupProperties();
    }

    int getTimeSamplingIndex() const
    {
        return getContext()->getTimeSamplingIndex(m_schema.getTimeSampling());
    }

    int getSampleIndex(const abcSampleSelector& ss) const
    {
        return static_cast<int>(ss.getIndex(m_time_sampling, m_num_samples));
    }

    float getSampleTime(const abcSampleSelector& ss) const
    {
        return static_cast<float>(m_time_sampling->getSampleTime(ss.getRequestedIndex()));
    }

    int getNumSamples() const
    {
        return static_cast<int>(m_num_samples);
    }


    Sample* getSample() override
    {
        return m_sample.get();
    }

    virtual Sample* newSample() = 0;

    void updateSample(const abcSampleSelector& ss) override
    {
        m_async_load.reset();
        updateSampleBody(ss);
        if (m_async_load.ready())
            getContext()->queueAsync(m_async_load);
    }

    virtual void readSample(Sample& sample, uint64_t idx)
    {
        m_force_update_local = m_force_update;

        auto body = [this, &sample, idx]() {
            readSampleBody(sample, idx);
        };

        if (m_force_sync || !getConfig().async_load)
            body();
        else
            m_async_load.m_read = body;
    }

    virtual void cookSample(Sample& sample)
    {
        auto body = [this, &sample]() {
            cookSampleBody(sample);
        };

        if (m_force_sync || !getConfig().async_load)
            body();
        else
            m_async_load.m_cook = body;
    }

    void waitAsync() override
    {
        m_async_load.wait();
    }

protected:
    virtual void updateSampleBody(const abcSampleSelector& ss)
    {
        if (!m_enabled)
            return;

        Sample* sample = nullptr;
        int64_t sample_index = getSampleIndex(ss);
        auto& config = getConfig();

        if (!m_sample || (!m_constant && sample_index != m_last_sample_index) || m_force_update) {
            m_sample_index_changed = true;
            if (!m_sample)
                m_sample.reset(newSample());
            sample = m_sample.get();
            readSample(*sample, sample_index);
        }
        else {
            m_sample_index_changed = false;
            sample = m_sample.get();
            if (m_constant || !config.interpolate_samples)
                sample = nullptr;
        }

        if (sample && config.interpolate_samples) {
            auto& ts = *m_time_sampling;
            double requested_time = ss.getRequestedTime();
            double index_time = ts.getSampleTime(sample_index);
            double interval = 0;
            if (ts.getTimeSamplingType().isAcyclic()) {
                auto tsi = std::min((size_t)sample_index + 1, ts.getNumStoredTimes() - 1);
                interval = ts.getSampleTime(tsi) - index_time;
            }
            else {
                interval = ts.getTimeSamplingType().getTimePerCycle();
            }

            float prev_offset = m_current_time_offset;
            m_current_time_offset = interval == 0.0 ? 0.0f :
                (float)std::max(0.0, std::min((requested_time - index_time) / interval, 1.0));
            m_current_time_interval = (float)interval;

            // skip if time offset is not changed
            if (sample_index == m_last_sample_index && prev_offset == m_current_time_offset && !m_force_update)
                sample = nullptr;
        }

        if (sample) {
            if (m_force_sync)
                sample->markForceSync();

            cookSample(*sample);
            m_data_updated = true;
        }
        else {
            m_data_updated = false;
        }
        updateProperties(ss);

        m_last_sample_index = sample_index;
        m_force_update = false;
        m_force_sync = false;
    }

    virtual void readSampleBody(Sample& sample, uint64_t idx) = 0;
    virtual void cookSampleBody(Sample& sample) = 0;


    AbcGeom::ICompoundProperty getAbcProperties() override
    {
        return m_schema.getUserProperties();
    }

    void readVisibility(Sample& sample, const abcSampleSelector& ss)
    {
        if (m_visibility_prop.valid() && m_visibility_prop.getNumSamples() > 0) {
            int8_t v;
            m_visibility_prop.get(v, ss);
            sample.visibility = v != 0;
        }
    }

protected:
    AbcSchema m_schema;
    Abc::TimeSamplingPtr m_time_sampling;
    AbcGeom::IVisibilityProperty m_visibility_prop;
    SamplePtr m_sample;
    int64_t m_num_samples = 0;
    int64_t m_last_sample_index = -1;
    float m_current_time_offset = 0;
    float m_current_time_interval = 0;
    bool m_sample_index_changed = false;

    bool m_force_update_local = false; // m_force_update for worker thread

private:
    aiAsyncLoad m_async_load;
};
