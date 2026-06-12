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

	private void Start()
	{
		scrpd = GetComponent<PlayableDirector>();
		if (scrpd.playOnAwake)
		{
			Play();
		}
	}

	public void Play()
	{
		scrpd = GetComponent<PlayableDirector>();
		scrpd.stopped += OnPlayableDirectorStopped;
		base.gameObject.SetActive(value: true);
		if (useCamera)
		{
			scrpd.Play();
		}
		eventStart.Invoke();
	}

	private void OnPlayableDirectorStopped(PlayableDirector aDirector)
	{
		eventStop.Invoke();
		if (destroyAfter)
		{
			Object.Destroy(base.gameObject);
		}
	}
}
