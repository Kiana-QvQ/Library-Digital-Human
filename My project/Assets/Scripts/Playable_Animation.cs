// based on the original game.Yen Chezky(yenichw)
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

public class Playable_Animation : MonoBehaviour
{
	public bool useCamera;

	[HideInInspector]
	public Transform cameraTarget;

	public UnityEvent eventStart;

	public UnityEvent eventStop;

	public bool destroyAfter;

	private PlayableDirector scrpd;
	private bool _stopHandled;

	private void Awake()
	{
		scrpd = GetComponent<PlayableDirector>();
		if (scrpd != null)
		{
			scrpd.stopped += OnPlayableDirectorStopped;
		}
	}

	private void OnDestroy()
	{
		if (scrpd != null)
		{
			scrpd.stopped -= OnPlayableDirectorStopped;
		}
	}

	private void Start()
	{
		if (scrpd == null)
		{
			scrpd = GetComponent<PlayableDirector>();
		}

		if (scrpd == null)
		{
			Debug.LogWarning("[Playable_Animation] 未找到 PlayableDirector", this);
			return;
		}

		if (scrpd.state == PlayState.Playing)
		{
			eventStart.Invoke();
			return;
		}

		if (scrpd.playOnAwake || scrpd.extrapolationMode == DirectorWrapMode.Loop)
		{
			Play();
			return;
		}

		// SceneAihasto 等开场：Director 默认播放，确保 Timeline 会跑完
		Play();
	}

	public void Play()
	{
		if (scrpd == null)
		{
			scrpd = GetComponent<PlayableDirector>();
		}

		if (scrpd == null)
		{
			return;
		}

		gameObject.SetActive(true);

		if (useCamera && cameraTarget != null)
		{
			Camera cam = Camera.main;
			if (cam != null)
			{
				cam.transform.SetPositionAndRotation(
					cameraTarget.position,
					cameraTarget.rotation
				);
			}
		}

		if (scrpd.state != PlayState.Playing)
		{
			scrpd.Play();
		}

		eventStart.Invoke();
	}

	private void OnPlayableDirectorStopped(PlayableDirector aDirector)
	{
		if (_stopHandled)
		{
			return;
		}

		_stopHandled = true;
		Debug.Log("[Playable_Animation] Timeline 结束，触发 eventStop", this);
		eventStop.Invoke();

		if (destroyAfter)
		{
			Destroy(gameObject);
		}
	}
}
