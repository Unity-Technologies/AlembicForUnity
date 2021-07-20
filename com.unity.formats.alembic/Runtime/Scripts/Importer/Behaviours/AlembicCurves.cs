using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Formats.Alembic.Sdk;

namespace UnityEngine.Formats.Alembic.Importer
{
    /// <summary>
    /// The AlembicCurves component stores the curve information for the associated Alembic tree Node.
    /// </summary>
    [DisallowMultipleComponent]
    public class AlembicCurves : MonoBehaviour
    {
        /// <summary>
        /// Defines the type for the update callback.
        /// </summary>
        /// <param name="curves">the component that was updated.</param>
        public delegate void OnUpdateDataHandler(AlembicCurves curves);
        /// <summary>
        /// Returns the positions for all the curves in the current Alembic node.
        /// </summary>
        public Vector3[] Positions => positionsList.GetArray();
        /// <summary>
        /// Returns an array where: the i-th entry returns the starting index of the i-th curve ( the i-th curve is between [CurveOffsets[i]..CurveOffsets[i+1]-1) ).
        /// </summary>
        public int[] CurveOffsets => curveOffsets.GetArray();
        /// <summary>
        /// Returns an array of UVs (optional), if the imported file contained UVs.
        /// </summary>
        public Vector2[] UVs => uvs.GetArray();
        /// <summary>
        /// Returns an array of Widths (optional), if the imported file contained widths/
        /// </summary>
        public float[] Widths => widths.GetArray();
        /// <summary>
        /// Returns an array of Velocities (optional), if the imported file contained UVs.
        /// </summary>
        public Vector3[] Velocities => velocitiesList.GetArray();
        /// <summary>
        /// Is an event that is invoked every time the data in the component is updated by the AlembicStreamPlayer. This is caused by the evaluation time or import options changing.
        /// This allows users to perform data post-processing only when needed.
        /// </summary>
        public event OnUpdateDataHandler OnUpdateData
        {
            add => update += value;
            remove => update -= value;
        }

        internal void InvokeOnUpdate(AlembicCurves curves)
        {
            update?.Invoke(curves);
        }

        OnUpdateDataHandler update;

        internal PinnedList<Vector3> positionsList { get; } = new PinnedList<Vector3>();
        internal PinnedList<int> curveOffsets { get; } = new PinnedList<int>();
        internal PinnedList<Vector2> uvs { get; } = new PinnedList<Vector2>();
        internal PinnedList<float> widths { get; } = new PinnedList<float>();
        internal PinnedList<Vector3> velocitiesList { get; } = new PinnedList<Vector3>();
    }
}
