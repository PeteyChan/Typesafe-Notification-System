using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

public class EventHandler : MonoBehaviour 
{
	Dictionary<int, GameEvent> eventLookup = new Dictionary<int, GameEvent>();
	static Dictionary<int, GameEvent> globalEventLookup = new Dictionary<int, GameEvent>();

	public void AddListener<E>(object subscriber, Action<E> callback) where E : struct
	{
		GameEvent e;
		if (eventLookup.TryGetValue(TypeID<E>.ID, out e))
		{
			GameEvent<E> handle = (GameEvent<E>)e;
			#if UNITY_EDITOR
			handle.Subscriber.Add(subscriber);
			#endif
			handle.gameEvent += callback;
			return;
		}

		GameEvent<E> newEvent = new GameEvent<E>();
		newEvent.gameEvent += callback;
		#if UNITY_EDITOR
		newEvent.Subscriber.Add(subscriber);
		#endif
		eventLookup.Add(TypeID<E>.ID, newEvent);
	}

	public static void AddGlobalListener<E>(object subscriber, Action<E> callback) where E : struct
	{
		GameEvent e;
		if (globalEventLookup.TryGetValue(TypeID<E>.ID, out e))
		{
			GameEvent<E> handle = (GameEvent<E>)e;
			#if UNITY_EDITOR
			handle.Subscriber.Add(subscriber);
			#endif
			handle.gameEvent += callback;
			return;
		}

		GameEvent<E> newEvent = new GameEvent<E>();
		newEvent.gameEvent += callback;
		#if UNITY_EDITOR
		newEvent.Subscriber.Add(subscriber);
		#endif
		globalEventLookup.Add(TypeID<E>.ID, newEvent);
	}

	public void RemoveListener<E>(object subscriber, Action<E> callback) where E : struct
	{
		GameEvent e;
		if (eventLookup.TryGetValue(TypeID<E>.ID, out e))
		{
			((GameEvent<E>)e).RemoveSubscriber(subscriber, callback);
		}
	}

	public static void RemoveGlobalListener<E>(object subscriber, Action<E> callback) where E : struct
	{
		GameEvent e;
		if (globalEventLookup.TryGetValue(TypeID<E>.ID, out e))
		{
			((GameEvent<E>)e).RemoveSubscriber(subscriber, callback);
		}
	}

	public void SendEvent<E>(object invoker, E args) where E : struct
	{
		GameEvent e;
		if (eventLookup.TryGetValue(TypeID<E>.ID, out e))
		{
			((GameEvent<E>)e).Invoke(invoker, args);
		}
	}

	public void SendEvent(object invoker, object args)
	{
		GameEvent e;
		if (eventLookup.TryGetValue(TypeID.GetID(args) , out e))
		{
			e.Invoke(invoker, args);
		}
	}

	public static void SendGlobalEvent<E>(object invoker, E args) where E : struct
	{
		GameEvent e;
		if (globalEventLookup.TryGetValue(TypeID<E>.ID, out e))
		{
			((GameEvent<E>)e).Invoke(invoker, args);
		}
	}

	public static void SendGlobalEvent(object invoker, object args)
	{
		GameEvent e;
		if (globalEventLookup.TryGetValue(TypeID.GetID(args), out e))
		{
			e.Invoke(invoker, args);
		}
	}

	class GameEvent<E> : GameEvent
	{
		public Action<E> gameEvent;
		public void Invoke(object system, E args)
		{
			if (gameEvent != null)
				gameEvent(args);

			#if UNITY_EDITOR
			if (EventInvokers.Count >= 10)
			{
				EventInvokers.Dequeue();
			}
			EventInvokers.Enqueue(new EventInvoker(){invoker = system, frame = Time.frameCount});
			#endif
		}

		public override void Invoke (object system, object args)
		{
			if (gameEvent != null)
				gameEvent((E)args);
			
			#if UNITY_EDITOR
			if (EventInvokers.Count >= 10)
			{
				EventInvokers.Dequeue();
			}
			EventInvokers.Enqueue(new EventInvoker(){invoker = system, frame = Time.frameCount});
			#endif
		}

		public void AddSubscriber(object system, Action<E> eventListener)
		{
			#if UNITY_EDITOR
				Subscriber.Add(system);
			#endif
			gameEvent += eventListener;
		}

		public void RemoveSubscriber(object system, Action<E> eventListener)
		{
			#if UNITY_EDITOR
				Subscriber.Remove(system);
			#endif
			gameEvent -= eventListener;
		}

		public override string GetEventType ()
		{
			return typeof(E).ToString();
		}
	}

	public class GameEvent
	{
		#if UNITY_EDITOR
		public bool show;
		public List<object> Subscriber = new List<object>();
		public Queue<EventInvoker> EventInvokers = new Queue<EventInvoker>();
		#endif

		public virtual string GetEventType()
		{
			return "";
		}

		public virtual void Invoke(object invoker, object obj)
		{}

		public struct EventInvoker
		{
			public object invoker;
			public int frame;
		}
	}
}

public static class EventHandlerExtensions
{
	public static void SendEvent<E>(this GameObject go, object Invoker, E EventArgs) where E : struct
	{
		var e = go.GetComponentInParent<EventHandler>();
		if (e)
			e.SendEvent(Invoker, EventArgs);
	}

	public static void SendEvent<E>(this Collider col, object Invoker, E EventArgs) where E : struct
	{
		var e = col.GetComponentInParent<EventHandler>();
		if (e)
			e.SendEvent(Invoker, EventArgs);
	}
}

#if UNITY_EDITOR
[CustomEditor(typeof(EventHandler))]
class EntityEventHandlerInspector : Editor
{
	string[] eventTypeDisplay = new string[]{"Local", "Global"};
	int eventType = 0;

	GUIContent[] eventDisplay = new GUIContent[]{new GUIContent("Subscribers", "Display Subscribers To Messages"), new GUIContent("Invokers", "Display preivous Event Invokers")};
	int eventInfoOption = 0;

	enum SearchType
	{
		Event,
		Subscriber
	}

	SearchType searchType = SearchType.Event;
	string searchValue = "";

	// Which window to display
	int window = 0;
	// How many events to show on screen at once
	int eventItems = 10;

	public override void OnInspectorGUI ()
	{
		if (!Application.isPlaying)
		{
			DrawDefaultInspector();
			return;
		}
		eventType = GUILayout.Toolbar(eventType, eventTypeDisplay);

		if (eventType == 0)
		{
			var prop = target.GetType().GetField("eventLookup", BindingFlags.Instance| BindingFlags.NonPublic).GetValue(target);
			var dic = (Dictionary<int, EventHandler.GameEvent>)prop;

			DrawEvents(dic, "Events.");
		}
		else
		{
			var prop = target.GetType().GetField("globalEventLookup", BindingFlags.Static| BindingFlags.NonPublic).GetValue(target);
			var dic = (Dictionary<int, EventHandler.GameEvent>)prop;
			DrawEvents(dic, "GlobalEvents.");
		}
	}

	public override bool RequiresConstantRepaint ()
	{
		if (!Application.isPlaying)
			return false;
		return true;
	}

	bool SelectEvent(EventHandler.GameEvent e)
	{
		switch(searchType)
		{
		case SearchType.Event:
			if (e.Subscriber.Count == 0)
				return false;
			return e.GetEventType().IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0;
		case SearchType.Subscriber:
			foreach(var item in e.Subscriber)
			{
				if (item == null)
					return false;
				if (item.GetType().ToString().IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0)
					return true;
			}
			return false;
		default : return false;
		}
	}

	public void DrawEvents(Dictionary<int, EventHandler.GameEvent> dic, string removeString)
	{
		// Sort for desired events
		var Events = 
			(
				from e in dic.Values
				where SelectEvent(e)
				orderby e.GetEventType() ascending
				select e
			).ToArray();

		eventInfoOption = GUILayout.Toolbar(eventInfoOption , eventDisplay);
		EditorGUILayout.LabelField(string.Format("Event Count : {0}", Events.Length), EditorStyles.boldLabel);
		EditorGUILayout.LabelField(string.Format("Current Frame : {0}", Time.frameCount));

		EditorGUILayout.BeginHorizontal();
		searchValue = EditorGUILayout.TextField("Search", searchValue, GUILayout.Width(Screen.width - Screen.width / 5f));
		searchType = (SearchType)EditorGUILayout.EnumPopup(searchType);
		EditorGUILayout.EndHorizontal();

		// Draw naviation buttons if there is too many to show 
		if (Events.Length > eventItems)
		{
			EditorGUILayout.LabelField(string.Format("Page {0}", window, GUILayout.Width(Screen.width / 6f)));
			EditorGUILayout.BeginHorizontal();
			if (window*eventItems < eventItems)
			{
				GUI.enabled = false;
			}
			if (GUILayout.Button(string.Format("Previous {0}", eventItems)))
			{
				window --;
			}
			GUI.enabled = true;

			if (window * eventItems >= Events.Length)
			{
				Debug.Log("Shrink");
				window = (Events.Length - Events.Length % eventItems)/eventItems;
				if (window * eventItems == Events.Length)
					window -= 1;
			}

			if (window * eventItems + eventItems >= Events.Length)
			{
				GUI.enabled = false;
			}
			if (GUILayout.Button(string.Format("Next {0}", eventItems)))
			{
				window ++;	
			}
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();
		}
		else window = 0;

		// Draw Event Info
		for(int itr = window * eventItems; itr < window*eventItems+eventItems; ++itr)
		{
			if (itr > Events.Length-1)
				continue;
			var item = Events[itr];

			if (item.Subscriber.Count == 0)
				continue;

			var foldout = item.GetEventType().ToString().Replace(removeString, "");

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			{
				EditorGUI.indentLevel ++;
				item.show = EditorGUILayout.Foldout(item.show, new GUIContent(string.Format("{0} : {1}", foldout, item.Subscriber.Count), "Event Name"), true);
				if (item.show)
				{					
					if (eventInfoOption == 0)
					{
						foreach(var subscriber in item.Subscriber)
						{
							if (searchType == SearchType.Subscriber)
							{
								if (subscriber == null || !(subscriber.GetType().ToString().IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0))
								{
									continue;
								}
							}

							if (subscriber == null)
							{
								EditorGUILayout.LabelField("NULL SUBSCRIBER");
							}
							else if (subscriber is MonoBehaviour)
							{
								var system = subscriber as MonoBehaviour;
								if (GUI.Button(EditorGUI.IndentedRect(EditorGUILayout.GetControlRect()), string.Format("{1} ({0})", system.gameObject.name, system.GetType().ToString()), EditorStyles.label))
								{
									EditorGUIUtility.PingObject(system);
								}
							}
							else
							{
								EditorGUILayout.LabelField(subscriber.GetType().ToString());
							}
						}
					}
					else
					{
						var invokers = item.EventInvokers.ToArray();
						if (invokers.Length > 0)
						{
							for (int i = invokers.Length-1; i >= 0; --i)
							{
								var invoker = invokers[i];
								EditorGUILayout.BeginHorizontal();

								EditorGUILayout.LabelField(string.Format("Frame:{0}", invoker.frame), GUILayout.Width(Screen.width/4f));

								if (invoker.invoker is MonoBehaviour)
								{
									var invokeSystem = invoker.invoker as MonoBehaviour;
									if (invokeSystem == null)
									{
										EditorGUILayout.LabelField("Deleted");
									}
									else
									{
										if (GUILayout.Button(string.Format("{0}:{1}", invokeSystem.gameObject.name,invokeSystem.GetType().ToString()), EditorStyles.label))
										{
											EditorGUIUtility.PingObject(invokeSystem);
										}
									}									
								}
								else
								{
									if (invoker.invoker == null)
										EditorGUILayout.LabelField("NULL INVOKER");
									else
										EditorGUILayout.LabelField(invoker.invoker.GetType().ToString());
								}	

								EditorGUILayout.EndHorizontal();
							}
						}
						else EditorGUILayout.LabelField("No Invokers");
					}
								
				}
				EditorGUI.indentLevel --;
			}
			EditorGUILayout.EndVertical();
		}
	}
}
#endif

