using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MarchingCubes))]
public class MarchingCubesEditor : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		var marchingCubes = (target as MarchingCubes);

		if (GUILayout.Button("Create Mesh"))
		{
			marchingCubes.CreateMesh();
		}
	}
}
