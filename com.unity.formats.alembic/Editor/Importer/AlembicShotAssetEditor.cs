using System;
using UnityEditor.Timeline;
using UnityEngine.Formats.Alembic.Importer;
using UnityEngine.Formats.Alembic.Timeline;
using UnityEngine.Timeline;

namespace UnityEditor.Formats.Alembic.Importer
{
    [CustomTimelineEditor(typeof(AlembicShotAsset))]
    class AlembicShotAssetEditor : ClipEditor
    {
        public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
        {
            if (TimelineEditor.inspectedDirector == null || TimelineEditor.inspectedAsset == null || clonedFrom != null)
            {
                return;
            }

            var asset = clip.asset as AlembicShotAsset;
            var asp = TimelineEditor.inspectedDirector.GetReferenceValue(asset.StreamPlayer.exposedName,
                out var valid) as AlembicStreamPlayer;
            if (asp == null || !valid)
            {
                return;
            }

#if TIMELINE_1_6_0
            var fps = TimelineEditor.inspectedAsset.editorSettings.frameRate;
#else
            var fps = TimelineEditor.inspectedAsset.editorSettings.fps;
#endif
            clip.duration = asp.Duration > 1 / fps ? asp.Duration : 1; // Clips that are under 1 frame are 1s long (tiny clips are hard to select in the UI)
        }
    }
}
