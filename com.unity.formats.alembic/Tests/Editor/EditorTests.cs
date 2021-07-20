using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;
using UnityEngine.Formats.Alembic.Sdk;
using UnityEngine.Formats.Alembic.Timeline;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace UnityEditor.Formats.Alembic.Exporter.UnitTests
{
    class EditorTests
    {
        readonly List<string> deleteFileList = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var file in deleteFileList)
            {
                AssetDatabase.DeleteAsset(file);
                var meta = file + ".meta";
                if (File.Exists(meta))
                {
                    File.Delete(meta);
                }
            }

            deleteFileList.Clear();
        }

        [Test]
        public void MarshalTests()
        {
            Assert.AreEqual(72, System.Runtime.InteropServices.Marshal.SizeOf(typeof(aePolyMeshData)));
        }

        [Test]
        public void BadGeometryDoesNotCreateNanNormals()
        {
            const string dummyGUID = "4f03ab724b2494f38ae7c6c3d06e0825";
            var path = AssetDatabase.GUIDToAssetPath(dummyGUID);
            var abc = AssetDatabase.LoadMainAssetAtPath(path);
            var instance = PrefabUtility.InstantiatePrefab(abc) as GameObject;
            instance.GetComponent<AlembicStreamPlayer>().UpdateImmediately(0);
            var mesh = instance.GetComponentInChildren<MeshFilter>().sharedMesh;
            var naNs = mesh.normals.Where(x => double.IsNaN(x.x) || double.IsNaN(x.y) || double.IsNaN(x.z));
            Assert.IsEmpty(naNs);
        }

        [Test]
        public void EmptyMeshFileIsHandledGracefully()
        {
            var path = AssetDatabase.GUIDToAssetPath("66b8b570b5eec42bd80704392a7001b5");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            Assert.IsNotNull(inst.GetComponent<AlembicStreamPlayer>());
            Assert.IsEmpty(inst.GetComponentsInChildren<MeshFilter>().Select(x => x.sharedMesh != null));
        }

        [UnityTest]
        public IEnumerator AddCurveRenderingSettingIsObeyed([Values] bool addCurve)
        {
            var path = AssetDatabase.GUIDToAssetPath("253cca792b1714bd985e9752217590a8"); // curves asset
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            var player = inst.GetComponent<AlembicStreamPlayer>();
            Assert.IsNotNull(player);
            player.StreamDescriptor.Settings.CreateCurveRenderers = addCurve;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            Assert.IsNotNull(inst.GetComponentsInChildren<AlembicCurves>());
            inst.GetComponentInChildren<AlembicCurvesRenderer>();
            Assert.IsTrue(inst.GetComponentInChildren<AlembicCurvesRenderer>() != null ^ addCurve);
        }

        [UnityTest]
        public IEnumerator MultipleActivationsTracksCanActOnTheSameStreamPlayer()
        {
            var path = AssetDatabase.GUIDToAssetPath("253cca792b1714bd985e9752217590a8");  // curves asset
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            var player = inst.GetComponent<AlembicStreamPlayer>();
            Assert.IsNotNull(player);
            var director = new GameObject().AddComponent<PlayableDirector>();
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            director.playableAsset = timeline;

            var abcTrack = timeline.CreateTrack<AlembicTrack>(null, "");
            var clip = abcTrack.CreateClip<AlembicShotAsset>();
            var abcAsset = clip.asset as AlembicShotAsset;
            var refAbc = new ExposedReference<AlembicStreamPlayer> {exposedName = Guid.NewGuid().ToString()};
            abcAsset.StreamPlayer = refAbc;
            director.SetReferenceValue(refAbc.exposedName, player);
            var a1 = timeline.CreateTrack<ActivationTrack>();

            var a2 = timeline.CreateTrack<ActivationTrack>();
            var c2 = a2.CreateDefaultClip();
            c2.start = 2;
            c2.duration = 1;

            director.SetGenericBinding(a1, player.gameObject);
            director.SetGenericBinding(a2, player.gameObject);
            director.RebuildGraph();
            director.time = 0;
            yield return null;
            director.time = 2.5;
            director.Evaluate();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectingAnAlembicPlayer_ShouldNotLeakResources()
        {
            var path = AssetDatabase.GUIDToAssetPath("1a066d124049a413fb12b82470b82811"); // GUID of DummyAlembic.abc
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var player = asset.GetComponent<AlembicStreamPlayer>();
            Assert.IsNull(player.abcStream);
            Selection.activeObject = asset;
            yield return null;
            Assert.IsNull(player.abcStream);
        }

        [Test]
        public void Importerless_ShouldGenerateTheCorrectNumberOfMaterialSlots()
        {
            var path = AssetDatabase.GUIDToAssetPath("f30dca527f4e947248046d6d12750c2d"); // GUID of twoTris_twoFaceSets...
            var player = new GameObject().AddComponent<AlembicStreamPlayer>();

            var ret = player.LoadFromFile(path);
            Assert.IsTrue(ret);

            var materials = player.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            var subMeshes = player.GetComponentInChildren<MeshFilter>().sharedMesh.subMeshCount;
            Assert.AreEqual(2, subMeshes);
            Assert.AreEqual(subMeshes, materials.Length);
        }

        [Test]
        public void UpgradingOldPrefabs_DoNotGetSwitchedToImporterless()
        {
            var player = LoadAndInstantiate("c01136857b8dc481bb9babb33803ed4a");
            Assert.AreEqual(AlembicStreamPlayer.AlembicStreamSource.Internal, player.StreamSource);
            Assert.AreEqual(typeof(AlembicStreamDescriptor), player.StreamDescriptor.GetType());
            var asd = player.StreamDescriptor as AlembicStreamDescriptor;
            Assert.IsTrue(!string.IsNullOrEmpty(asd.PathToAbc));
            Assert.IsTrue(File.Exists(asd.PathToAbc));
        }

        [UnityTest]
        public IEnumerator ImportingFilesShouldNotGenerateUndoEvents()
        {
            Undo.IncrementCurrentGroup();
            var go = new GameObject();
            Undo.RegisterCreatedObjectUndo(go, "Create Object");
            Undo.IncrementCurrentGroup();

            yield return null;
            yield return null;

            const string dummyGUID = "1a066d124049a413fb12b82470b82811"; // GUID of DummyAlembic.abc
            const string copiedAbcFile = "Assets/abc.abc";
            var path = AssetDatabase.GUIDToAssetPath(dummyGUID);
            var srcDummyFile = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AlembicStreamPlayer>().First()
                .StreamDescriptor.PathToAbc;
            File.Copy(srcDummyFile, copiedAbcFile, true);
            deleteFileList.Add(copiedAbcFile);

            AssetDatabase.Refresh();
            yield return null;
            yield return null;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(copiedAbcFile);
            Assert.IsNotNull(asset);

            Undo.PerformUndo();

            Assert.IsTrue(go == null); // If the alembic importer pushed undo events, i will not undo the creation of the gameobject. If this is dead, it means there were no extra undo events between.
        }

        [Test]
        public void Velocities_AreConsistentWithMotion()
        {
            var player = LoadAndInstantiate("a6d019a425afe49d7a8fd029c82c0455");
            var mesh = player.GetComponentInChildren<MeshFilter>().sharedMesh;
            player.UpdateImmediately(0);

            var vert0 = new List<Vector3>();
            var velocity0 = new List<Vector3>();
            mesh.GetVertices(vert0);
            mesh.GetUVs(5, velocity0);

            Assert.IsTrue(velocity0.All(x => x == Vector3.zero));

            player.UpdateImmediately(1 / 60f);
            var vert1 = new List<Vector3>();
            var velocity1 = new List<Vector3>();
            mesh.GetVertices(vert1);
            mesh.GetUVs(5, velocity1);

            var computedVelocity = vert1.Zip(vert0, (v1, v0) => v1 - v0).ToList();
            CollectionAssert.AreEqual(velocity1, computedVelocity);
        }

        [Test]
        public void Computed_Velocities_AreScaled()
        {
            var player = LoadAndInstantiate("a6d019a425afe49d7a8fd029c82c0455");
            var mesh = player.GetComponentInChildren<MeshFilter>().sharedMesh;
            player.UpdateImmediately(1 / 60f);

            var vert0 = new List<Vector3>();
            var velocity0 = new List<Vector3>();
            mesh.GetVertices(vert0);
            mesh.GetUVs(5, velocity0);

            var expectedVelocity = new[] {new Vector3(0, 8, 0), new Vector3(-4, 0, 0), new Vector3(0, 0, 0)};
            {
                var error = expectedVelocity.Zip(velocity0, (x, y) => (x - y).magnitude);
                Assert.IsTrue(error.All(x => x < 1e-5));
            }
            player.VertexMotionScale = 0.5f;
            player.UpdateImmediately(0);
            player.UpdateImmediately(1 / 60f); // Bug that needs to change time to update velocity
            mesh.GetUVs(5, velocity0);
            {
                var expected = expectedVelocity.Select(x => 0.5f * x).ToArray();
                var error = expected.Zip(velocity0, (x, y) => (x - y).magnitude);
                Assert.IsTrue(error.All(x => x < 1e-5));
            }
        }

        [Test]
        public void Read_Velocities_AreNotScaled() // This is very strange. Need to decide if this is a bug
        {
            var player = LoadAndInstantiate("d0f12062215204c6991895f6a51dd627");
            var mesh = player.GetComponentInChildren<MeshFilter>().sharedMesh;
            player.UpdateImmediately(1 / 60f);

            var velocity0 = new List<Vector3>();
            mesh.GetUVs(5, velocity0);

            var v0 = new Vector3(0.00163127552f, 3.62623905E-05f, 0.0247734953f);
            var v100 = new Vector3(0.00122647523f, 5.96942518E-05f, 0.0172897689f);
            Assert.That(v0, Is.EqualTo(velocity0[0]).Within(1e-5));
            Assert.That(v100, Is.EqualTo(velocity0[100]).Within(1e-5));

            player.VertexMotionScale = 0.5f;
            player.UpdateImmediately(0);
            player.UpdateImmediately(1 / 60f); // Bug that needs to change time to update velocity
            mesh.GetUVs(5, velocity0);

            Assert.That(player.VertexMotionScale * v0, Is.Not.EqualTo(velocity0[0]).Within(1e-5));
            Assert.That(player.VertexMotionScale * v100, Is.Not.EqualTo(velocity0[100]).Within(1e-5));
        }

        [Test]
        [Ignore("JIRA: ABC-231")]
        public void FaceAttributes_AreSplitCorrectly() // Broken
        {
            var path = AssetDatabase.GUIDToAssetPath("dd3554fc098614b9e99b49873fe18cd6"); // GUID of cubes_coloured.abc
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            go.GetComponent<AlembicStreamPlayer>().UpdateImmediately(0);

            var mesh = GameObject.Find("cube_face").GetComponentInChildren<MeshFilter>().sharedMesh;
            var cd = mesh.colors;

            Assert.IsTrue(true);
        }

        [Test]
        public void PointAttributes_AreSplitCorrectly()
        {
            var path = AssetDatabase.GUIDToAssetPath("dd3554fc098614b9e99b49873fe18cd6"); // GUID of cubes_coloured.abc
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            go.GetComponent<AlembicStreamPlayer>().UpdateImmediately(0);

            var mesh = GameObject.Find("cube_point").GetComponentInChildren<MeshFilter>().sharedMesh;
            var expectedColours = new List<Color32>
            {
                new Color32(206, 75, 7, 255),
                new Color32(14, 244, 121, 255),
                new Color32(105, 34, 75, 255),
                new Color32(35, 146, 9, 255),
                new Color32(163, 82, 173, 255),
                new Color32(67, 148, 214, 255),
                new Color32(217, 8, 47, 255),
                new Color32(230, 208, 156, 255),
            };

            var diff = mesh.colors32.Zip(expectedColours, (x, y) => Mathf.Abs(((Color)x - y).grayscale)).ToArray();

            Assert.AreEqual(expectedColours.Count, mesh.colors32.Length);
            Assert.IsTrue(diff.All(x => x < 1e-5));
        }

        [Test]
        public void VertexAttributes_AreSplitCorrectly()
        {
            var path = AssetDatabase.GUIDToAssetPath("dd3554fc098614b9e99b49873fe18cd6"); // GUID of cubes_coloured.abc
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            go.GetComponent<AlembicStreamPlayer>().UpdateImmediately(0);

            var mesh = GameObject.Find("cube_vertex").GetComponentInChildren<MeshFilter>().sharedMesh;
            var expectedColours = new List<Color32>
            {
                new Color32(35, 146, 9, 255),
                new Color32(206, 75, 7, 255),
                new Color32(163, 82, 173, 255),
                new Color32(217, 8, 47, 255),
                new Color32(105, 34, 75, 255),
                new Color32(14, 244, 121, 255),
                new Color32(67, 148, 214, 255),
                new Color32(230, 208, 156, 255),
                new Color32(190, 178, 36, 255),
                new Color32(176, 40, 197, 255),
                new Color32(237, 132, 127, 255),
                new Color32(14, 133, 73, 255),
                new Color32(115, 29, 124, 255),
                new Color32(230, 46, 186, 255),
                new Color32(14, 176, 3, 255),
                new Color32(101, 60, 188, 255),
                new Color32(151, 231, 54, 255),
                new Color32(214, 30, 169, 255),
                new Color32(220, 64, 148, 255),
                new Color32(201, 132, 165, 255),
                new Color32(107, 232, 54, 255),
                new Color32(53, 209, 84, 255),
                new Color32(187, 17, 4, 255),
                new Color32(62, 202, 60, 255)
            };

            var diff = mesh.colors32.Zip(expectedColours, (x, y) => Mathf.Abs(((Color)x - y).grayscale)).ToArray();

            Assert.AreEqual(expectedColours.Count, mesh.colors32.Length);
            Assert.IsTrue(diff.All(x => x < 1e-5));
        }

        [Test]
        public void FacesetNames_AreReadCorrectly() // Broken
        {
            var path = AssetDatabase.GUIDToAssetPath("f30dca527f4e947248046d6d12750c2d"); // GUID of cubes_coloured.abc
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            var facesetNames = new[] { "phong2SG", "phong1SG" };
            CollectionAssert.AreEqual(facesetNames, asset.GetComponentInChildren<AlembicCustomData>().FacesetNames);

            var go = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            go.GetComponent<AlembicStreamPlayer>().UpdateImmediately(0);
            CollectionAssert.AreEqual(facesetNames, go.GetComponentInChildren<AlembicCustomData>().FacesetNames);
        }

        [Test]
        public void SubDAndPolyMeshes_ReadTheSameWay()
        {
            Vector3[] vertexPoly, vertexSubd;
            {
                var playerPoly = LoadAndInstantiate("d3dedb3122bcd4d3787b29d185751353");
                var meshPoly = playerPoly.GetComponentInChildren<MeshFilter>().sharedMesh;
                playerPoly.UpdateImmediately(0);
                vertexPoly = meshPoly.vertices;
            }
            {
                var playerSubd = LoadAndInstantiate("42c8ff3c70d6b46d09da4146d0a44754");
                var meshSubd = playerSubd.GetComponentInChildren<MeshFilter>().sharedMesh;
                playerSubd.UpdateImmediately(0);
                vertexSubd = meshSubd.vertices;
            }

            CollectionAssert.AreEqual(vertexSubd, vertexPoly);
        }

        [Test]
        public void SubDMeshes_Animate()
        {
            var player = LoadAndInstantiate("d844f566b42f54aa4a4f8a1bc3a121af");
            var mesh = player.GetComponentInChildren<MeshFilter>().sharedMesh;
            player.UpdateImmediately(player.StreamDescriptor.MediaStartTime);
            var v0 = mesh.vertices;

            Assert.IsTrue(v0.Any(x => x != Vector3.zero)); // some data
            player.UpdateImmediately((player.StreamDescriptor.MediaStartTime + player.StreamDescriptor.MediaEndTime) / 2);
            CollectionAssert.AreNotEqual(v0, mesh.vertices);
        }

        [Test]
        public void SubDVariableTopologyMeshes_Animate()
        {
            var player = LoadAndInstantiate("942e560acda81ab47b439395d265a587");
            var mesh = player.GetComponentInChildren<MeshFilter>().sharedMesh;
            player.UpdateImmediately(player.StreamDescriptor.MediaStartTime);
            var v0 = mesh.vertices;

            Assert.IsTrue(v0.Any(x => x != Vector3.zero)); // some data
            player.UpdateImmediately(player.StreamDescriptor.MediaEndTime);
            CollectionAssert.AreNotEqual(v0, mesh.vertices);
            Assert.AreNotEqual(v0.Length, mesh.vertices.Length);
        }

        static AlembicStreamPlayer LoadAndInstantiate(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            var player = go.GetComponent<AlembicStreamPlayer>();

            return player;
        }
    }
}
