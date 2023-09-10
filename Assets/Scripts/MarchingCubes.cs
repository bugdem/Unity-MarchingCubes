using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public struct SurfacePoint
{
	public Vector3 Position;
	public Vector3Int Index;
	public float Value;
	public GameObject Obj;
}

public class MarchingCubes : MonoBehaviour
{
	[SerializeField] private Vector3Int _mapSize = new Vector3Int(12, 12, 12);
	[SerializeField] private float _surfaceSpacing = 1f;
	[SerializeField] private Color _maxSurfaceColor = Color.white;
	[SerializeField] private Color _minSurfaceColor = Color.black;
	[SerializeField] private int _maxSurfaceValue = 10;
	[SerializeField] private int _minSurfaceValue = 0;
	[SerializeField, ReadOnly] private float _currentSurfaceValue;

	[SerializeField] private bool _useRandomSeed = false;
	[SerializeField] private int _randomSeed = 1;

	[SerializeField] private Transform _surfacePointContainer;
	[SerializeField] private GameObject _surfacePointPrefab;
	[SerializeField] private Slider _surfaceValueSlider;

	private Dictionary<Vector3Int, SurfacePoint> _surfacePoints = new();

	private void Start()
	{
		_currentSurfaceValue = _maxSurfaceValue;
		_surfaceValueSlider.onValueChanged.AddListener(OnSurfaceSliderValueChanged);

		CreateSurfacePoints();
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

		for (int indexX = 0; indexX < _mapSize.x; indexX++)
		{
			for (int indexY = 0;indexY < _mapSize.y; indexY++)
			{
				for (int indexZ = 0; indexZ < _mapSize.z; indexZ ++)
				{
					Vector3Int index = new Vector3Int(indexX, indexY, indexZ);

					SurfacePoint surfacePoint = new SurfacePoint();
					surfacePoint.Index = index;
					surfacePoint.Position = new Vector3(indexX * _surfaceSpacing, indexY * _surfaceSpacing, indexZ *  _surfaceSpacing);
					surfacePoint.Value = GetSurfaceValue(index);
					surfacePoint.Obj = Instantiate(_surfacePointPrefab, _surfacePointContainer.transform);
					surfacePoint.Obj.transform.localPosition = surfacePoint.Position;
					surfacePoint.Obj.GetComponent<MeshRenderer>().material.SetColor("_Color", GetColorFromSurfaceValue(surfacePoint.Value));

					_surfacePoints.Add(index, surfacePoint);
				}
			}
		}
	}

	private void OnSurfaceSliderValueChanged(float newValue)
	{
		SetSurfaceValue(Mathf.Lerp(_minSurfaceValue, _maxSurfaceValue, newValue));
	}

	private void SetSurfaceValue(float newValue)
	{
		_currentSurfaceValue = newValue;

		for (int indexX = 0; indexX < _mapSize.x; indexX++)
		{
			for (int indexY = 0; indexY < _mapSize.y; indexY++)
			{
				for (int indexZ = 0; indexZ < _mapSize.z; indexZ++)
				{
					Vector3Int index = new Vector3Int(indexX, indexY, indexZ);
					SurfacePoint surfacePoint = _surfacePoints[index];
					surfacePoint.Obj.gameObject.SetActive(surfacePoint.Value >= _currentSurfaceValue);
				}
			}
		}
	}

	private float GetSurfaceValue(Vector3Int surfaceIndex)
	{
		return Random.Range(_minSurfaceValue, _maxSurfaceValue);
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

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.white;
		Gizmos.DrawWireCube(transform.position + ((Vector3)(_mapSize - Vector3Int.one) * _surfaceSpacing) / 2, ((Vector3)(_mapSize - Vector3Int.one) * _surfaceSpacing));
	}
}
