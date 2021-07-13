# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.3.0-pre.1] - 2021-07-11
### Added
- Added support for importing the cage mesh of SubD Alembic nodes.

### Changed
-  Automatically set the timeline clip length when dragging an AlembicStreamPlayer onto a timeline Alembic track.
### Fixed

## [2.2.0] - 2021-06-10
### Added
### Changed
### Fixed
- Fixed an issue where changing the start value of the streaming time range would slightly change the end of range value.
- Fixed a bug that was causing visual artefacts due to motion vector persistence when pausing Alembic playback.

## [2.2.0-pre.4] - 2021-04-27
### Added
### Changed
### Fixed
- Fixed a bug that caused out of project stream files to have the wrong number of material slots.
- Fixed a bug that was causing data loss when resetting the AlembicStreamPlayer.
- Fixed a bug that was converting previous Alembic instance in prefabs into out-of-project references.
- Fixed a bug that was adding unnecessary Undo events during the Alembic asset import process.
- Fixed a bug that was re-importing Alembic files on every SRP project start.
- Fixed a bug that caused standalone builds to fail when the Alembic assets are read-only.

## [2.2.0-pre.3] - 2021-04-07
### Added
- New option to automatically add the AlembicCurveRendering components for basic preview of the curves in the Scene.
- New option to stream Alembic files from outside a Unity project.

### Changed
- Minimum Unity version is 2019.4.
- Added a custom dependency on the "CurrentRenderPipeline" setting and ensure assigning the correct SRP material by default.

### Fixed
- Fixed a bug that caused the Alembic exporter to fail if GameObjects were being deleted during the recording session.
- Fixed a memory leak that occurred when using varying topology meshes.
- Fixed a bug that caused the exported mesh normals to be unreadable in DCCs.
- Fixed a bug that caused memory leaks when the same StreamPlayer was being Enabled/Disabled/Updated on the same frame.

## [2.2.0-exp.2] - 2021-01-21
### Added
- Added support for Stadia standalone builds.
- Added support for arm64 macOS.

### Changed
- Updated to Alembic version 1.7.16.
- The package depends on the Cloth Unity Module.
- Renamed AlembicCurve CurvePointCount to CurveOffsets, and changed the semantic to a stride array.

### Fixed
- Fixed a bug where degenerate triangles would create NaN normals.
- Fixed a bug in the importer to prevent the Editor from crashing when importing meshes with empty geometry samples.

## [2.2.0-exp.1] - 2020-12-17
### Added
- Added support for piecewise constant Curve importing.

## [2.1.2] - 2020-12-14
### Changed
- Fixed a bug causing the Alembic binary libraries to be copied into unsupported platform build, eg: iOS.

## [2.1.1-pre.1] - 2020-10-21
### Changed
- Added Unity recorder integration (compatible with Unity Recorder >= 2.2.0).
- When the timeline does discontinuous time updates (scrubbing), the alembic updates the scene synchronously.
- Updated optional dependency to Burst 1.1.1 or newer.

### Fixed
- Fixed a bug on Windows where file pointers would leak, and after some time all alembic loads would fail.
- Fixed a bug that caused a crash when exporting a GameObject with a MeshRender but without a MeshFilter Component.
- Fixed a bug where the visibility was not properly read if it was the only animated property of the object.

## [2.0.1-preview.1] - 2020-05-29
### Fixed
- Fixed a crash in the Alembic Exporter  when GameObject names contained / in the name.
- Fixed a bug where the Alembic motion vector direction was inverted.
- Fixed a bug where the time range slider in the importer inspector breaks when going beyond the upper bound.
- Fixed a crash in the Alembic Streamer when the object was being enabled and disabled very quickly.
- Fixed a bug where the exported SkinnedMesh scale was wrong if the transform contained a scale change.
- Fixed a bug where the exporter was writing the incorrect Camera rotation parameters.

## [2.0.0-preview.1] - 2019-12-20
### Changes
- Minimum Unity version is 2019.3.
- Introduced public API for Alembic playback and recording.
- Loading of Alembic files is now multi-threaded.
- Removed the LitAlembic HDRP shader that has been obsoleted by HDRP native shaders that now offers the once missing velocity option.
- HDF5 format is Obsolete.
- Fixed a bug where the face-id were being sorted in ascending order.
- Fixed a bug that caused crashes when importing point clouds that had no velocity information.
- Fixed a bug that caused exported geometries to have too many triangles.

## [1.0.6] - 2019-08-26
### Changes
- Added support for Linux
- Fixed incorrect normal calculation when the UVs were split
- Fixed lost references to the scene GameObject when renaming the alembic asset.
- Fixed the LitAlembic material compatibility with HDRP 4.10 (latest 2018.4 version). For 2019.2+ HDRP standard materials support vertex motion vectors.
-Fixed Vertex colour import from Houdini.

### Known Issues
- HDF5 is not supported under Linux for import or export.


## [1.0.5] - 2019-05-10
### Changes
- Fixed regression introduced in 1.0.4 where old Alembic scene instances would lose prefab connection. New scene instances made with 1.0.4 are unfortunately unrecoverable

## [1.0.4] - 2019-05-02
### Changes
- Fixed crash in the Exporter when using SwapFaces
- Fixed Branch recording mode in the Exporter
- Fixed Scene references being lost, when alembic prefab was being renamed
- Fixed initial import of Alembic files when both the alembic Package and assets are
  imported at the same time
- Fixed regression, where absolute paths to alembic assets were stored
- Alembic importer and Exporter correctly deals with physical Camera parameters

## [1.0.3] - 2019-04-09
### Changes
- Do not lock Alembic files when Windows
- Fix InheritXform on files from Blender
- Fix multibyte character names for Alembic nodes
- Fix crash from cyclic sampling
- Fix crash with empty samples
- Fix standalone player builds for Windows and OSX. Alembic files are now copied in
  the StreamingAssets folder of the built application

## [1.0.2] - 2019-03-21
### Changes
- Removed BuildTarget.LinuxStandalone (obsolete) references for 2019.2+

## [1.0.1] - 2019-03-14
### Changes
- Removed UnityEngine.UI references

## [1.0.0] - 2019-01-15
### Changes
- Removed Timeline Alembic recorder
- General cleanup
### Known Issues
- The first time the Alembic package is used in a project, the shaders shipped with
it need to be reimported, or else all Alembic imports will fail (case 1125012)

## [1.0.0-preview.13] - 2019-01-15
### Changes
- Removed broken recording of particles
- Moved testing code into the package

## [1.0.0-preview.10] - 2019-01-15
### Changes
- AlembicShotClip registers the time of the AlembicStreamPlayer as driven so timeline changes to the scene are not persistent
- AlembicRecorderClip uses tags to target GameObject root when recording a subset of
  the scene. If there are multiple GameObjects that match this Tag, a warning appears
  in the inspector and recording is disabled.

## [1.0.0-preview.9] - 2019-01-03
### Changes
- Meshes generated by the AlembicStreamPlayer are not serialized with the Scene

## [1.0.0-preview.8] - 2018-12-19
### Changes
- Alembic files are No longer copied to the StreamingAssets folder
- renamed Linux dso to abci.so for consistency
- reordered DllImport path selection so Editor locations are always found first

### Known Issues
- If you rename the prefab once instantiated, all scene references are lost (move works)

## [1.0.0-preview.7] - 2018-11-28
### Changes
- Fixed Windows plugin platform settings
- Apply Transform On Points is now ON by default to be consistent with mesh behaviour
- Point clouds are no longer imported by default
- Bug fix: changes to time range were not persisted
- Updated label of assumeNonSkinnedMeshRendersAreConstant to "Static MeshRenderers" to prevent cropping at default width

### Known Issues
- Camera objects aren't using Physical Camera mode
- Points renderer motion vector shader doesn't work
- Camera nodes are still created as GameObjects even if we don't import cameras
- AlembicWaitForEndOfFrame script is visible from the Component list in the inspector

## [1.0.0-preview.6] - 2018-11-22
- Apply transform to points renderer by default
- Don't import point clouds by default

## [1.0.0-preview.4] - 2018-10-05
- Added missing Linux dso

## [1.0.0-preview.3] - 2018-09-26
- Reorganized to conform to Unity's package standards
- Updated Alembic to 1.7.9 (Ogawa is now memory-mapped by default)

## [1.0.0-preview.2] - 2018-09-20
- Fixed AlembicStreamPlayerEditor warning in Unity 2018.3

## [1.0.0-preview.1] - 2018-09-07
- Added Documentation for imported animation clips
- Removed UTJ/Alembic/Exporter component menu
- Normalized all line endings

## [1.0.0-preview] - 2018-08-21
- Updated Documentation
- Contains all changes up until [release 20180413](https://github.com/unity3d-jp/AlembicForUnity/releases/tag/20180413) on github

## [0.2.0] - 2018-06-22
- Internalized the API
- Updated Documentation
- Changed namespaces to Unity[Editor|Engine].Formats.Alembic.[Exporter|Importer]
- Removed Alembic Camera Params component

## [0.1.2] - 2018-03-19

- Initial version for Package Manager
- Corresponds to [release 20180222](https://github.com/unity3d-jp/AlembicImporter/releases/tag/20180222) on github
