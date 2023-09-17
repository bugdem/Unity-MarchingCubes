using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public struct SurfacePoint
{
	public Vector3 Position;
	public Vector3Int Index;
	public float Value;
	public GameObject Obj;
}

public struct Cube
{
	public int TriangulationTableIndex;
	public SurfacePoint[] Points;
}

public class MarchingCubes : MonoBehaviour
{
	[Header("Configs")]
	[SerializeField] private Vector3Int _mapSize = new Vector3Int(12, 12, 12);
	[SerializeField] private bool _useRandomSeed = false;
	[SerializeField] private int _randomSeed = 1;
	[SerializeField] private float _noiseScale = .9f;

	[Header("Surface Point")]
	[SerializeField] private bool _showSurfacePoints = true;
	[SerializeField] private float _surfaceSpacing = 1f;
	[SerializeField] private Color _maxSurfaceColor = Color.white;
	[SerializeField] private Color _minSurfaceColor = Color.black;
	[SerializeField] private float _maxSurfaceValue = 10;
	[SerializeField] private float _minSurfaceValue = 0;
	[SerializeField] private float _currentSurfaceValue;
	[SerializeField] private Transform _surfacePointContainer;
	[SerializeField] private GameObject _surfacePointPrefab;
	[SerializeField] private Slider _surfaceValueSlider;
	[SerializeField] private TMPro.TextMeshProUGUI _maxSurfaceValueText;
	[SerializeField] private TMPro.TextMeshProUGUI _minSurfaceValueText;

	[Header("Marching Cube")]
	[SerializeField] private float _moveDuration = .5f;
	[SerializeField] private MeshRenderer _meshRenderer;
	[SerializeField] private MeshFilter _meshFilter;

	private Dictionary<Vector3Int, SurfacePoint> _surfacePoints = new Dictionary<Vector3Int, SurfacePoint>();
	private List<Vector3> _vertices = new List<Vector3>();
	private List<int> _triangles = new List<int>();
	private Mesh _mesh;
	private Vector3Int _currentMarchingCubeIndex;
	private Coroutine _meshCreationCoroutine;

	private float _minPerlinSurfaceValue;
	private float _maxPerlinSurfaceValue;

	private void Start()
	{
		_maxSurfaceValueText.SetText(_maxSurfaceValue.ToString());
		_minSurfaceValueText.SetText(_minSurfaceValue.ToString());

		_minPerlinSurfaceValue = float.MaxValue;
		_maxPerlinSurfaceValue = float.MinValue;

		_mesh = _meshFilter.mesh;
		_surfaceValueSlider.value = _currentSurfaceValue / (_minSurfaceValue + _maxSurfaceValue);
		_surfaceValueSlider.onValueChanged.AddListener(OnSurfaceSliderValueChanged);

		CreateSurfacePoints();
	}

	private void LateUpdate()
	{
		_surfacePointContainer.gameObject.SetActive(_showSurfacePoints);
	}

	private void CreateSurfacePoints()
	{
		if (!_useRandomSeed)
			_randomSeed = Random.Range(0, int.MaxValue);

		Random.InitState(_randomSeed);

		// Clear previous surface data.
		for (int index = _surfacePointContainer.childCount - 1; index >= 0; index --)
		{
			if (Application.isPlaying)
				GameObject.Destroy(_surfacePointContainer.GetChild(index).gameObject);
			else
				GameObject.DestroyImmediate(_surfacePointContainer.GetChild(index).gameObject);
		}

		_surfacePoints.Clear();

		LoopMap((index) =>
		{
			SurfacePoint surfacePoint = new SurfacePoint();
			surfacePoint.Index = index;
			surfacePoint.Position = new Vector3(index.x * _surfaceSpacing, index.y * _surfaceSpacing, index.z * _surfaceSpacing);
			surfacePoint.Value = GetSurfaceValue(index);
			surfacePoint.Obj = Instantiate(_surfacePointPrefab, _surfacePointContainer.transform);
			surfacePoint.Obj.transform.localPosition = surfacePoint.Position;
			surfacePoint.Obj.GetComponent<MeshRenderer>().material.SetColor("_Color", GetColorFromSurfaceValue(surfacePoint.Value));
			surfacePoint.Obj.gameObject.SetActive(surfacePoint.Value >= _currentSurfaceValue);

			_surfacePoints.Add(index, surfacePoint);

			if (surfacePoint.Value < _minPerlinSurfaceValue)
				_minPerlinSurfaceValue = surfacePoint.Value;
			if (surfacePoint.Value > _maxPerlinSurfaceValue)
				_maxPerlinSurfaceValue = surfacePoint.Value;
		});

		Debug.Log("Min Surface Value : " + _minPerlinSurfaceValue.ToString("F2"));
		Debug.Log("Max Surface Value : " + _maxPerlinSurfaceValue.ToString("F2"));
	}

	public void CreateMesh()
	{
		if (!Application.isPlaying) 
			return;

		if (_meshCreationCoroutine != null)
			StopCoroutine(_meshCreationCoroutine);
		_meshCreationCoroutine = StartCoroutine(ICreateMesh());
	}

	private IEnumerator ICreateMesh()
	{
		_mesh.Clear();
		_vertices.Clear();
		_triangles.Clear();

		for (int indexX = 0; indexX < _mapSize.x; indexX++)
		{
			for (int indexY = 0; indexY < _mapSize.y; indexY++)
			{
				for (int indexZ = 0; indexZ < _mapSize.z; indexZ++)
				{
					Vector3Int index = new Vector3Int(indexX, indexY, indexZ);
					MarchCube(index);

					if (_moveDuration > 0)
						yield return new WaitForSeconds(_moveDuration);
				}
			}
		}

		yield break;
	}

	private void MarchCube(Vector3Int index)
	{
		_currentMarchingCubeIndex = index;

		Cube marchingCube = GetCube(index);
		for (int edgeIndexInTriangulation = 0; Table.Triangulation[marchingCube.TriangulationTableIndex, edgeIndexInTriangulation] != -1; edgeIndexInTriangulation += 3)
		{			
			for (int vertexIndex = 0; vertexIndex < 3; vertexIndex ++)
			{
				int edgeIndex = Table.Triangulation[marchingCube.TriangulationTableIndex, edgeIndexInTriangulation + vertexIndex];

				// Get indices of the corner points of current edge.
				int indexA = Table.CornerIndexFromEdge[edgeIndex, 0];
				int indexB = Table.CornerIndexFromEdge[edgeIndex, 1];

				Vector3 vertexPos = (marchingCube.Points[indexA].Position + marchingCube.Points[indexB].Position) / 2;

				_vertices.Add(vertexPos);
				_triangles.Add(_vertices.Count - 1);
			}
		}

		_mesh.vertices = _vertices.ToArray();
		_mesh.triangles = _triangles.ToArray();
		_mesh.RecalculateNormals();
		_mesh.RecalculateBounds();
	}

	private Cube GetCube(Vector3Int index)
	{
		Cube cube = new Cube();
		cube.Points = new SurfacePoint[8];
		cube.TriangulationTableIndex = 0;

		for (int pointIndex = 0; pointIndex < 8; pointIndex++)
		{
			int x = (pointIndex % 4) / 2;
			int y = pointIndex / 4;
			int z = (pointIndex % 4 == 1 || pointIndex % 4 == 2) ? 1 : 0;

			Vector3Int currentIndex = index + new Vector3Int(x,y,z);
			SurfacePoint surfacePoint = GetSurfacePoint(currentIndex);

			cube.Points[pointIndex] = surfacePoint;
			if (surfacePoint.Value >= _currentSurfaceValue)
				cube.TriangulationTableIndex |= 1 << pointIndex;
		}
		return cube;
	}

	private SurfacePoint GetSurfacePoint(Vector3Int index)
	{
		if (_surfacePoints.TryGetValue(index, out SurfacePoint surfacePoint))
			return surfacePoint;

		SurfacePoint newSurfacePoint = new SurfacePoint();
		newSurfacePoint.Index = index;
		newSurfacePoint.Value = 0;
		newSurfacePoint.Position = new Vector3(index.x * _surfaceSpacing, index.y * _surfaceSpacing, index.z * _surfaceSpacing);
		return newSurfacePoint;
	}

	private void OnSurfaceSliderValueChanged(float newValue)
	{
		SetSurfaceValue(Mathf.Lerp(_minSurfaceValue, _maxSurfaceValue, newValue));
	}

	private void SetSurfaceValue(float newValue)
	{
		_currentSurfaceValue = newValue;

		LoopMap((index) =>
		{
			SurfacePoint surfacePoint = _surfacePoints[index];
			surfacePoint.Obj.gameObject.SetActive(surfacePoint.Value >= _currentSurfaceValue);
		});
	}

	private void LoopMap(Action<Vector3Int> action)
	{
		for (int indexX = 0; indexX < _mapSize.x; indexX++)
		{
			for (int indexY = 0; indexY < _mapSize.y; indexY++)
			{
				for (int indexZ = 0; indexZ < _mapSize.z; indexZ++)
				{
					action?.Invoke(new Vector3Int(indexX, indexY, indexZ));
				}
			}
		}
	}

	// TODO: This method should return a value with an algorithm like a perlin noise.
	// Returns random value right now.
	private float GetSurfaceValue(Vector3Int surfaceIndex)
	{
		// return Random.Range(_minSurfaceValue, _maxSurfaceValue);

		return Perlin3D(surfaceIndex.x * _noiseScale, surfaceIndex.y * _noiseScale, surfaceIndex.z * _noiseScale);
	}

	private Color GetColorFromSurfaceValue(Vector3Int surfaceIndex)
	{
		float surfaceValue = GetSurfaceValue(surfaceIndex);
		return GetColorFromSurfaceValue(surfaceValue);
	}

	private Color GetColorFromSurfaceValue(float surfaceValue)
	{
		return Color.Lerp(_minSurfaceColor, _maxSurfaceColor, surfaceValue / (_minSurfaceValue + _maxSurfaceValue));
	}

	public static float Perlin3D(float x, float y, float z)
	{
		float ab = Mathf.PerlinNoise(x, y);
		float bc = Mathf.PerlinNoise(y, z);
		float ac = Mathf.PerlinNoise(x, z);

		float ba = Mathf.PerlinNoise(y, x);
		float cb = Mathf.PerlinNoise(z, y);
		float ca = Mathf.PerlinNoise(z, x);

		float abc = ab + bc + ac + ba + cb + ca;
		return abc / 6f;
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.gray;
		Gizmos.DrawWireCube(transform.position + ((Vector3)(_mapSize - Vector3Int.one) * _surfaceSpacing) / 2, ((Vector3)(_mapSize - Vector3Int.one) * _surfaceSpacing));

		var marchingCube = GetCube(_currentMarchingCubeIndex);
		for (int index = 0; index < 8; index ++)
		{
			var surfacePoint = marchingCube.Points[index];
			Gizmos.color = surfacePoint.Value >= _currentSurfaceValue ? Color.white : Color.black;
			Gizmos.DrawSphere(surfacePoint.Position, .5f);
		}

		Gizmos.color = Color.white;
		Gizmos.DrawWireCube(transform.position + Vector3.one * _surfaceSpacing * .5f + (Vector3)_currentMarchingCubeIndex * _surfaceSpacing, Vector3.one * _surfaceSpacing);
	}
}
