// based on the original game.Yen Chezky(yenichw)
using UnityEngine;
using UnityEngine.Events;

[AddComponentMenu("Functions/Event/Event")]
public class Events_Data : MonoBehaviour
{
	[Header("Method [EV(int)]")]
	public bool onStartZeroIndex;

	public UnityEvent[] _event;

	private void Start()
	{
		if (onStartZeroIndex)
		{
			TryInvoke(0, nameof(Start));
		}
	}

	public void EV(int x)
	{
		TryInvoke(x, nameof(EV));
	}

	public void NewEvent(int x)
	{
		TryInvoke(x, nameof(NewEvent));
	}

	private void TryInvoke(int index, string caller)
	{
		if (_event == null)
		{
			Debug.LogError($"{nameof(Events_Data)}.{caller}: _event 未赋值（null）", this);
			return;
		}

		if (index < 0 || index >= _event.Length)
		{
			Debug.LogError($"{nameof(Events_Data)}.{caller}: index 越界 index={index} length={_event.Length}", this);
			return;
		}

		var ev = _event[index];
		if (ev == null)
		{
			Debug.LogWarning($"{nameof(Events_Data)}.{caller}: _event[{index}] 为 null（无事件）", this);
			return;
		}

		ev.Invoke();
	}
}
